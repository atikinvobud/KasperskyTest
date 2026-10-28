
using Backend.DTOs;
using Backend.Extensions;
using Backend.Services;
using Backend.Share;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("vulnerability")]
public class VulnerabilitiesController : ControllerBase
{
    private readonly IVulnerabilityService _vulnerabilityService;

    public VulnerabilitiesController(
        IVulnerabilityService vulnerabilityService)
    {
        _vulnerabilityService = vulnerabilityService;
    }

    [HttpPost]
    public IActionResult Create([FromBody] PostVulnerabilityDto dto)
    {
        var result = _vulnerabilityService.Create(dto);
        return result.ToHttpResult();
    }

    [HttpGet("{id}")]
    public IActionResult GetById([FromRoute]Guid id)
    {
        var result = _vulnerabilityService.GetById(id);
        return result.ToHttpResult();
    }
}
