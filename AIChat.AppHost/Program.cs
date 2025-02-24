var builder = DistributedApplication.CreateBuilder(args);

// This is the AI model our application will use
var model = builder.AddAIModel("llm")
                   .RunAsOllama("phi4", c =>
                   {
                       // Enable to enable GPU support (if your machine has a GPU)
                       c.WithGPUSupport();
                       c.WithLifetime(ContainerLifetime.Persistent);
                   });
                   // Uncomment to use OpenAI instead in local dev, but requires an OpenAI API key
                   // in Parameters:openaikey section of configuration (use user secrets)
                   //.AsOpenAI("gpt-4o", builder.AddParameter("openaikey", secret: true));
                   // .PublishAsOpenAI("gpt-4o", builder.AddParameter("openaikey", secret: true));
                   // Uncomment to use Azure AI Inference instead in local dev, but requires an Azure AI Inference API key
                   //.RunAsAzureAIInference("DeepSeek-R1", "", builder.AddParameter("azureaiinferencekey", secret: true));
                   //.PublishAsAzureAIInference("DeepSeek-R1", "", builder.AddParameter("azureaiinferencekey", secret: true));
                   // Uncomment to use Azure OpenAI instead in local dev, but requires an Azure OpenAI API key
                   //.PublishAsAzureOpenAI("gpt-4o", b =>
                   //{
                   //    b.AddDeployment(new AzureOpenAIDeployment("gpt-4o", "gpt-4o", "2024-05-13"));
                   //});

// We use Cosmos DB for our conversation history
var conversations = builder.AddAzureCosmosDB("cosmos")
                           .RunAsPreviewEmulator(e => e.WithDataExplorer().WithDataVolume())
                           .AddCosmosDatabase("db")
                           .AddContainer("conversations", "/id");

// Redis is used to store and broadcast the live message stream
// so that multiple clients can connect to the same conversation.
var cache = builder.AddRedis("cache")
                   .WithRedisInsight();

var chatapi = builder.AddProject<Projects.ChatApi>("chatapi")
                     .WithReference(model)
                     .WaitFor(model)
                     .WithReference(conversations)
                     .WaitFor(conversations)
                     .WithReference(cache)
                     .WaitFor(cache)
                     .PublishAsAzureContainerApp((infra, app) =>
                      {
                          app.Configuration.Ingress.AllowInsecure = true;
                      });

builder.AddNpmApp("chatui", "../chatui")
       .WithNpmPackageInstallation()
       .WithHttpEndpoint(env: "PORT")
       .WithReverseProxy(chatapi.GetEndpoint("http"))
       .WithExternalHttpEndpoints()
       .WithOtlpExporter();

builder.Build().Run();
