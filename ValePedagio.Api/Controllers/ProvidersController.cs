using Microsoft.AspNetCore.Mvc;
using ValePedagio.Application;

namespace ValePedagio.Api.Controllers;

[ApiController]
[Route("api/v1/vale-pedagio/provedores")]
public sealed class ProvidersController : ControllerBase
{
    private readonly IValePedagioApplicationService _service;

    public ProvidersController(IValePedagioApplicationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ValePedagioProviderSummaryDto>>> GetAsync(CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var providers = await _service.ListProvidersAsync(tenantId, cancellationToken);
        return Ok(providers);
    }

    private string ResolveTenantId()
    {
        return Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId.ToString()
            : "default";
    }
}
