using Aspire.Hosting.Pipelines;
using CliWrap;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

// Publish this as a Docker Compose application
builder.AddDockerComposeEnvironment("env")
       .WithDashboard(db => db.WithHostPort(8085))
       .ConfigureComposeFile(file =>
       {
           file.Name = "aspire-ai-chat";
       });

// This is the AI model our application will use
var model = builder.AddAIModel("llm");

if (OperatingSystem.IsMacOS())
{
    // Just use OpenAI on MacOS, running ollama does not work well via docker
    // see https://github.com/CommunityToolkit/Aspire/issues/608
    model.AsOpenAI("gpt-4.1");
}
else
{
    model.RunAsOllama("phi4", c =>
    {
        // Enable to enable GPU support (if your machine has a GPU)
        c.WithGPUSupport();
        c.WithLifetime(ContainerLifetime.Persistent);
    })
    .PublishAsOpenAI("gpt-4.1");
}

// We use Postgres for our conversation history
var db = builder.AddPostgres("pg")
                .WithDataVolume(builder.ExecutionContext.IsPublishMode ? "pgvolume" : null)
                .WithPgAdmin()
                .AddDatabase("conversations");

// Redis is used to store and broadcast the live message stream
// so that multiple clients can connect to the same conversation.
var cache = builder.AddRedis("cache")
                   .WithRedisInsight();

var chatapi = builder.AddProject<Projects.ChatApi>("chatapi")
                     .WithReference(model)
                     .WaitFor(model)
                     .WithReference(db)
                     .WaitFor(db)
                     .WithReference(cache)
                     .WaitFor(cache)
                     .WithUrls(context =>
                     {
                         foreach (var u in context.Urls)
                         {
                             u.DisplayLocation = UrlDisplayLocation.DetailsOnly;
                         }

                         context.Urls.Add(new()
                         {
                             Url = "/scalar",
                             DisplayText = "API Reference",
                             Endpoint = context.GetEndpoint("https")
                         });
                     });

var frontend = builder.AddViteApp("chatuife", "../chatui")
                      .WithReference(chatapi)
                      .WithEnvironment("BROWSER", "none")
                      .WithUrl("", "Chat UI");

// We use YARP as the static file server and reverse proxy.
var yarp =builder.AddYarp("chatui")
       .WithExternalHttpEndpoints()
       .PublishWithStaticFiles(frontend)
       .WithConfiguration(c =>
       {
           c.AddRoute("/api/{**catch-all}", chatapi);
       })
       .WithExplicitStart();

// Add a push to GitHub Container Registry step
// that will be executed from the pipeline
builder.Pipeline.AddStep("push-gh", async context =>
{
    // Get configuration values
    var ghcrRepo = builder.Configuration["GHCR_REPO"] ?? throw new InvalidOperationException("GHCR_REPO environment variable is required");
    var tagSuffix = builder.Configuration["TAG_SUFFIX"] ?? "latest";
    
    var resourcesToPublish = new (IResource resource, string imageName)[] 
    { 
        (chatapi.Resource, "chatapi"),
        (yarp.Resource, "chatui")
    };

    foreach (var (resource, imageName) in resourcesToPublish)
    {
        // For project resources, use hardcoded "latest" tag
        var localImageName = resource is ProjectResource ? $"{imageName}:latest" : null;
        
        if (localImageName is null && !resource.TryGetContainerImageName(out localImageName))
        {
            context.Logger.LogWarning("{ImageName} image name not found, skipping", imageName);
            continue;
        }

        var remoteTag = $"{ghcrRepo}/{imageName}:{tagSuffix}";
        
        context.Logger.LogInformation("Tagging {LocalImage} as {RemoteTag}", localImageName, remoteTag);
        
        // Tag the image
        await Cli.Wrap("docker")
            .WithArguments(["tag", localImageName, remoteTag])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => context.Logger.LogDebug("{Output}", line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => context.Logger.LogError("{Error}", line)))
            .ExecuteAsync();
        
        context.Logger.LogInformation("Pushing {RemoteTag}", remoteTag);
        
        // Push the image
        await Cli.Wrap("docker")
            .WithArguments(["push", remoteTag])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => context.Logger.LogDebug("{Output}", line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => context.Logger.LogError("{Error}", line)))
            .ExecuteAsync();
    }
}, 
dependsOn: WellKnownPipelineSteps.Build);

builder.Build().Run();
