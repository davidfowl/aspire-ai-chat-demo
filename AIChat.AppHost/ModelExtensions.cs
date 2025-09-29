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

    public static IResourceBuilder<AIModel> RunAsOpenAI(this IResourceBuilder<AIModel> builder, string modelName)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder.AsOpenAI(modelName);
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> PublishAsOpenAI(this IResourceBuilder<AIModel> builder, string modelName)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsPublishMode)
        {
            return builder.AsOpenAI(modelName);
        }

        return builder;
    }

    public static IResourceBuilder<AIModel> AsOpenAI(this IResourceBuilder<AIModel> builder, string modelName)
    {
        builder.Reset();

        var oai = builder.ApplicationBuilder.AddOpenAI("oai");

        var model = oai.AddModel(builder.Resource.Name, modelName);

        builder.Resource.UnderlyingResource = model.Resource;
        builder.Resource.ConnectionString = ReferenceExpression.Create($"{model};Provider=OpenAI");

        return builder;
    }

    private static void Reset(this IResourceBuilder<AIModel> builder)
    {
        IResource? GetParentResource(IResource resource)
        {
            if (resource is IResourceWithParent parentResource)
            {
                return parentResource.Parent;
            }

            if (resource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var relationshipAnnotations))
            {
                return relationshipAnnotations.FirstOrDefault(r => r.Type == "Parent")?.Resource;
            }

            return null;
        }

        // Reset the properties of the AIModel resource
        IResource? underlyingResource = builder.Resource.UnderlyingResource;

        if (underlyingResource is not null)
        {
            builder.ApplicationBuilder.Resources.Remove(underlyingResource);

            while (GetParentResource(underlyingResource) is IResource parent)
            {
                builder.ApplicationBuilder.Resources.Remove(parent);

                underlyingResource = parent;
            }
        }

        builder.Resource.ConnectionString = null;
    }
}

// A resource representing an AI model.
public class AIModel(string name) : Resource(name), IResourceWithConnectionString, IResourceWithoutLifetime
{
    // For tracking
    internal IResource? UnderlyingResource { get; set; }
    internal ReferenceExpression? ConnectionString { get; set; }

    public ReferenceExpression ConnectionStringExpression =>
        ConnectionString ?? throw new InvalidOperationException("No connection string available.");
}


