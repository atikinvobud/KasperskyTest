using System.Collections.Concurrent;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Repositories;
using Backend.Scanning;
using Backend.Share;

namespace Backend.Services;

public class ScanService : IScanService
{
    private readonly IScanRepository _scanRepository;
    private readonly IScanTaskQueue _scanTaskQueue;
    private readonly RepositoryUrlValidator _urlValidator;

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource>  _cancellationSources = new();

    private readonly object _sync = new();

    public ScanService(IScanRepository scanRepository, IScanTaskQueue scanTaskQueue, RepositoryUrlValidator urlValidator)
    {
        _scanRepository = scanRepository;
        _scanTaskQueue = scanTaskQueue;
        _urlValidator = urlValidator;
    }

    public async Task<Result<GetScanTaskDto>> StartScanAsync( StartScanDto dto, CancellationToken cancellationToken = default)
    {
        if (!await _urlValidator.IsValidAsync( dto.RepositoryUrl, cancellationToken))
        {
            return Result<GetScanTaskDto>.Error(ErrorCode.InvalidRepositoryUrl);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var scanTask = dto.ToModel();
        var cts = new CancellationTokenSource();

        lock (_sync)
        {
            _scanRepository.Add(scanTask);
            _cancellationSources[scanTask.Id] = cts;

            try
            {
                _scanTaskQueue.Enqueue(scanTask.Id);
            }
            catch
            {
                _cancellationSources.TryRemove(scanTask.Id, out _);

                cts.Dispose();
                throw;
            }
        }

        return Result<GetScanTaskDto>.Success(scanTask.ToDto());
    }

    public Result<GetScanTaskDto> GetById(Guid id)
    {
        lock (_sync)
        {
            var scanTask = _scanRepository.GetById(id);

            if (scanTask is null)
            {
                return Result<GetScanTaskDto>.Error(ErrorCode.ScanTaskNotFound);
            }

            return Result<GetScanTaskDto>.Success(scanTask.ToDto());
        }
    }

    public bool TryStartScan(Guid id)
    {
        lock (_sync)
        {
            var scanTask = _scanRepository.GetById(id);

            if (scanTask is null || scanTask.Status != ScanStatus.Queued)
            {
                return false;
            }

            scanTask.UpdateStatus(ScanStatus.Running);
            return _scanRepository.Update(scanTask);
        }
    }

    public CancellationToken GetCancellationToken(Guid id)
    {
        return _cancellationSources.TryGetValue(id, out var cts) ? cts.Token : CancellationToken.None;
    }

    public void ReleaseCancellationSource(Guid id)
    {
        if (_cancellationSources.TryRemove(id, out var cts))
        {
            cts.Dispose();
        }
    }

    public Result<CancelScanResultDto> CancelScan(Guid id)
    {
        CancellationTokenSource? cts = null;
        var disposeSource = false;
        CancelScanResultDto response;

        lock (_sync)
        {
            var scanTask = _scanRepository.GetById(id);

            if (scanTask is null)
            {
                return Result<CancelScanResultDto>.Error(ErrorCode.ScanTaskNotFound);
            }

            var requestedAt = DateTime.UtcNow;

            if (IsTerminal(scanTask.Status))
            {
                return Result<CancelScanResultDto>.Success(
                    new CancelScanResultDto
                    {
                        Id = id,
                        RequestedAt = requestedAt,
                        Success = false
                    });
            }

            var wasQueued = scanTask.Status == ScanStatus.Queued;

            scanTask.UpdateStatus(ScanStatus.Cancelled);

            if (!_scanRepository.Update(scanTask))
            {
                return Result<CancelScanResultDto>.Error(ErrorCode.ScanTaskNotFound);
            }

            _cancellationSources.TryGetValue(id, out cts);

            if (wasQueued && _cancellationSources.TryRemove(id, out var removed))
            {
                cts = removed;
                disposeSource = true;
            }

            response = new CancelScanResultDto
            {
                Id = id,
                RequestedAt = requestedAt,
                Success = true
            };
        }

        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            finally
            {
                if (disposeSource)
                {
                    cts.Dispose();
                }
            }
        }

        return Result<CancelScanResultDto>.Success(response);
    }

    public Result<GetScanTaskDto> UpdateStatus(Guid id, ScanStatus status, List<ScanFinding>? result = null, string? errorMessage = null)
    {
        lock (_sync)
        {
            var scanTask = _scanRepository.GetById(id);

            if (scanTask is null)
            {
                return Result<GetScanTaskDto>.Error(ErrorCode.ScanTaskNotFound);
            }

            if (IsTerminal(scanTask.Status))
            {
                return Result<GetScanTaskDto>.Success(scanTask.ToDto());
            }

            scanTask.UpdateStatus(status, result, errorMessage);

            if (!_scanRepository.Update(scanTask))
            {
                return Result<GetScanTaskDto>.Error(ErrorCode.ScanTaskNotFound);
            }

            return Result<GetScanTaskDto>.Success(scanTask.ToDto());
        }
    }

    private static bool IsTerminal(ScanStatus status)
    {
        return status is ScanStatus.Completed or ScanStatus.Failed or ScanStatus.Cancelled;
    }
}