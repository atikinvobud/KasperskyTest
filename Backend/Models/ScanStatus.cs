namespace Backend.Models;

public enum ScanStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}