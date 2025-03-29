using Aspire.Hosting.Azure;
using Azure.Provisioning.AppContainers;

public static class ProxyExtensions
{
    public static IResourceBuilder<NodeAppResource> WithReverseProxy(this IResourceBuilder<NodeAppResource> builder, EndpointReference upstreamEndpoint)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder.WithEnvironment("BACKEND_URL", upstreamEndpoint);
        }

        if (upstreamEndpoint.Scheme == "http")
        {
            // Configure the target resource to allow http when deploying to Azure
            // container apps
            static void AllowHttp(AzureResourceInfrastructure infra, ContainerApp app)
            {
                app.Configuration.Ingress.AllowInsecure = true;
            }
            
            if (upstreamEndpoint.Resource is ProjectResource p)
            {
                builder.ApplicationBuilder.CreateResourceBuilder(p)
                    .PublishAsAzureContainerApp(AllowHttp);
            }
            else if (upstreamEndpoint.Resource is ContainerResource c)
            {
                builder.ApplicationBuilder.CreateResourceBuilder(c)
                    .PublishAsAzureContainerApp(AllowHttp);
            }
        }

        return builder.PublishAsDockerFile(c => c.WithReverseProxy(upstreamEndpoint));
    }

    public static IResourceBuilder<ContainerResource> WithReverseProxy(this IResourceBuilder<ContainerResource> builder, EndpointReference upstreamEndpoint)
    {
        // Caddy listens on port 80
        builder.WithEndpoint("http", e => e.TargetPort = 80);

        return builder.WithEnvironment(context =>
        {
            // In the docker file, caddy uses the host and port without the scheme
            context.EnvironmentVariables["BACKEND_URL"] = upstreamEndpoint.Property(EndpointProperty.HostAndPort);
            context.EnvironmentVariables["SPAN"] = builder.Resource.Name;
        });
    }
}