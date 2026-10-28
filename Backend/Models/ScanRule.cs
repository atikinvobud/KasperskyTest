namespace Backend.Models;

public class ScanRule
{
    public required string Name { get; init; }

    public CriticalityLevel Criticality { get; init; }

    public required string Pattern { get; init; }

    public required string Description { get; init; }
}