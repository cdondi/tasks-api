using Dapper;
using MySqlConnector;

namespace TasksApi.Api;

public record CreateTaskRequest(int UserId, string? Title, DateOnly? DueDate);

public record TaskResponse(int Id, int UserId, string Title, string Status, DateOnly? DueDate, DateTime CreatedAt);

// Dapper maps rows onto settable properties (and converts the unsigned id columns to int).
internal class TaskRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public TaskResponse ToResponse() =>
        new(Id, UserId, Title, Status, DueDate is { } d ? DateOnly.FromDateTime(d) : null, CreatedAt);
}

public static class TaskEndpoints
{
    private const int MaxTitleLength = 200;

    private const string SelectColumns =
        "SELECT id, user_id AS UserId, title, status, due_date AS DueDate, created_at AS CreatedAt FROM tasks";

    public static void MapTaskEndpoints(this WebApplication app)
    {
        app.MapGet("/tasks", async (int userId, MySqlConnection db) =>
        {
            var rows = await db.QueryAsync<TaskRow>(
                $"{SelectColumns} WHERE user_id = @userId ORDER BY id", new { userId });
            return Results.Ok(rows.Select(r => r.ToResponse()));
        });

        app.MapPost("/tasks", async (CreateTaskRequest request, MySqlConnection db) =>
        {
            var errors = Validate(request);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            int id;
            try
            {
                id = await db.ExecuteScalarAsync<int>(
                    "INSERT INTO tasks (user_id, title, due_date) VALUES (@UserId, @Title, @DueDate); SELECT LAST_INSERT_ID();",
                    new
                    {
                        request.UserId,
                        Title = request.Title!.Trim(),
                        DueDate = request.DueDate?.ToDateTime(TimeOnly.MinValue),
                    });
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoReferencedRow2)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["userId"] = [$"User {request.UserId} does not exist."],
                });
            }

            var row = await db.QuerySingleAsync<TaskRow>($"{SelectColumns} WHERE id = @id", new { id });
            return Results.Created($"/tasks/{id}", row.ToResponse());
        });
    }

    private static Dictionary<string, string[]> Validate(CreateTaskRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.UserId <= 0)
            errors["userId"] = ["userId must be a positive integer."];
        if (string.IsNullOrWhiteSpace(request.Title))
            errors["title"] = ["title is required."];
        else if (request.Title.Trim().Length > MaxTitleLength)
            errors["title"] = [$"title must be at most {MaxTitleLength} characters."];
        return errors;
    }
}
