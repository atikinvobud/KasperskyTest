namespace Backend.DTOs;

public class CancelScanResultDto
{
    public Guid Id { get; set; }

    public DateTime RequestedAt { get; set; }

    public bool Success { get; set; }
}