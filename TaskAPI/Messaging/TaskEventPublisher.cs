
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using TaskAPI.Models;

namespace TaskAPI.Messaging;

public class TaskEventPublisher : IAsyncDisposable
{
    private readonly ConnectionFactory _factory = new()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "guest",
        Password = "guest"
    };

    private readonly ILogger<TaskEventPublisher> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public TaskEventPublisher(ILogger<TaskEventPublisher> logger)
    {
        _logger = logger;
    }

    public async Task PublishCompletedAsync(TaskItem task)
    {
        await _lock.WaitAsync();

        try
        {
            if (_connection is null || !_connection.IsOpen ||
                _channel is null || !_channel.IsOpen)
            {
                await ResetConnectionAsync();

                _connection = await _factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();
            }

            await _channel.ExchangeDeclareAsync(
                exchange: "task.events",
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null);

            var message = new
            {
                taskId = task.Id,
                title = task.Title,
                completedAt = task.CompletedAt,
                priority = task.Priority.ToString()
            };

            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(message));

            await _channel.BasicPublishAsync(
                exchange: "task.events",
                routingKey: "task.completed",
                body: body);
        }
        catch
        {
            await ResetConnectionAsync();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ResetConnectionAsync()
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ошибка закрытия канала RabbitMQ");
            }

            _channel = null;
        }

        if (_connection is not null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ошибка закрытия соединения RabbitMQ");
            }

            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lock.WaitAsync();

        try
        {
            await ResetConnectionAsync();
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
