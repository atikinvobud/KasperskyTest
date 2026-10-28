namespace Backend.Models;

public class ScanTask
{
    public Guid Id { get; set; }

    public required string RepositoryUrl { get; set; }

    public ScanStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<ScanFinding>? Result { get; set; }

    public string? ErrorMessage { get; set; }
}