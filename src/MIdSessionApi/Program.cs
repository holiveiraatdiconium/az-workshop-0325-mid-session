using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Azure;
using Azure.Identity;
using MIdSessionApi.Models; 

var builder = WebApplication.CreateBuilder(args);

// Add support for environment variables and app settings
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", async (IConfiguration configuration) =>
{
    string? connectionString = configuration["AZURE_STORAGE_CONNECTION_STRING"];
    TableServiceClient serviceClient;

    try
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            string? storageAccountUrl = configuration["AZURE_STORAGE_ACCOUNT_URL"];
            if (string.IsNullOrEmpty(storageAccountUrl))
            {
                return Results.Content("<html><body><h1>Error</h1><p>Azure Storage account URL is not configured.</p></body></html>", "text/html");
            }

            serviceClient = new TableServiceClient(new Uri(storageAccountUrl), new DefaultAzureCredential());
        }
        else
        {
            serviceClient = new TableServiceClient(connectionString);
        }

        TableClient tableClient = serviceClient.GetTableClient("Sessions");

        List<SessionResult> sessions = new List<SessionResult>();
        await foreach (TableEntity entity in tableClient.QueryAsync<TableEntity>())
        {
            if (entity["Status"]?.ToString() == "ready")
            {
                sessions.Add(new SessionResult
                {
                    SessionId = entity.RowKey,
                    Timestamp = entity.Timestamp,
                    User = entity["Email"]?.ToString() ?? string.Empty,
                    Checkin = entity.ContainsKey("checkin") ? (DateTime?)entity["checkin"] : null
                });

                if (!entity.ContainsKey("checkin"))
                {
                    entity["checkin"] = DateTime.UtcNow;
                    await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge);
                }
            }
        }

        var orderedSessions = sessions
            .OrderByDescending(s => s.Checkin.HasValue)
            .ThenByDescending(s => s.Checkin)
            .ThenByDescending(s => s.Timestamp);

        string html = "<html><body><h1>Session Results</h1><table border='1'><tr><th>Session ID</th><th>Timestamp</th><th>User</th><th>Checkin</th></tr>";
        foreach (var session in orderedSessions)
        {
            html += $"<tr><td>{session.SessionId}</td><td>{session.Timestamp}</td><td>{session.User}</td><td>{session.Checkin}</td></tr>";
        }
        html += "</table></body></html>";

        return Results.Content(html, "text/html");
    }
    catch (Exception ex)
    {
        string errorHtml = $"<html><body><h1>Error</h1><p>{ex.Message}</p></body></html>";
        return Results.Content(errorHtml, "text/html");
    }
});

app.Run();
