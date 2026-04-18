using Microsoft.AspNetCore.Mvc;
using ValePedagio.Application;
using ValePedagio.Domain;

namespace ValePedagio.Api.Controllers;

[ApiController]
[Route("api/v1/vale-pedagio/solicitacoes")]
public sealed class SolicitacoesController : ControllerBase
{
    private readonly IValePedagioApplicationService _service;

    public SolicitacoesController(IValePedagioApplicationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<ValePedagioSolicitacaoListResponse>> ListAsync(
        [FromQuery] ValePedagioProviderType? provider,
        [FromQuery] ValePedagioStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var result = await _service.ListSolicitacoesAsync(tenantId, provider, status, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ValePedagioSolicitacaoResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var solicitacao = await _service.GetSolicitacaoAsync(tenantId, id, cancellationToken);
        return solicitacao is null ? NotFound() : Ok(solicitacao);
    }

    [HttpPost("cotar")]
    public async Task<ActionResult<ValePedagioSolicitacaoResponse>> QuoteAsync([FromBody] ValePedagioSolicitacaoRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        try
        {
            var response = await _service.QuoteAsync(tenantId, request, cancellationToken);
            return Created($"/api/v1/vale-pedagio/solicitacoes/{response.Id}", response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("comprar")]
    public async Task<ActionResult<ValePedagioSolicitacaoResponse>> PurchaseAsync([FromBody] ValePedagioSolicitacaoRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        try
        {
            var response = await _service.PurchaseAsync(tenantId, request, cancellationToken);
            return Created($"/api/v1/vale-pedagio/solicitacoes/{response.Id}", response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<ActionResult<ValePedagioSolicitacaoResponse>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        try
        {
            var response = await _service.CancelAsync(tenantId, id, cancellationToken);
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

    [HttpGet("{id:guid}/recibo")]
    public async Task<IActionResult> DownloadReceiptAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var receipt = await _service.GetReceiptAsync(tenantId, id, cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        return File(receipt.Content, receipt.ContentType, receipt.FileName);
    }

    private string ResolveTenantId()
    {
        return Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId)
            ? tenantId.ToString()
            : "default";
    }
}
