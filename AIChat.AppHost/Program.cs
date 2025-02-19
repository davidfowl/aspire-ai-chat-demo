using Azure.Provisioning.CognitiveServices;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc.ModelBinding;

var builder = DistributedApplication.CreateBuilder(args);

// This is the AI model our application will use
//var model = builder.AddAIModel("llm")
//.RunAsOllama("phi4", c =>
//{
//    // Enable to enable GPU support (if your machine has a GPU)
//    c.WithGPUSupport();
//    c.WithLifetime(ContainerLifetime.Persistent);
//});
// Uncomment to use OpenAI instead in local dev, but requires an OpenAI API key
// in Parameters:openaikey section of configuration (use user secrets)
//.AsOpenAI("gpt-4o", builder.AddParameter("openaikey", secret: true));
// .PublishAsOpenAI("gpt-4o", builder.AddParameter("openaikey", secret: true));
// Uncomment to use Azure OpenAI instead in local dev, but requires an Azure OpenAI API key
//                   .PublishAsAzureOpenAI("gpt-4o", "2024-05-13");

var azureAIName = builder.AddParameter("azureai-name");
var azureAIRG = builder.AddParameter("azureai-rg");
var modelName = "Phi-4";
var azureAI = builder.AddAzureOpenAI("azureai")
    .AsExisting(azureAIName, azureAIRG)
    .AddDeployment(new AzureOpenAIDeployment(modelName, modelName, "3"))
    .ConfigureInfrastructure(infra =>
    {
        var deployment = infra.GetProvisionableResources().OfType<CognitiveServicesAccountDeployment>().First(d => d.BicepIdentifier == "Phi_4");
        deployment.Sku.Name = "GlobalStandard";
        deployment.Sku.Capacity = 1;
        deployment.Properties.Model.Format = "Microsoft";
    });

var csb = new ReferenceExpressionBuilder();
csb.Append($"{azureAI.Resource.ConnectionStringExpression}");
csb.Append($";Model={modelName}");
var cs = csb.Build();

var model = builder.CreateResourceBuilder(new AIModel("llm")
{
    UnderlyingResource = azureAI.Resource,
    ConnectionString = cs,
    Provider = "AzureInference"
});

// We use Cosmos DB for our conversation history
var conversations = builder.AddAzureCosmosDB("cosmos")
                           .RunAsPreviewEmulator(e => e.WithDataExplorer().WithDataVolume())
                           .AddCosmosDatabase("db")
                           .AddContainer("conversations", "/id");

var chatapi = builder.AddProject<Projects.ChatApi>("chatapi")
                     .WithReference(model)
                     //.WaitFor(model)
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
