public static class DashboardExtensions
{
    public static void AddDashboard(this IDistributedApplicationBuilder builder)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            // The name aspire-dashboard is special cased and excluded from the default
            var dashboard = builder.AddContainer("dashboard", "mcr.microsoft.com/dotnet/nightly/aspire-dashboard")
                   .WithHttpEndpoint(targetPort: 18888)
                   .WithHttpEndpoint(name: "otlp", targetPort: 18889)
                   .PublishAsDockerComposeService((_, service) =>
                   {
                       service.Restart = "always";
                   });

            builder.Eventing.Subscribe<BeforeStartEvent>((e, ct) =>
            {
                // We loop over all resources and set the OTLP endpoint to the dashboard
                // we should make WithOtlpExporter() add an annotation so we can detect this
                // automatically in the future.
                foreach (var r in e.Model.Resources.OfType<IResourceWithEnvironment>())
                {
                    if (r == dashboard.Resource)
                    {
                        continue;
                    }

                    builder.CreateResourceBuilder(r).WithEnvironment(c =>
                    {
                        c.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = dashboard.GetEndpoint("otlp");
                        c.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc";
                        c.EnvironmentVariables["OTEL_SERVICE_NAME"] = r.Name;
                    });
                }

                return Task.CompletedTask;
            });
        }
    }
}