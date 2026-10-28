using Backend.DTOs;
using Backend.Models;
using Backend.Scanning;
using Backend.Services;
using Backend.Share;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.Tests.Scanning;

public class ScanBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ScanSucceeds_UpdatesStatusToCompleted()
    {
        var scanTaskId = Guid.NewGuid();
        var repositoryUrl = "https://github.com/example/repository";
        var scanTask = CreateScanTask(scanTaskId, repositoryUrl);

        var findings = new List<ScanFinding>
        {
            new()
            {
                Name = "Hardcoded password",
                Criticality = CriticalityLevel.High,
                FilePath = "src/Config.cs",
                Line = 15
            }
        };

        var scanService = new Mock<IScanService>();
        var scanner = new Mock<IRepositoryScanner>();
        var queue = new ScanTaskQueue();

        SetupTask(scanService, scanTaskId, scanTask);

        scanService
            .Setup(x => x.UpdateStatus(
                scanTaskId,
                ScanStatus.Completed,
                findings,
                null))
            .Returns(CreateSuccessResult(scanTask));

        scanner
            .Setup(x => x.ScanAsync(
                repositoryUrl,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(findings);

        queue.Enqueue(scanTaskId);

        using var provider = CreateProvider(scanService.Object, scanner.Object);
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var service = CreateBackgroundService(queue, provider, loggerFactory, 1);

        await service.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntilAsync(() =>
                scanService.Invocations.Any(x =>
                    x.Method.Name == nameof(IScanService.UpdateStatus) &&
                    x.Arguments[1] is ScanStatus status &&
                    status == ScanStatus.Completed));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        scanner.Verify(
            x => x.ScanAsync(repositoryUrl, It.IsAny<CancellationToken>()),
            Times.Once);

        scanService.Verify(x => x.TryStartScan(scanTaskId), Times.Once);

        scanService.Verify(
            x => x.UpdateStatus(
                scanTaskId,
                ScanStatus.Completed,
                findings,
                null),
            Times.Once);

        scanService.Verify(
            x => x.ReleaseCancellationSource(scanTaskId),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ScannerThrows_UpdatesStatusToFailed()
    {
        var scanTaskId = Guid.NewGuid();
        var repositoryUrl = "https://github.com/example/repository";
        var scanTask = CreateScanTask(scanTaskId, repositoryUrl);

        var scanService = new Mock<IScanService>();
        var scanner = new Mock<IRepositoryScanner>();
        var queue = new ScanTaskQueue();

        SetupTask(scanService, scanTaskId, scanTask);

        scanService
            .Setup(x => x.UpdateStatus(
                scanTaskId,
                ScanStatus.Failed,
                null,
                "Во время сканирования произошла ошибка."))
            .Returns(CreateSuccessResult(scanTask));

        scanner
            .Setup(x => x.ScanAsync(
                repositoryUrl,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Scanner error"));

        queue.Enqueue(scanTaskId);

        using var provider = CreateProvider(scanService.Object, scanner.Object);
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var service = CreateBackgroundService(queue, provider, loggerFactory, 1);

        await service.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntilAsync(() =>
                scanService.Invocations.Any(x =>
                    x.Method.Name == nameof(IScanService.UpdateStatus) &&
                    x.Arguments[1] is ScanStatus status &&
                    status == ScanStatus.Failed));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        scanService.Verify(
            x => x.UpdateStatus(
                scanTaskId,
                ScanStatus.Failed,
                null,
                "Во время сканирования произошла ошибка."),
            Times.Once);

        scanService.Verify(
            x => x.ReleaseCancellationSource(scanTaskId),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TaskDoesNotExist_DoesNotCallScanner()
    {
        var scanTaskId = Guid.NewGuid();

        var scanService = new Mock<IScanService>();
        var scanner = new Mock<IRepositoryScanner>();
        var queue = new ScanTaskQueue();

        scanService
            .Setup(x => x.TryStartScan(scanTaskId))
            .Returns(true);

        scanService
            .Setup(x => x.GetById(scanTaskId))
            .Returns(CreateErrorResult<GetScanTaskDto>());

        queue.Enqueue(scanTaskId);

        using var provider = CreateProvider(scanService.Object, scanner.Object);
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var service = CreateBackgroundService(queue, provider, loggerFactory, 1);

        await service.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntilAsync(() =>
                scanService.Invocations.Any(x =>
                    x.Method.Name == nameof(IScanService.GetById)));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        scanner.Verify(
            x => x.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        scanService.Verify(
            x => x.UpdateStatus(
                It.IsAny<Guid>(),
                It.IsAny<ScanStatus>(),
                It.IsAny<List<ScanFinding>?>(),
                It.IsAny<string?>()),
            Times.Never);

        scanService.Verify(
            x => x.ReleaseCancellationSource(scanTaskId),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TaskCancelled_SetsCancelledStatus()
    {
        var scanTaskId = Guid.NewGuid();
        var repositoryUrl = "https://github.com/example/repository";
        var scanTask = CreateScanTask(scanTaskId, repositoryUrl);

        using var taskCts = new CancellationTokenSource();

        var scanService = new Mock<IScanService>();
        var scanner = new Mock<IRepositoryScanner>();
        var queue = new ScanTaskQueue();

        SetupTask(scanService, scanTaskId, scanTask, taskCts.Token);

        scanService
            .Setup(x => x.UpdateStatus(
                scanTaskId,
                ScanStatus.Cancelled,
                null,
                null))
            .Returns(CreateSuccessResult(scanTask));

        var scanStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        scanner
            .Setup(x => x.ScanAsync(
                repositoryUrl,
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken token) =>
            {
                scanStarted.TrySetResult();

                await Task.Delay(Timeout.Infinite, token);

                return new List<ScanFinding>();
            });

        queue.Enqueue(scanTaskId);

        using var provider = CreateProvider(scanService.Object, scanner.Object);
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var service = CreateBackgroundService(queue, provider, loggerFactory, 1);

        await service.StartAsync(CancellationToken.None);

        try
        {
            await scanStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            taskCts.Cancel();

            await WaitUntilAsync(() =>
                scanService.Invocations.Any(x =>
                    x.Method.Name == nameof(IScanService.UpdateStatus) &&
                    x.Arguments[1] is ScanStatus status &&
                    status == ScanStatus.Cancelled));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        scanService.Verify(
            x => x.UpdateStatus(
                scanTaskId,
                ScanStatus.Cancelled,
                null,
                null),
            Times.Once);

        scanService.Verify(
            x => x.ReleaseCancellationSource(scanTaskId),
            Times.Once);

        scanService.Verify(
            x => x.UpdateStatus(
                scanTaskId,
                ScanStatus.Completed,
                It.IsAny<List<ScanFinding>>(),
                null),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TaskCannotStart_DoesNotCallScanner()
    {
        var scanTaskId = Guid.NewGuid();

        var scanService = new Mock<IScanService>();
        var scanner = new Mock<IRepositoryScanner>();
        var queue = new ScanTaskQueue();

        scanService
            .Setup(x => x.TryStartScan(scanTaskId))
            .Returns(false);

        queue.Enqueue(scanTaskId);

        using var provider = CreateProvider(scanService.Object, scanner.Object);
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var service = CreateBackgroundService(queue, provider, loggerFactory, 1);

        await service.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntilAsync(() =>
                scanService.Invocations.Any(x =>
                    x.Method.Name == nameof(IScanService.TryStartScan)));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        scanner.Verify(
            x => x.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        scanService.Verify(
            x => x.ReleaseCancellationSource(scanTaskId),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsMaxConcurrentScans()
    {
        const int maxConcurrentScans = 2;
        const int taskCount = 3;

        var scanService = new Mock<IScanService>();
        var scanner = new Mock<IRepositoryScanner>();
        var queue = new ScanTaskQueue();

        var started = new System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskCompletionSource>();
        var taskIds = new List<Guid>();

        for (var i = 0; i < taskCount; i++)
        {
            var id = Guid.NewGuid();
            var url = $"https://github.com/example/repository{i}";
            var dto = CreateScanTask(id, url);

            taskIds.Add(id);
            started[id] = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            SetupTask(scanService, id, dto);

            scanService
                .Setup(x => x.UpdateStatus(
                    id,
                    ScanStatus.Completed,
                    It.IsAny<List<ScanFinding>>(),
                    null))
                .Returns(CreateSuccessResult(dto));

            scanner
                .Setup(x => x.ScanAsync(url, It.IsAny<CancellationToken>()))
                .Returns(async (string _, CancellationToken token) =>
                {
                    started[id].TrySetResult();
                    await Task.Delay(Timeout.Infinite, token);
                    return new List<ScanFinding>();
                });

            queue.Enqueue(id);
        }

        using var provider = CreateProvider(scanService.Object, scanner.Object);
        using var loggerFactory = LoggerFactory.Create(_ => { });

        var service = CreateBackgroundService(
            queue, provider, loggerFactory, maxConcurrentScans);

        await service.StartAsync(CancellationToken.None);

        try
        {
            await Task.WhenAll(
                started[taskIds[0]].Task,
                started[taskIds[1]].Task
            ).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(started[taskIds[2]].Task.IsCompleted);

            // Освобождаем один обработчик через отмену первой задачи.
            // В этом тесте используем остановку сервиса для завершения
            // всех сканеров и проверяем, что третья задача не стартовала
            // до освобождения одного из обработчиков.
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static void SetupTask(
        Mock<IScanService> scanService,
        Guid id,
        GetScanTaskDto dto,
        CancellationToken token = default)
    {
        scanService
            .Setup(x => x.TryStartScan(id))
            .Returns(true);

        scanService
            .Setup(x => x.GetById(id))
            .Returns(CreateSuccessResult(dto));

        scanService
            .Setup(x => x.GetCancellationToken(id))
            .Returns(token);
    }

    private static GetScanTaskDto CreateScanTask(Guid id, string url)
    {
        return new GetScanTaskDto
        {
            Id = id,
            RepositoryUrl = url,
            Status = ScanStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ScanBackgroundService CreateBackgroundService(
        ScanTaskQueue queue,
        ServiceProvider provider,
        ILoggerFactory loggerFactory,
        int maxConcurrentScans)
    {
        return new ScanBackgroundService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            loggerFactory.CreateLogger<ScanBackgroundService>(),
            Options.Create(new ScanOptions
            {
                MaxConcurrentScans = maxConcurrentScans
            }));
    }

    private static ServiceProvider CreateProvider(
        IScanService scanService,
        IRepositoryScanner scanner)
    {
        var services = new ServiceCollection();

        services.AddSingleton(scanService);
        services.AddSingleton(scanner);

        return services.BuildServiceProvider();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));

        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, timeout.Token);
        }
    }

    private static Result<T> CreateSuccessResult<T>(T value)
    {
        return Result<T>.Success(value);
    }

    private static Result<T> CreateErrorResult<T>()
    {
        return Result<T>.Error(ErrorCode.VulnerabilityNotFound);
    }
}
