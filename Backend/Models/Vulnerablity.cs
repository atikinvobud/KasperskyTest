namespace Backend.Models;

public class Vulnerability
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public CriticalityLevel Criticality { get; set; }

    public required DateTime CreatedAt { get; set; } 
}