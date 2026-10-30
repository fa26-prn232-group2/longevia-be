namespace Progress.IntegrationTests.Metrics;

public sealed class ProgressWeightConfigurationTests
{
    // 19. Container network and port configuration
    [Fact]
    public void Case19_ContainerNetworkAndPort_Configuration_Verified()
    {
        var root = FindRepoRoot();
        var composeText = File.ReadAllText(Path.Combine(root, "deployments", "docker-compose.yml"));
        Assert.Contains("PROGRESS__HTTP_PORT: 8080", composeText);
        Assert.Contains("PROGRESS__GRPC_PORT: 50051", composeText);
        Assert.Contains("IdentityGrpc__Address: http://identity:50051", composeText);
    }

    // 20. No public host exposure in production Compose
    [Fact]
    public void Case20_ProductionCompose_NoPublicHostExposure()
    {
        var root = FindRepoRoot();
        var composeText = File.ReadAllText(Path.Combine(root, "deployments", "docker-compose.yml"));
        var progressIndex = composeText.IndexOf("progress:", StringComparison.Ordinal);
        var nextServiceIndex = composeText.IndexOf("recommendation:", progressIndex, StringComparison.Ordinal);
        var progressSection = composeText.Substring(progressIndex, nextServiceIndex - progressIndex);

        Assert.DoesNotContain("50051:50051", progressSection);
        Assert.DoesNotContain("ports:", progressSection);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "longevia-be.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
}
