var builder = DistributedApplication.CreateBuilder(args);

// This is the AI model our application will use
var model = builder.AddAIModel("llm")
                   .RunAsOllama("llama3.2:1b", c =>
                   {
                       // Enable to enable GPU support (if your machine has a GPU)
                       //c.WithGPUSupport();
                       c.WithLifetime(ContainerLifetime.Persistent);
                   });
                   // Uncomment to use OpenAI instead in local dev, but requires an OpenAI API key
                   // in Parameters:openaikey section of configuration (use user secrets)
                   //.AsOpenAI("gpt-4o", builder.AddParameter("openaikey", secret: true));
                   // .PublishAsOpenAI("gpt-4o", builder.AddParameter("openaikey", secret: true));
                   // Uncomment to use Azure OpenAI instead in local dev, but requires an Azure OpenAI API key
                   // .PublishAsAzureOpenAI("gpt-4o", "2024-05-13", );

// We use Cosmos DB for our conversation history
var conversations = builder.AddAzureCosmosDB("cosmos")
                           .RunAsPreviewEmulator(e => e.WithDataExplorer().WithDataVolume())
                           .AddCosmosDatabase("db")
                           .AddContainer("conversations", "/id");

var chatapi = builder.AddProject<Projects.ChatApi>("chatapi")
                     .WithReference(model)
                     .WaitFor(model)
                     .WithReference(conversations)
                     .WaitFor(conversations)
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
