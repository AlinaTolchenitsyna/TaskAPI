namespace TaskAPI.Messaging
{
    public record TaskCompletedEvent(
    Guid TaskId,
    string Title,
    DateTimeOffset CompletedAt,
    string Priority);
}
