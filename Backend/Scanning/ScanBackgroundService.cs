
using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Scanning;

public class ScanBackgroundService : BackgroundService
{
    private readonly IScanTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScanBackgroundService> _logger;
    private readonly ScanOptions _options;

    public ScanBackgroundService(
        IScanTaskQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ScanBackgroundService> logger,
        IOptions<ScanOptions> options)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var workerCount = Math.Max(1, _options.MaxConcurrentScans);

        _logger.LogInformation(
            "Запущено обработчиков сканирования: {WorkerCount}",
            workerCount);

        var workers = Enumerable
            .Range(0, workerCount)
            .Select(workerId => ProcessQueueAsync(workerId, stoppingToken));

        await Task.WhenAll(workers);
    }

    private async Task ProcessQueueAsync(
        int workerId,
        CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var scanTaskId in
                _queue.ReadAllAsync(stoppingToken))
            {
                stoppingToken.ThrowIfCancellationRequested();

                using var scope = _scopeFactory.CreateScope();

                var scanService = scope.ServiceProvider
                    .GetRequiredService<IScanService>();

                _logger.LogInformation(
                    "Обработчик {WorkerId} получил задачу {ScanTaskId}",
                    workerId,
                    scanTaskId);

                // Задача могла быть отменена, пока находилась в очереди.
                if (!scanService.TryStartScan(scanTaskId))
                {
                    scanService.ReleaseCancellationSource(scanTaskId);

                    _logger.LogInformation(
                        "Задача {ScanTaskId} пропущена: запуск невозможен",
                        scanTaskId);

                    continue;
                }

                try
                {
                    var taskResult = scanService.GetById(scanTaskId);

                    if (!taskResult.IsSuccess ||
                        taskResult.Value is null)
                    {
                        _logger.LogWarning(
                            "Не удалось получить задачу {ScanTaskId}",
                            scanTaskId);

                        continue;
                    }

                    var scanner = scope.ServiceProvider
                        .GetRequiredService<IRepositoryScanner>();

                    var taskToken =
                        scanService.GetCancellationToken(scanTaskId);

                    using var linkedCts =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            stoppingToken,
                            taskToken);

                    _logger.LogInformation(
                        "Задача {ScanTaskId}: запуск сканирования",
                        scanTaskId);

                    var findings = await scanner.ScanAsync(
                        taskResult.Value.RepositoryUrl,
                        linkedCts.Token);

                    // Если отмена пришла одновременно с завершением
                    // сканирования, не записываем Completed.
                    linkedCts.Token.ThrowIfCancellationRequested();

                    scanService.UpdateStatus(
                        scanTaskId,
                        ScanStatus.Completed,
                        findings);

                    _logger.LogInformation(
                        "Задача {ScanTaskId} завершена. Найдено: {Count}",
                        scanTaskId,
                        findings.Count);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    scanService.UpdateStatus(
                        scanTaskId,
                        ScanStatus.Cancelled);

                    _logger.LogInformation(
                        "Задача {ScanTaskId} прервана при остановке приложения",
                        scanTaskId);

                    throw;
                }
                catch (OperationCanceledException)
                    when (scanService
                        .GetCancellationToken(scanTaskId)
                        .IsCancellationRequested)
                {
                    scanService.UpdateStatus(
                        scanTaskId,
                        ScanStatus.Cancelled);

                    _logger.LogInformation(
                        "Задача {ScanTaskId} отменена пользователем",
                        scanTaskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Ошибка при сканировании задачи {ScanTaskId}",
                        scanTaskId);

                    scanService.UpdateStatus(
                        scanTaskId,
                        ScanStatus.Failed,
                        errorMessage:
                            "Во время сканирования произошла ошибка.");
                }
                finally
                {
                    scanService.ReleaseCancellationSource(scanTaskId);

                    _logger.LogInformation(
                        "Обработчик {WorkerId} освободил задачу {ScanTaskId}",
                        workerId,
                        scanTaskId);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Обработчик {WorkerId} остановлен",
                workerId);
        }
    }
}
