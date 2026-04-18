using Microsoft.AspNetCore.Mvc;
using ValePedagio.Application;
using ValePedagio.Domain;

namespace ValePedagio.Api.Controllers;

[ApiController]
[Route("api/v1/vale-pedagio/configuracoes")]
public sealed class ConfiguracoesController : ControllerBase
{
    private readonly IValePedagioApplicationService _service;

    public ConfiguracoesController(IValePedagioApplicationService service)
    {
        _service = service;
    }

    [HttpGet("{provider}")]
    public async Task<ActionResult<ValePedagioProviderConfigurationDto>> GetAsync(
        ValePedagioProviderType provider,
        CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var config = await _service.GetProviderConfigurationAsync(tenantId, provider, cancellationToken);
        return Ok(config);
    }

    [HttpPut("{provider}")]
    public async Task<ActionResult<ValePedagioProviderConfigurationDto>> PutAsync(
        ValePedagioProviderType provider,
        [FromBody] ValePedagioProviderConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var config = await _service.UpdateProviderConfigurationAsync(tenantId, provider, request, cancellationToken);
        return Ok(config);
    }

    private string ResolveTenantId()
    {
        return Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId.ToString()
            : "default";
    }
}
