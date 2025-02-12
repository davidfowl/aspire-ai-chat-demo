var builder = DistributedApplication.CreateBuilder(args);

var model = builder.ExecutionContext.IsPublishMode
? builder.AddModel("llm")
         .WithEndpoint(builder.AddParameter("modelep"))
         .WithModelName("Phi-4")
         .WithAccessKey(builder.AddParameter("modelkey", secret: true))
         .AsConnectionString()

: builder.AddOllama("ollama")
         .WithGPUSupport()
         .WithDataVolume()
         .WithLifetime(ContainerLifetime.Persistent)
         .AddModel("llm", "phi4");

var db = builder.AddAzureCosmosDB("cosmos")
                .RunAsPreviewEmulator(e => e.WithDataExplorer().WithDataVolume())
                .AddCosmosDatabase("chats");

var chatapi = builder.AddProject<Projects.ChatApi>("chatapi")
       .WithReference(model)
       .WaitFor(model)
    // .WithReference(db)
    // .WaitFor(db)
       .PublishAsAzureContainerApp((infra, app) =>
        {
            app.Configuration.Ingress.AllowInsecure = true;
        });

builder.AddDockerfile("chatui", "../chatui")
       .WithHttpEndpoint(targetPort: 80, env: "PORT")
       .WithEnvironment(c =>
        {
            var be = chatapi.GetEndpoint("http");

            // In the docker file, caddy uses the host and port without the scheme
            var hostAndPort = ReferenceExpression.Create($"{be.Property(EndpointProperty.Host)}:{be.Property(EndpointProperty.Port)}");

            c.EnvironmentVariables["BACKEND_URL"] = hostAndPort;
            c.EnvironmentVariables["SPAN"] = "chatui";
        })
        .WithExternalHttpEndpoints()
        .WithOtlpExporter();

builder.Build().Run();
