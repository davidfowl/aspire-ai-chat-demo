public static class ProxyExtensions
{
    public static IResourceBuilder<NodeAppResource> WithReverseProxy(this IResourceBuilder<NodeAppResource> builder, EndpointReference upstreamEndpoint)
    {
        if (builder.ApplicationBuilder.ExecutionContext.IsRunMode)
        {
            return builder.WithEnvironment("BACKEND_URL", upstreamEndpoint);
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