var builder = DistributedApplication.CreateBuilder(args);

// Allows aspire publish -p docker-compose to work
builder.AddDockerComposePublisher();

// This is the AI model our application will use
var model = builder.AddAIModel("llm")
                   .RunAsOllama("phi4", c =>
                   {
                       // Enable to enable GPU support (if your machine has a GPU)
                       if (!OperatingSystem.IsMacOS())
                       {
                           c.WithGPUSupport();
                       }
                       c.WithLifetime(ContainerLifetime.Persistent);
                   })
                   .PublishAsOpenAI("gpt-4o", b => b.AddParameter("openaikey", secret: true));

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
       .WithOtlpExporter();

builder.Build().Run();

