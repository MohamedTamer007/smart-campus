using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SmartCampus.Tests;

public sealed class OpenRouterLiveFactAttribute : FactAttribute
{
    public OpenRouterLiveFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SMART_CAMPUS_RUN_LIVE_AI_TESTS"), "true", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMART_CAMPUS_OPENROUTER_TEST_KEY")))
            Skip = "Set the explicit live-test switch and a separate OpenRouter test key to send at most two free synthetic maintenance requests.";
    }
}

public sealed class OpenRouterLiveTests(ITestOutputHelper output)
{
    private sealed class ClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            if (name is not ("inference" or "retrieval")) throw new InvalidOperationException("Unexpected client name.");
            return new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }
    }

    private sealed class SixRequestBudget : IInferenceBudget
    {
        private int _reserved;
        public int Reserved => Volatile.Read(ref _reserved);

        public Task Reserve(string provider, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (provider != "openrouter" || Interlocked.Increment(ref _reserved) > 2)
                throw new RuleException("ai_quota_exhausted", 503);
            return Task.CompletedTask;
        }
    }

    [OpenRouterLiveFact]
    public async Task Maintenance_provider_remains_on_the_pinned_free_Nemotron_super_VL_model()
    {
        var key = Environment.GetEnvironmentVariable("SMART_CAMPUS_OPENROUTER_TEST_KEY")!;
        var config = Configuration(key);
        var clients = new ClientFactory();
        var budget = new SixRequestBudget();
        var ai = new AiProviders(clients, config, budget);

        var timer = Stopwatch.StartNew();
        var text = await ai.Assess(
            "بلاغ افتراضي: فيه ماسورة بتسرّب مياه في معمل خيالي، والمية قربت توصل ناحية لوحة كهرباء.",
            CancellationToken.None);
        output.WriteLine($"Maintenance text: {text.Type}/{text.Priority}/{text.Risk}; {timer.ElapsedMilliseconds} ms.");
        Assert.Contains(text.Type, new[] { "plumbing", "water", "electrical", "hvac", "lighting", "furniture", "doors", "safety", "other" });
        Assert.Contains(text.Priority, new[] { "low", "medium", "high" });
        Assert.Contains(text.Risk, new[] { "low", "medium", "high" });

        var leakImage = DrawLeak();
        timer.Restart();
        var image = await ai.Assess("Fictional leak in an empty campus lab.", CancellationToken.None, leakImage);
        output.WriteLine($"Maintenance image: {image.Type}/{image.Priority}/{image.Risk}; {timer.ElapsedMilliseconds} ms.");
        Assert.Contains(image.Type, new[] { "plumbing", "water", "electrical", "hvac", "lighting", "furniture", "doors", "safety", "other" });
        Assert.Contains(image.Priority, new[] { "low", "medium", "high" });
        Assert.Contains(image.Risk, new[] { "low", "medium", "high" });

        Assert.Equal(2, budget.Reserved);
        output.WriteLine("Requests reserved: 2; both target only the configured Nemotron free model with zero price caps, no provider fallback, and no retry.");
    }

    private static IConfiguration Configuration(string key) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["AI:Provider"] = "openrouter",
        ["AI:AllowNonSensitiveInference"] = "true",
        ["AI:AllowNonSensitiveImages"] = "true",
        ["AI:ProviderDataCollection"] = "allow",
        ["AI:OpenRouterKey"] = key,
        ["AI:OpenRouterDailyRequestLimit"] = "50",
        ["AI:OpenRouterRequestsPerMinute"] = "20",
        ["AI:FaultTypes:0"] = "plumbing",
        ["AI:FaultTypes:1"] = "water",
        ["AI:FaultTypes:2"] = "electrical",
        ["AI:FaultTypes:3"] = "hvac",
        ["AI:FaultTypes:4"] = "lighting",
        ["AI:FaultTypes:5"] = "furniture",
        ["AI:FaultTypes:6"] = "doors",
        ["AI:FaultTypes:7"] = "safety",
        ["AI:FaultTypes:8"] = "other",
        ["AI:Priorities:0"] = "low",
        ["AI:Priorities:1"] = "medium",
        ["AI:Priorities:2"] = "high",
        ["AI:Risks:0"] = "low",
        ["AI:Risks:1"] = "medium",
        ["AI:Risks:2"] = "high",
        ["Search:Provider"] = "hosted",
        ["Search:ExternalProcessingApproved"] = "true",
        ["Search:ExternalImagesApproved"] = "true",
        ["Search:ProviderDataCollection"] = "allow"
    }).Build();

    private static byte[] DrawLeak()
    {
        using var bitmap = new SKBitmap(640, 420);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var pipe = new SKPaint { Color = new SKColor(65, 118, 157), StrokeWidth = 42, IsStroke = true, StrokeCap = SKStrokeCap.Square, IsAntialias = true };
        using var dark = new SKPaint { Color = new SKColor(20, 49, 74), StrokeWidth = 12, IsStroke = true, IsAntialias = true };
        using var drops = new SKPaint { Color = new SKColor(25, 139, 211), IsAntialias = true };
        canvas.DrawLine(160, 170, 455, 170, pipe);
        canvas.DrawLine(455, 170, 455, 215, dark);
        foreach (var point in new[] { new SKPoint(455, 260), new SKPoint(490, 300), new SKPoint(430, 330) })
            canvas.DrawCircle(point, 12, drops);
        return Encode(bitmap);
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return encoded.ToArray();
    }

}
