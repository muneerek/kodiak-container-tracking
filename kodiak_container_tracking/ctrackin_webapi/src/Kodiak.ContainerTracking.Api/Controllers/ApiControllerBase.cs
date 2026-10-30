using System.Globalization;
using Kodiak.ContainerTracking.Business.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Kodiak.ContainerTracking.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected long ExpectedVersion()
    {
        var header = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrEmpty(header))
            throw new BusinessException(428, "precondition_required", "Supply the ETag from GET in the If-Match header.");
        if (header.Length < 3 || header[0] != '"' || header[^1] != '"' ||
            !long.TryParse(header.AsSpan(1, header.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
            throw new BusinessException(400, "invalid_etag", "If-Match must contain one quoted version, for example \"1\".");
        return version;
    }

    protected void SetEtag(string version) => Response.Headers.ETag = $"\"{version}\"";
}
