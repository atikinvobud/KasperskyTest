
using Backend.Scanning;

namespace Backend.Tests.Scanning;

public class ScanTaskQueueTests
{
    [Fact]
    public async Task Enqueue_ThenRead_ReturnsSameId()
    {
        var queue = new ScanTaskQueue();
        var id = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        queue.Enqueue(id);

        await using var enumerator =
            queue.ReadAllAsync(cts.Token).GetAsyncEnumerator();

        var hasItem = await enumerator.MoveNextAsync();

        Assert.True(hasItem);
        Assert.Equal(id, enumerator.Current);
    }

    [Fact]
    public async Task Enqueue_MultipleItems_ReturnsThemInOrder()
    {

        var queue = new ScanTaskQueue();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        queue.Enqueue(firstId);
        queue.Enqueue(secondId);

        await using var enumerator =
            queue.ReadAllAsync(cts.Token).GetAsyncEnumerator();

        var hasFirst = await enumerator.MoveNextAsync();
        var actualFirst = enumerator.Current;

        var hasSecond = await enumerator.MoveNextAsync();
        var actualSecond = enumerator.Current;

        Assert.True(hasFirst);
        Assert.Equal(firstId, actualFirst);

        Assert.True(hasSecond);
        Assert.Equal(secondId, actualSecond);
    }
}
