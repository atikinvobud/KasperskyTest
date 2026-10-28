
using Backend.DTOs;
using Backend.Services;
using Backend.Share;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("scan")]
public class ScansController : ControllerBase
{
    private readonly IScanService _scanService;

    public ScansController(IScanService scanService)
    {
        _scanService = scanService;
    }

    [HttpPost]
    public async Task<IActionResult> StartScan([FromBody] StartScanDto dto, CancellationToken cancellationToken)
    {
        var result = await _scanService.StartScanAsync(dto, cancellationToken);
        return result.ToHttpResult();
    }

    [HttpGet("{id}")]
    public IActionResult GetById([FromRoute]Guid id)
    {
        var result = _scanService.GetById(id);
        return result.ToHttpResult();
    }
    [HttpPost("{id}/cancel")]
    public IActionResult CancelScan([FromRoute] Guid id)
    {
        var result = _scanService.CancelScan(id);
        return result.ToHttpResult();
    }
}
