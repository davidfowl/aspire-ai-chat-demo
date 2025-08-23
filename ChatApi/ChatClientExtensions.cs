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

        _ = connectionInfo.Provider switch
        {
            ClientChatProvider.Ollama => builder.AddOllamaClient(connectionName, connectionInfo),
            ClientChatProvider.OpenAI => builder.AddOpenAIClient(connectionName, connectionInfo),
            _ => throw new NotSupportedException($"Unsupported provider: {connectionInfo.Provider}")
        };
        
        return builder;
    }

    private static ChatClientBuilder AddOpenAIClient(this IHostApplicationBuilder builder, string connectionName, ChatClientConnectionInfo connectionInfo)
    {
        return builder.AddOpenAIClient(connectionName).AddChatClient();
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
