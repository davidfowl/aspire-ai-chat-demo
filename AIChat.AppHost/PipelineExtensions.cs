using System.Text.RegularExpressions;
using Aspire.Hosting.Pipelines;
using CliWrap;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIChat.AppHost;

public static partial class PipelineExtensions
{
    public static void AddGhcrPushStep(this IDistributedApplicationPipeline pipeline, IComputeResource[] resourcesToPublish)
    {
        pipeline.AddStep("push-gh", async context =>
        {
            var configuration = context.Services.GetRequiredService<IConfiguration>();

            // Get raw configuration values from environment
            var ghcrRepo = configuration["GHCR_REPO"] ?? throw new InvalidOperationException("GHCR_REPO environment variable is required");
            var branchName = configuration["BRANCH_NAME"] ?? throw new InvalidOperationException("BRANCH_NAME environment variable is required");
            var buildNumber = configuration["BUILD_NUMBER"] ?? throw new InvalidOperationException("BUILD_NUMBER environment variable is required");
            var gitSha = configuration["GIT_SHA"] ?? throw new InvalidOperationException("GIT_SHA environment variable is required");

            // Sanitize branch name for Docker tag (replace invalid characters with -)
            var sanitizedBranch = SanitizerRegex().Replace(branchName, "-").ToLowerInvariant();

            // Use short SHA (first 7 characters)
            var shortSha = gitSha.Length > 7 ? gitSha[..7] : gitSha;

            // Build tag: <sanitized-branch>-<build-number>-<short-sha>
            var tag = $"{sanitizedBranch}-{buildNumber}-{shortSha}";

            foreach (var resource in resourcesToPublish)
            {
                // For project resources, use hardcoded "latest" tag
                var localImageName = resource is ProjectResource ? $"{resource.Name}:latest" : null;

                if (localImageName is null && !resource.TryGetContainerImageName(out localImageName))
                {
                    context.Logger.LogWarning("{ImageName} image name not found, skipping", localImageName);
                    continue;
                }

                var remoteTag = $"{ghcrRepo}/{localImageName}:{tag}";

                context.Logger.LogInformation("Tagging {LocalImage} as {RemoteTag}", localImageName, remoteTag);

                // Tag the image
                await Cli.Wrap("docker")
                    .WithArguments(["tag", localImageName, remoteTag])
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line => context.Logger.LogDebug("{Output}", line)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(line => context.Logger.LogError("{Error}", line)))
                    .ExecuteAsync();

                context.Logger.LogInformation("Pushing {RemoteTag}", remoteTag);

                // Push the image
                await Cli.Wrap("docker")
                    .WithArguments(["push", remoteTag])
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line => context.Logger.LogDebug("{Output}", line)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(line => context.Logger.LogError("{Error}", line)))
                    .ExecuteAsync();
            }
        },
        dependsOn: WellKnownPipelineSteps.Build);
    }

    [GeneratedRegex("[^a-zA-Z0-9._-]")]
    private static partial Regex SanitizerRegex();
}
