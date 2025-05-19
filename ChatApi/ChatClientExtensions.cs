using Microsoft.Extensions.AI;

public static class ChatClientExtensions
{
    public static IHostApplicationBuilder AddChatClient(this IHostApplicationBuilder builder, string connectionName)
    {
        var cs = builder.Configuration.GetConnectionString(connectionName);

        if (!ChatClientConnectionInfo.TryParse(cs, out var connectionInfo))
        {
            throw new InvalidOperationException($"Invalid connection string: {cs}. Expected format: 'Endpoint=endpoint;AccessKey=your_access_key;Model=model_name;Provider=ollama/openai/azureopenai;'.");
        }

        var chatClientBuilder = connectionInfo.Provider switch
        {
            ClientChatProvider.Ollama => builder.AddOllamaClient(connectionName, connectionInfo),
            ClientChatProvider.OpenAI => builder.AddOpenAIClient(connectionName, connectionInfo),
            _ => throw new NotSupportedException($"Unsupported provider: {connectionInfo.Provider}")
        };

        // Add OpenTelemetry tracing for the ChatClient activity source
        chatClientBuilder.UseOpenTelemetry().UseLogging();

        // This is the default name of the trace source and meter
        var telemetryName = "Experimental.Microsoft.Extensions.AI";

        builder.Services.AddOpenTelemetry()
               .WithTracing(t => t.AddSource(telemetryName))
               .WithMetrics(m => m.AddMeter(telemetryName));

        return builder;
    }

    private static ChatClientBuilder AddOpenAIClient(this IHostApplicationBuilder builder, string connectionName, ChatClientConnectionInfo connectionInfo)
    {
        return builder.AddOpenAIClient(connectionName, settings =>
        {
            settings.Endpoint = connectionInfo.Endpoint;
            settings.Key = connectionInfo.AccessKey;
        })
        .AddChatClient(connectionInfo.SelectedModel);
    }

    private static ChatClientBuilder AddOllamaClient(this IHostApplicationBuilder builder, string connectionName, ChatClientConnectionInfo connectionInfo)
    {
        return builder.AddOllamaApiClient(connectionName, settings =>
        {
            settings.SelectedModel = connectionInfo.SelectedModel;
        })
        .AddChatClient();
    }
}
