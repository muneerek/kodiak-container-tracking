namespace Kodiak.ContainerTracking.DataAccess.Repositories;

public interface IUnitOfWork
{
    Task SaveAsync(long? actorId, string actorSubject, string reason, CancellationToken ct);
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
}
