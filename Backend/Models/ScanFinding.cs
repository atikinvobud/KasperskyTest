namespace Backend.Models;

public class ScanFinding
{
    public required string Name { get; set; }

    public CriticalityLevel Criticality { get; set; }

    public required string FilePath { get; set; }

    public int Line { get; set; }
}