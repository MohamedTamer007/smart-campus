using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SmartCampus.Tests;

public sealed class GeminiLiveFactAttribute : FactAttribute
{
    public GeminiLiveFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SMART_CAMPUS_RUN_LIVE_AI_TESTS"), "true", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMART_CAMPUS_GEMINI_TEST_KEY")))
            Skip = "Set the explicit live-test switch and a separate Gemini test key to send at most two synthetic requests.";
    }
}

public sealed class GeminiLiveTests(ITestOutputHelper output)
{
    private sealed class TestClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("retrieval", name);
            return new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        }
    }

    private sealed class TwoRequestBudget : IInferenceBudget
    {
        private int reserved;
        public int Reserved => Volatile.Read(ref reserved);
        public Task Reserve(string provider, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (provider != "gemini" || Interlocked.Increment(ref reserved) > 2)
                throw new RuleException("ai_quota_exhausted", 503);
            return Task.CompletedTask;
        }
    }

    [GeminiLiveFact]
    public async Task Gemini_embedding_2_returns_768_dimensional_text_and_image_vectors_for_synthetic_inputs()
    {
        var key = Environment.GetEnvironmentVariable("SMART_CAMPUS_GEMINI_TEST_KEY")!;
        var config = Configuration(key);
        var budget = new TwoRequestBudget();
        var embeddings = new RemoteEmbeddings(new TestClientFactory(), config, budget);

        var timer = Stopwatch.StartNew();
        var text = await embeddings.Query("غرض افتراضي مفقود: زجاجة مياه زرقاء بجوار مكتبة خيالية.");
        output.WriteLine($"Synthetic Arabic text embedding: {text.Dimension} values in {timer.ElapsedMilliseconds} ms.");
        Assert.Equal(RemoteEmbeddings.Model, text.Model);
        Assert.Equal(768, text.Dimension);

        timer.Restart();
        var image = await embeddings.Image(FictionalImage());
        output.WriteLine($"Synthetic image embedding: {image.Dimension} values in {timer.ElapsedMilliseconds} ms.");
        Assert.Equal(RemoteEmbeddings.Model, image.Model);
        Assert.Equal(768, image.Dimension);
        Assert.Equal(2, budget.Reserved);
        Assert.True(RemoteEmbeddings.Configured(config));
    }

    private static IConfiguration Configuration(string key) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Search:Provider"] = "hosted",
        ["Search:ExternalProcessingApproved"] = "true", // synthetic test input only
        ["Search:GeminiExternalProcessingApproved"] = "true",
        ["Search:ExternalImagesApproved"] = "true", // generated illustration only
        ["Search:GeminiExternalImagesApproved"] = "true",
        ["Search:GeminiProviderDataCollection"] = "allow", // synthetic test input only
        ["AI:GeminiFreeTierOnly"] = "true",
        ["AI:GeminiKey"] = key,
        ["AI:GeminiDailyRequestLimit"] = "2",
        ["AI:GeminiRequestsPerMinute"] = "2"
    }).Build();

    private static byte[] FictionalImage()
    {
        using var bitmap = new SKBitmap(320, 220);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(246, 249, 252));
        using var bottle = new SKPaint { Color = new SKColor(44, 115, 198), IsAntialias = true };
        using var cap = new SKPaint { Color = new SKColor(20, 59, 105), IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(112, 34, 208, 194), 24, 24, bottle);
        canvas.DrawRect(new SKRect(138, 15, 182, 42), cap);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        return encoded!.ToArray();
    }
}
