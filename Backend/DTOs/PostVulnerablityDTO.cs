
using Backend.Models;

namespace Backend.DTOs;

public record PostVulnerabilityDto
{
    public required string Name { get; set; }

    public CriticalityLevel Criticality { get; set; }
}
