using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kodiak.ContainerTracking.DataAccess.Entities;
using Kodiak.ContainerTracking.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public sealed class TrackingUnitOfWork(TrackingDbContext db) : IUnitOfWork
{
    public async Task SaveAsync(long? actorId, string actorSubject, string reason, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var auditActor = actorId?.ToString() ?? actorSubject;
        if (auditActor.Length > 50) auditActor = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(auditActor)))[..43];
        var changes = db.ChangeTracker.Entries<AuditableEntity>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(x => new
            {
                Entry = x, Action = x.State.ToString(),
                Fields = x.Properties.Where(p => p.Metadata.Name != nameof(ApplicationUser.UserPassword) && p.Metadata.Name != nameof(UserToken.TokenHash))
                    .ToDictionary(p => p.Metadata.Name, p => new { Before = p.OriginalValue, After = p.CurrentValue })
            }).ToArray();
        foreach (var entry in db.ChangeTracker.Entries<AuditableEntity>().Where(x => x.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreateOn = now;
                entry.Entity.CreatedBy = auditActor;
            }
            else
            {
                entry.Property(x => x.CreateOn).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
            }
            entry.Entity.UpdatedOn = now;
            entry.Entity.UpdatedBy = auditActor;
        }
        await db.SaveChangesAsync(ct);
        db.AccessAuditEvents.Add(new AccessAuditEvent
        {
            OccurredOn = now, ActorSubject = actorSubject, Reason = reason,
            Changes = JsonSerializer.Serialize(changes.Select(x => new
            {
                Entity = x.Entry.Metadata.ClrType.Name, x.Action,
                Key = x.Entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).ToDictionary(p => p.Metadata.Name, p => p.CurrentValue),
                x.Fields
            }))
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await action();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception exception) when (attempt < 7 && CanRetry(exception))
            {
                // Re-read counters and business records; no external side effects occur in these transactions.
                db.ChangeTracker.Clear();
                var delay = Math.Min(25 * (1 << attempt), 250) + Random.Shared.Next(0, 25);
                await Task.Delay(TimeSpan.FromMilliseconds(delay), ct);
            }
        }
    }

    private static bool CanRetry(Exception exception) =>
        exception is DbUpdateConcurrencyException concurrency && concurrency.Entries.Any(x => x.Entity is UniqueCodeCounter)
        || exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }
        || exception.InnerException is not null && CanRetry(exception.InnerException);
}
