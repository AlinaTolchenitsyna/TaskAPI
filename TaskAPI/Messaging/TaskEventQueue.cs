
using System.Threading.Channels;

namespace TaskAPI.Messaging;

public class TaskEventQueue
{
    private readonly Channel<TaskCompletedEvent> _queue =
        Channel.CreateUnbounded<TaskCompletedEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

    public ValueTask EnqueueAsync(TaskCompletedEvent taskEvent)
    {
        return _queue.Writer.WriteAsync(taskEvent);
    }

    public IAsyncEnumerable<TaskCompletedEvent> ReadAllAsync(
        CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }
}
