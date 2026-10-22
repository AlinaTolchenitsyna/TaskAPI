
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RabbitMQ.Client;

namespace TaskAPI.Tests;

public class TaskCompletionIntegrationTests
{
    [Fact]
    public async Task CreateAndCompleteTask_ShouldPublishRabbitMqEvent()
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection =
            await factory.CreateConnectionAsync();

        await using var channel =
            await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: "task.events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);

        var queue = await channel.QueueDeclareAsync(
            queue: "",
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null);

        var queueName = queue.QueueName;

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: "task.events",
            routingKey: "task.completed",
            arguments: null);

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                (_, _, _, _) => true
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:7056")
        };

        var createResponse = await client.PostAsJsonAsync(
            "/tasks",
            new { title = "Интеграционный тест RabbitMQ" });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var createdTask = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var taskId = createdTask.GetProperty("id").GetGuid();

        var completeResponse = await client.PutAsync(
            $"/tasks/{taskId}/complete",
            null);

        Assert.Equal(
            HttpStatusCode.OK,
            completeResponse.StatusCode);

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            var message = await channel.BasicGetAsync(
                queueName,
                autoAck: true);

            if (message is not null)
            {
                using var json = JsonDocument.Parse(
                    message.Body.ToArray());

                var root = json.RootElement;

                if (root.GetProperty("taskId").GetGuid() == taskId)
                {
                    Assert.Equal(
                        "Интеграционный тест RabbitMQ",
                        root.GetProperty("title").GetString());

                    Assert.Equal(
                        "Medium",
                        root.GetProperty("priority").GetString());

                    Assert.True(
                        root.GetProperty("completedAt")
                            .GetDateTimeOffset() > DateTimeOffset.MinValue);

                    return;
                }
            }

            await Task.Delay(200);
        }

        Assert.Fail(
    $"Событие завершения задачи {taskId} не пришло в RabbitMQ.");
    }
}
