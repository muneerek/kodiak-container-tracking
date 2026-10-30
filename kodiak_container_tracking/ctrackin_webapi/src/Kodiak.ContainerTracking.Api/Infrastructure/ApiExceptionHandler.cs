using Kodiak.ContainerTracking.Business.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Kodiak.ContainerTracking.DataAccess.Entities;

namespace Kodiak.ContainerTracking.Api.Infrastructure;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, detail) = exception switch
        {
            BusinessException e => (e.StatusCode, e.Code, e.Message),
            DbUpdateConcurrencyException e when e.Entries.Any(x => x.Entity is UniqueCodeCounter) =>
                (409, "code_generation_conflict", "Code generation encountered a concurrent change. Please retry."),
            DbUpdateConcurrencyException => (412, "stale_version", "The record changed. Reload it before saving."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (409, "duplicate_record", "A record with the same unique values already exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                (409, "related_record_conflict", "A related record is unavailable or incompatible."),
            _ when IsTransactionConflict(exception) => (409, "concurrent_change", "A concurrent change occurred. Reload and retry."),
            _ => (500, "unexpected_error", "An unexpected error occurred.")
        };
        if (status >= 500) logger.LogError(exception, "Request failed. TraceId: {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = code, Detail = detail,
                Extensions = { ["code"] = code, ["traceId"] = context.TraceIdentifier } }
        });
    }

    private static bool IsTransactionConflict(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }
        || exception.InnerException is not null && IsTransactionConflict(exception.InnerException);
}
