using Backend.DTOs;
using Backend.Models;
using Backend.Repositories;
using Backend.Scanning;
using Backend.Services;
using Moq;
namespace Backend.Tests.Services;

public class ScanServiceTests
{
    private readonly Mock<IScanRepository> _repositoryMock;
    private readonly Mock<IScanTaskQueue> _queueMock;
    private readonly ScanService _service;

    public ScanServiceTests()
    {
        _repositoryMock = new Mock<IScanRepository>();
        _queueMock = new Mock<IScanTaskQueue>();

        _service = new ScanService(
            _repositoryMock.Object,
            _queueMock.Object,
            new RepositoryUrlValidator());
    }

    [Fact]
    public async Task StartScanAsync_InvalidUrl_ReturnsError()
    {
        var dto = new StartScanDto
        {
            RepositoryUrl = "invalid-url"
        };

        var result = await _service.StartScanAsync(dto);

        Assert.False(result.IsSuccess);

        _repositoryMock.Verify(
            r => r.Add(It.IsAny<ScanTask>()),
            Times.Never);

        _queueMock.Verify(
            q => q.Enqueue(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsError()
    {
        var id = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetById(id))
            .Returns((ScanTask?)null);

        var result = _service.GetById(id);

        Assert.False(result.IsSuccess);

        _repositoryMock.Verify(
            r => r.GetById(id),
            Times.Once);
    }

    [Fact]
    public void GetById_ExistingTask_ReturnsTask()
    {
        var id = Guid.NewGuid();

        var scanTask = new ScanTask
        {
            Id = id,
            RepositoryUrl = "https://github.com/example/project",
            Status = ScanStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetById(id))
            .Returns(scanTask);

        var result = _service.GetById(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value.Id);
        Assert.Equal(scanTask.RepositoryUrl, result.Value.RepositoryUrl);
        Assert.Equal(ScanStatus.Queued, result.Value.Status);

        _repositoryMock.Verify(
            r => r.GetById(id),
            Times.Once);
    }

    [Fact]
    public void UpdateStatus_UnknownId_ReturnsError()
    {
        var id = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetById(id))
            .Returns((ScanTask?)null);

        var result = _service.UpdateStatus(
            id,
            ScanStatus.Running);

        Assert.False(result.IsSuccess);

        _repositoryMock.Verify(
            r => r.Update(It.IsAny<ScanTask>()),
            Times.Never);
    }

    [Fact]
    public void UpdateStatus_ExistingTask_UpdatesStatus()
    {
        var id = Guid.NewGuid();

        var scanTask = new ScanTask
        {
            Id = id,
            RepositoryUrl = "https://github.com/example/project",
            Status = ScanStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetById(id))
            .Returns(scanTask);

        _repositoryMock
            .Setup(r => r.Update(It.IsAny<ScanTask>()))
            .Returns(true);

        var result = _service.UpdateStatus(
            id,
            ScanStatus.Running);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ScanStatus.Running, result.Value.Status);

        _repositoryMock.Verify(
            r => r.Update(It.Is<ScanTask>(
                task => task.Id == id &&
                        task.Status == ScanStatus.Running)),
            Times.Once);
    }

    [Fact]
    public void UpdateStatus_RepositoryUpdateFails_ReturnsError()
    {

        var id = Guid.NewGuid();

        var scanTask = new ScanTask
        {
            Id = id,
            RepositoryUrl = "https://github.com/example/project",
            Status = ScanStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(r => r.GetById(id))
            .Returns(scanTask);

        _repositoryMock
            .Setup(r => r.Update(It.IsAny<ScanTask>()))
            .Returns(false);

        var result = _service.UpdateStatus(
            id,
            ScanStatus.Running);

        Assert.False(result.IsSuccess);
    }
}
