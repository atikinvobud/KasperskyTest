
using System.Threading.Channels;

namespace Backend.Scanning;

public class ScanTaskQueue : IScanTaskQueue
{
    private readonly Channel<Guid> _queue =
        Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid scanTaskId)
    {
        if (!_queue.Writer.TryWrite(scanTaskId))
        {
            throw new InvalidOperationException("Не удалось добавить задачу сканирования в очередь.");
        }
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}
