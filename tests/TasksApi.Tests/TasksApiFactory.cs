using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TasksApi.Tests;

public class TasksApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Development");
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<TasksApiFactory>;
