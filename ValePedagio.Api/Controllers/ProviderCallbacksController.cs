using Microsoft.AspNetCore.Mvc;
using ValePedagio.Application;
using ValePedagio.Domain;

namespace ValePedagio.Api.Controllers;

[ApiController]
[Route("api/v1/vale-pedagio/provedores/{provider}/callbacks")]
public sealed class ProviderCallbacksController : ControllerBase
{
    private readonly IValePedagioApplicationService _service;

    public ProviderCallbacksController(IValePedagioApplicationService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ValePedagioSolicitacaoResponse>> PostAsync(
        ValePedagioProviderType provider,
        [FromBody] ValePedagioProviderCallbackRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        try
        {
            var response = await _service.ProcessCallbackAsync(tenantId, provider, request, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private string ResolveTenantId()
    {
        return Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId.ToString()
            : "default";
    }
}
