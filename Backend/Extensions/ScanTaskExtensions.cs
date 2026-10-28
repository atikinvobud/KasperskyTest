using Backend.DTOs;
using Backend.Models;

namespace Backend.Extensions;

public static class ScanTaskExtensions
{
    public static ScanTask ToModel(this StartScanDto dto)
    {
        var now = DateTime.UtcNow;

        return new ScanTask
        {
            Id = Guid.NewGuid(),
            RepositoryUrl = dto.RepositoryUrl,
            Status = ScanStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now,
            Result = null,
            ErrorMessage = null
        };
    }

    public static GetScanTaskDto ToDto(this ScanTask scanTask)
    {
        return new GetScanTaskDto
        {
            Id = scanTask.Id,
            RepositoryUrl = scanTask.RepositoryUrl,
            Status = scanTask.Status,
            CreatedAt = scanTask.CreatedAt,
            UpdatedAt = scanTask.UpdatedAt,
            Result = scanTask.Result?.Select(finding => finding.ToDto()).ToList(),
            ErrorMessage = scanTask.ErrorMessage
        };
    }

    public static ScanFindingDto ToDto(this ScanFinding finding)
    {
        return new ScanFindingDto
        {
            Name = finding.Name,
            Criticality = finding.Criticality,
            FilePath = finding.FilePath,
            Line = finding.Line
        };
    }
    public static void UpdateStatus(this ScanTask scanTask, ScanStatus status,List<ScanFinding>? result = null,string? errorMessage = null)
    {
        scanTask.Status = status;
        scanTask.UpdatedAt = DateTime.UtcNow;

        if (status == ScanStatus.Completed)
        {
            scanTask.Result = result ?? new List<ScanFinding>();
            scanTask.ErrorMessage = null;
        }
        else if (status == ScanStatus.Failed)
        {
            scanTask.Result = null;
            scanTask.ErrorMessage = errorMessage;
        }
        else if (status == ScanStatus.Cancelled)
        {
            scanTask.Result = null;
            scanTask.ErrorMessage = null;
        }
    }
}