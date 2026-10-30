namespace Kodiak.ContainerTracking.Business.Exceptions;

public sealed class BusinessException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
