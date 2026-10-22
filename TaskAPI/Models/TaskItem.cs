namespace TaskAPI.Models
{
    public enum Priority
    {
        Low,
        Medium,
        High
    }

    public class TaskItem
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Priority Priority { get; set; } = Priority.Medium;
    }
}
