using Backend.Models;

namespace Backend.DTOs;

public class ScanFindingDto
{
    public required string Name { get; set; }

    public CriticalityLevel Criticality { get; set; }

    public required string FilePath { get; set; }

    public int Line { get; set; }
}