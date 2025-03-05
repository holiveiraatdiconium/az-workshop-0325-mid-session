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

app.MapGet("/", async (HttpContext context, IConfiguration configuration) =>
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
                string errorHtml = @"
                <html>
                    <body>
                        <h1>Error</h1>
                        <p>Azure Storage account URL is not configured.</p>
                        <ul>
                            <li>Ensure this app is added to the storage account at <a href='https://portal.azure.com/#@mydiconium.onmicrosoft.com/resource/subscriptions/48ee300d-8738-496a-9366-1271ebefc1e6/resourceGroups/RG-pt-azure-workshop/providers/Microsoft.Storage/storageAccounts/diconiumptazureworkshop/overview'>this URL</a> using role assignment and managed identity.</li>
                            <li>Activate Identity on the web app.</li>
                            <li>Add the role 'Storage Table Data Contributor'.</li>
                            <li>Set the environment variable <code>AZURE_STORAGE_ACCOUNT_URL</code> to <code>https://diconiumptazureworkshop.table.core.windows.net/Sessions</code>.</li>
                        </ul>
                    </body>
                </html>";
                return Results.Content(errorHtml, "text/html");
            }

            serviceClient = new TableServiceClient(new Uri(storageAccountUrl), new DefaultAzureCredential());
        }
        else
        {
            serviceClient = new TableServiceClient(connectionString);
        }

        TableClient tableClient = serviceClient.GetTableClient("Sessions");

        string sessionId;
        if (context.Request.Host.Host.Contains("localhost") && context.Request.Query.ContainsKey("sessionid"))
        {
            sessionId = context.Request.Query["sessionid"];
        }
        else
        {
            var hostParts = context.Request.Host.Host.Split('.');
            sessionId = hostParts[0].Split('-').Last();
        }

        List<SessionResult> sessions = new List<SessionResult>();
        await foreach (TableEntity entity in tableClient.QueryAsync<TableEntity>(e => e.RowKey == sessionId))
        {
            if (entity["Status"]?.ToString() == "ready")
            {
                sessions.Add(new SessionResult
                {
                    SessionId = entity.RowKey,
                    Timestamp = entity.Timestamp ?? DateTimeOffset.UtcNow,
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
