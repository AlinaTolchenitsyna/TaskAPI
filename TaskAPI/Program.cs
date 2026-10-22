using Microsoft.EntityFrameworkCore;
using TaskAPI.Data;
using TaskAPI.Messaging;
using TaskAPI.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<TaskEventPublisher>();
builder.Services.AddSingleton<TaskEventQueue>();
builder.Services.AddHostedService<TaskEventWorker>();


builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/tasks", async (TaskDbContext db) =>
{
    var tasks = await db.Tasks.ToListAsync();
    return Results.Ok(tasks);
});

app.MapPost("/tasks", async (TaskItem task, TaskDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length > 200)
    {
        return Results.BadRequest("Название должно содержать от 1 до 200 символов.");
    }

    task.Id = Guid.NewGuid();
    task.CreatedAt = DateTimeOffset.UtcNow;
    task.IsCompleted = false;
    task.CompletedAt = null;
    task.Priority = Enum.IsDefined(task.Priority)
        ? task.Priority
        : Priority.Medium;

    db.Tasks.Add(task);
    await db.SaveChangesAsync();

    return Results.Created($"/tasks/{task.Id}", task);
});



app.MapPut("/tasks/{id:guid}/complete", async (
    Guid id,
    TaskDbContext db,
    TaskEventQueue eventQueue,
    ILogger<Program> logger) =>
{
    var task = await db.Tasks.FindAsync(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    if (task.IsCompleted)
    {
        return Results.Conflict("Задача уже завершена.");
    }

    task.IsCompleted = true;
    task.CompletedAt = DateTimeOffset.UtcNow;

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(
            "Задача уже была завершена другим запросом.");
    }

    var taskEvent = new TaskCompletedEvent(
        task.Id,
        task.Title,
        task.CompletedAt.Value,
        task.Priority.ToString());

    try
    {
        await eventQueue.EnqueueAsync(taskEvent);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Не удалось поставить событие завершения задачи {TaskId} в очередь",
            task.Id);
    }

    return Results.Ok(task);
});



app.MapDelete("/tasks/{id:guid}", async (Guid id, TaskDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);

    if (task is null)
    {
        return Results.NotFound();
    }

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();
