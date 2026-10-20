using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using TasksApi.Api;

namespace TasksApi.Tests;

// Runs against the local tasks-mysql container. Each test class instance creates its own
// users and deletes them afterwards; ON DELETE CASCADE removes their tasks.
[Collection("Database")]
public class TaskEndpointsTests(TasksApiFactory factory) : IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly List<int> _createdUserIds = [];
    private int _userId;

    public async Task InitializeAsync() => _userId = await CreateUserAsync();

    public async Task DisposeAsync()
    {
        await using var db = OpenConnection();
        await db.ExecuteAsync("DELETE FROM users WHERE id IN @ids", new { ids = _createdUserIds });
    }

    [Fact]
    public async Task Post_ValidTask_Returns201AndTask()
    {
        var response = await _client.PostAsJsonAsync("/tasks",
            new { userId = _userId, title = "Write tests", dueDate = "2026-11-01" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.NotNull(task);
        Assert.Equal($"/tasks/{task.Id}", response.Headers.Location?.ToString());
        Assert.Equal(_userId, task.UserId);
        Assert.Equal("Write tests", task.Title);
        Assert.Equal("todo", task.Status);
        Assert.Equal(new DateOnly(2026, 11, 1), task.DueDate);
    }

    [Fact]
    public async Task Post_WithoutDueDate_Returns201WithNullDueDate()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { userId = _userId, title = "No deadline" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>();
        Assert.Null(task!.DueDate);
    }

    [Fact]
    public async Task Post_NonexistentUserId_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { userId = int.MaxValue, title = "Orphan" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("does not exist", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Post_MissingTitle_Returns400(string? title)
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { userId = _userId, title });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_TitleTooLong_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { userId = _userId, title = new string('a', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsOnlyThatUsersTasks()
    {
        var otherUserId = await CreateUserAsync();
        await PostTaskAsync(_userId, "First");
        await PostTaskAsync(_userId, "Second");
        await PostTaskAsync(otherUserId, "Someone else's");

        var tasks = await _client.GetFromJsonAsync<List<TaskResponse>>($"/tasks?userId={_userId}");

        Assert.NotNull(tasks);
        Assert.Equal(["First", "Second"], tasks.Select(t => t.Title));
        Assert.All(tasks, t => Assert.Equal(_userId, t.UserId));
    }

    [Fact]
    public async Task Get_UserWithNoTasks_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync($"/tasks?userId={_userId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<TaskResponse>>())!);
    }

    [Fact]
    public async Task Get_MissingUserId_Returns400()
    {
        var response = await _client.GetAsync("/tasks");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task PostTaskAsync(int userId, string title)
    {
        var response = await _client.PostAsJsonAsync("/tasks", new { userId, title });
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CreateUserAsync()
    {
        await using var db = OpenConnection();
        var id = await db.ExecuteScalarAsync<int>(
            "INSERT INTO users (email, name) VALUES (@email, @name); SELECT LAST_INSERT_ID();",
            new { email = $"test-{Guid.NewGuid()}@example.com", name = "Test User" });
        _createdUserIds.Add(id);
        return id;
    }

    private MySqlConnection OpenConnection() =>
        new(factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Default"));
}
