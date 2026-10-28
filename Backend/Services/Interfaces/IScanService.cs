using Backend.DTOs;
using Backend.Models;
using Backend.Share;

namespace Backend.Services;

public interface IScanService
{
    Task<Result<GetScanTaskDto>> StartScanAsync(StartScanDto dto, CancellationToken cancellationToken = default);

    Result<GetScanTaskDto> GetById(Guid id);

    Result<GetScanTaskDto> UpdateStatus(Guid id, ScanStatus status, List<ScanFinding>? result = null, string? errorMessage = null);

    Result<CancelScanResultDto> CancelScan(Guid id);

    bool TryStartScan(Guid id);

    CancellationToken GetCancellationToken(Guid id);

    void ReleaseCancellationSource(Guid id);
}