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
                     .WaitFor(cache);

builder.AddNpmApp("chatui", "../chatui")
       .WithNpmPackageInstallation()
       .WithHttpEndpoint(env: "PORT")
       .WithReverseProxy(chatapi.GetEndpoint("http"))
       .WithExternalHttpEndpoints()
       .WithOtlpExporter()
       .WithEnvironment("BROWSER", "none");

builder.Build().Run();

