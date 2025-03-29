public static class ModelExtensions
{
    public static IResourceBuilder<AIModel> AddAIModel(this IDistributedApplicationBuilder builder, string name)
    {
        var model = new AIModel(name);
        return builder.CreateResourceBuilder(model);
    }

    public static IResourceBuilder<AIModel> RunAsOllama(this IResourceBuilder<AIModel> builder, string model, Action<IResourceBuilder<OllamaResource>>? configure = null)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            builder.Reset();

            var ollama = builder.ApplicationBuilder.AddOllama("ollama")
                .WithDataVolume();

            configure?.Invoke(ollama);

            var ollamaModel = ollama.AddModel(builder.Resource.Name, model);

            builder.Resource.UnderlyingResource = ollamaModel.Resource;
            builder.Resource.ConnectionString = ReferenceExpression.Create($"{ollamaModel};Provider=Ollama");
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> RunAsOpenAI(this IResourceBuilder<AIModel> builder, string modelName, Func<IDistributedApplicationBuilder, IResourceBuilder<ParameterResource>> addApiKey)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder.AsOpenAI(modelName, addApiKey(builder.ApplicationBuilder));
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> PublishAsOpenAI(this IResourceBuilder<AIModel> builder, string modelName, Func<IDistributedApplicationBuilder, IResourceBuilder<ParameterResource>> addApiKey)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder.AsOpenAI(modelName, addApiKey(builder.ApplicationBuilder));
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> AsAzureOpenAI(this IResourceBuilder<AIModel> builder, string modelName, Action<IResourceBuilder<AzureOpenAIResource>>? configure)
    {
        builder.Reset();

        var openAIModel = builder.ApplicationBuilder.AddAzureOpenAI(builder.Resource.Name);

        configure?.Invoke(openAIModel);

        builder.Resource.UnderlyingResource = openAIModel.Resource;
        // Add the model name to the connection string
        builder.Resource.ConnectionString = ReferenceExpression.Create($"{openAIModel};Model={modelName};AzureOpenAI");

        return builder;
    }

    public static IResourceBuilder<AIModel> RunAsAzureOpenAI(this IResourceBuilder<AIModel> builder, string modelName, Action<IResourceBuilder<AzureOpenAIResource>>? configure)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder.AsAzureOpenAI(modelName, configure);
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> PublishAsAzureOpenAI(this IResourceBuilder<AIModel> builder, string modelName, Action<IResourceBuilder<AzureOpenAIResource>>? configure)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder.AsAzureOpenAI(modelName, configure);
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> RunAsAzureAIInference(this IResourceBuilder<AIModel> builder, string modelName, string endpoint, IResourceBuilder<ParameterResource> apiKey)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder.AsAzureAIInference(modelName, endpoint, apiKey);
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> PublishAsAzureAIInference(this IResourceBuilder<AIModel> builder, string modelName, string endpoint, IResourceBuilder<ParameterResource> apiKey)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder.AsAzureAIInference(modelName, endpoint, apiKey);
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> AsAzureAIInference(this IResourceBuilder<AIModel> builder, string modelName, string endpoint, IResourceBuilder<ParameterResource> apiKey)
    {
        builder.Reset();

        var cs = builder.ApplicationBuilder.AddConnectionString(builder.Resource.Name, csb =>
        {
            csb.Append($"Endpoint={endpoint};");
            csb.Append($"AccessKey={apiKey};");
            csb.Append($"Model={modelName};");
            csb.AppendLiteral("Provider=AzureAIInference``");
        });

        builder.Resource.UnderlyingResource = cs.Resource;
        builder.Resource.ConnectionString = cs.Resource.ConnectionStringExpression;

        return builder;
    }

    public static IResourceBuilder<AIModel> AsOpenAI(this IResourceBuilder<AIModel> builder, string modelName, Func<IDistributedApplicationBuilder, IResourceBuilder<ParameterResource>> addApiKey)
    {
        return builder.AsOpenAI(modelName, addApiKey(builder.ApplicationBuilder));
    }

    public static IResourceBuilder<AIModel> AsOpenAI(this IResourceBuilder<AIModel> builder, string modelName, IResourceBuilder<ParameterResource> apiKey)
    {
        builder.Reset();

        var cs = builder.ApplicationBuilder.AddConnectionString(builder.Resource.Name, csb =>
        {
            csb.Append($"AccessKey={apiKey};");
            csb.Append($"Model={modelName};");
            csb.AppendLiteral("Provider=OpenAI");
        });

        builder.Resource.UnderlyingResource = cs.Resource;
        builder.Resource.ConnectionString = cs.Resource.ConnectionStringExpression;

        return builder;
    }

    private static void Reset(this IResourceBuilder<AIModel> builder)
    {
        // Reset the properties of the AIModel resource
        if (builder.Resource.UnderlyingResource is { } underlyingResource)
        {
            builder.ApplicationBuilder.Resources.Remove(underlyingResource);

            if (underlyingResource is IResourceWithParent resourceWithParent)
            {
                builder.ApplicationBuilder.Resources.Remove(resourceWithParent.Parent);
            }
        }

        builder.Resource.ConnectionString = null;
    }
}

// A resource representing an AI model.
public class AIModel(string name) : Resource(name), IResourceWithConnectionString, IResourceWithoutLifetime
{
    internal IResourceWithConnectionString? UnderlyingResource { get; set; }
    internal ReferenceExpression? ConnectionString { get; set; }

    public ReferenceExpression ConnectionStringExpression =>
        ConnectionString ?? throw new InvalidOperationException("No connection string available.");
}

