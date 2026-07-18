using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

namespace NexusLabs.Foundry.Langfuse.Tests;

public sealed class LangfuseServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFoundryLangfuse_Configured_RegistersEnabledScoreClientAndTracerProvider()
    {
        var services = new ServiceCollection();

        services.AddFoundryLangfuse(o =>
        {
            o.PublicKey = "pk-lf-1";
            o.SecretKey = "sk-lf-2";
            o.Host = "http://localhost:3000";
        });

        using var provider = services.BuildServiceProvider();

        var scoreClient = provider.GetService<ILangfuseScoreClient>();
        Assert.NotNull(scoreClient);
        Assert.True(scoreClient!.IsEnabled);
        var client = provider.GetRequiredService<ILangfuseClient>();
        Assert.Same(
            client.PublicationHealth,
            provider.GetRequiredService<LangfusePublicationHealth>());

        // AddOpenTelemetry registers a TracerProvider when tracing is configured.
        Assert.NotNull(provider.GetService<TracerProvider>());
    }

    [Fact]
    public void AddFoundryLangfuse_NotConfigured_RegistersDisabledNoOpScoreClient()
    {
        var services = new ServiceCollection();

        // Keys present but no Host/Region → not configured → disabled (no cloud egress).
        services.AddFoundryLangfuse(o =>
        {
            o.PublicKey = "pk-lf-1";
            o.SecretKey = "sk-lf-2";
            o.Host = null;
            o.Region = null;
        });

        using var provider = services.BuildServiceProvider();

        var scoreClient = provider.GetService<ILangfuseScoreClient>();
        Assert.NotNull(scoreClient);
        Assert.False(scoreClient!.IsEnabled);
        var client = provider.GetRequiredService<ILangfuseClient>();
        Assert.Same(
            client.PublicationHealth,
            provider.GetRequiredService<LangfusePublicationHealth>());
    }

    [Fact]
    public async Task AddFoundryLangfuse_DisabledScoreClient_RecordIsNoOp()
    {
        var services = new ServiceCollection();
        services.AddFoundryLangfuse(o => { o.Enabled = false; });

        using var provider = services.BuildServiceProvider();
        var scoreClient = provider.GetRequiredService<ILangfuseScoreClient>();
        var client = provider.GetRequiredService<ILangfuseClient>();

        await scoreClient.RecordScoreAsync(
            "trace",
            "name",
            1.0,
            options: null,
            TestContext.Current.CancellationToken);
        Assert.False(client.PublicationHealth.GetSnapshot().IsEnabled);
        Assert.Equal(0, client.PublicationHealth.GetSnapshot().ScoreUploads.Total);
    }

    [Fact]
    public async Task AddFoundryLangfuse_Configured_UsesNamedHttpClientPipelineForRestCalls()
    {
        var handler = new TrackingHttpMessageHandler(request =>
            LangfuseHttpStub.Json(
                System.Net.HttpStatusCode.OK,
                """{"id":"score-1"}"""));
        var services = new ServiceCollection();
        services
            .AddHttpClient(LangfuseServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddFoundryLangfuse(options =>
        {
            options.PublicKey = "pk-lf-1";
            options.SecretKey = "sk-lf-2";
            options.Host = "http://localhost:3000";
        });

        using var provider = services.BuildServiceProvider();
        var scoreClient = provider.GetRequiredService<ILangfuseScoreClient>();
        var client = provider.GetRequiredService<ILangfuseClient>();

        await scoreClient.RecordScoreAsync(
            "trace-1",
            "quality",
            1,
            new LangfuseScoreOptions { Id = "score-1" },
            TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.CapturedRequests);
        Assert.Equal("/api/public/scores", request.Uri.AbsolutePath);
        Assert.Equal(1, client.PublicationHealth.GetSnapshot().ScoreUploads.Succeeded);
    }

    [Fact]
    public void AddFoundryLangfuse_Configured_PreservesPreRegisteredResourceLockProvider()
    {
        var resourceLocks = new LangfuseInProcessResourceLockProvider();
        var services = new ServiceCollection();
        services.AddSingleton<ILangfuseResourceLockProvider>(resourceLocks);
        services.AddFoundryLangfuse(options =>
        {
            options.PublicKey = "pk-lf-1";
            options.SecretKey = "sk-lf-2";
            options.Host = "http://localhost:3000";
        });

        using var provider = services.BuildServiceProvider();

        Assert.Same(
            resourceLocks,
            provider.GetRequiredService<ILangfuseResourceLockProvider>());
    }
}
