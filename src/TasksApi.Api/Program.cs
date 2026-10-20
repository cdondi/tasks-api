using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
builder.Services.AddTransient(_ => new MySqlConnection(connectionString));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program { }
