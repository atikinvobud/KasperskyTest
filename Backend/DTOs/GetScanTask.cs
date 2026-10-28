using Backend.Models;

namespace Backend.DTOs;

public record GetScanTaskDto
{
    public Guid Id { get; set; }

    public required string RepositoryUrl { get; set; }

    public ScanStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<ScanFindingDto>? Result { get; set; }

    public string? ErrorMessage { get; set; }
}