
using Microsoft.Extensions.Hosting;
using TaskAPI.Models;

namespace TaskAPI.Messaging;

public class TaskEventWorker : BackgroundService
{
    private readonly TaskEventQueue _queue;
    private readonly TaskEventPublisher _publisher;
    private readonly ILogger<TaskEventWorker> _logger;

    public TaskEventWorker(
        TaskEventQueue queue,
        TaskEventPublisher publisher,
        ILogger<TaskEventWorker> logger)
    {
        _queue = queue;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        await foreach (var taskEvent in
            _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var task = new TaskItem
                {
                    Id = taskEvent.TaskId,
                    Title = taskEvent.Title,
                    CompletedAt = taskEvent.CompletedAt,
                    Priority = Enum.Parse<Priority>(
                        taskEvent.Priority)
                };

                await _publisher.PublishCompletedAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Не удалось опубликовать событие завершения задачи {TaskId}",
                    taskEvent.TaskId);
            }
        }
    }
}
