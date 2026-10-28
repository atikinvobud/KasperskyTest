
namespace Backend.Scanning;

public interface IScanTaskQueue
{
    void Enqueue(Guid scanTaskId);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
