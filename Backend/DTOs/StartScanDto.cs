namespace Backend.DTOs;

public record StartScanDto
{
    public required string RepositoryUrl { get; set; }
}