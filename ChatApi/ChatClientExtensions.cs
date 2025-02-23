using Azure.AI.Inference;
using Azure;
using Microsoft.Extensions.AI;
using OllamaSharp;

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
            ClientChatProvider.AzureOpenAI => builder.AddAzureOpenAIClient(connectionName).AddChatClient(connectionInfo.SelectedModel),
            ClientChatProvider.AzureAIInference => builder.AddAzureInferenceClient(connectionName, connectionInfo),
            _ => throw new NotSupportedException($"Unsupported provider: {connectionInfo.Provider}")
        };

        // Add OpenTelemetry tracing for the ChatClient activity source
        chatClientBuilder.UseOpenTelemetry().UseLogging();

        builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Experimental.Microsoft.Extensions.AI"));

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

    private static ChatClientBuilder AddAzureInferenceClient(this IHostApplicationBuilder builder, string connectionName, ChatClientConnectionInfo connectionInfo)
    {
        return builder.Services.AddChatClient(sp =>
        {
            var credential = new AzureKeyCredential(connectionInfo.AccessKey!);

            var client = new ChatCompletionsClient(connectionInfo.Endpoint, credential, new AzureAIInferenceClientOptions());

            return client.AsChatClient(connectionInfo.SelectedModel);
        });
    }

    private static ChatClientBuilder AddOllamaClient(this IHostApplicationBuilder builder, string connectionName, ChatClientConnectionInfo connectionInfo)
    {
        var httpKey = $"{connectionName}_http";

        builder.Services.AddHttpClient(httpKey, c =>
        {
            c.BaseAddress = connectionInfo.Endpoint;
        });

        return builder.Services.AddChatClient(sp =>
        {
            // Create a client for the Ollama API using the http client factory
            var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(httpKey);

            return new OllamaApiClient(client, connectionInfo.SelectedModel);
        });
    }
}
