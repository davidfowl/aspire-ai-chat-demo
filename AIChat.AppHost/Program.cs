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

if (builder.ExecutionContext.IsRunMode)
{
    builder.AddNpmApp("chatui-fe", "../chatui")
           .WithNpmPackageInstallation()
           .WithHttpEndpoint(env: "PORT")
           .WithEnvironment("BACKEND_URL", chatapi.GetEndpoint("http"))
           .WithOtlpExporter()
           .WithEnvironment("BROWSER", "none");
}

// We use YARP as the static file server and reverse proxy. This is used to test
// the application in a containerized environment.
builder.AddYarp("chatui")
       .WithStaticFiles()
       .WithExternalHttpEndpoints()
       .WithDockerfile("../chatui")
       .WithConfiguration(c =>
       {
           c.AddRoute("/api/{**catch-all}", chatapi.GetEndpoint("http"));
       })
       .WithExplicitStart();

builder.Build().Run();

