using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class AiEvaluationTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class SyntheticProviderBoundary : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string _) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            string body;
            if (request.RequestUri!.Host == "openrouter.ai")
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("test-only-openrouter-key", request.Headers.Authorization?.Parameter);
                Assert.Equal("nvidia/nemotron-3.5-super-vl:free", payload.RootElement.GetProperty("model").GetString());
                var route = payload.RootElement.GetProperty("provider");
                Assert.False(route.GetProperty("allow_fallbacks").GetBoolean());
                Assert.Equal(0, route.GetProperty("max_price").GetProperty("prompt").GetInt32());
                Assert.Equal(0, route.GetProperty("max_price").GetProperty("completion").GetInt32());
                body = ClassificationResponse();
            }
            else
            {
                Assert.Equal("generativelanguage.googleapis.com", request.RequestUri.Host);
                Assert.Equal(RemoteEmbeddings.Endpoint, request.RequestUri.ToString());
                Assert.Equal("test-only-gemini-key", request.Headers.GetValues("x-goog-api-key").Single());
                Assert.Equal(768, payload.RootElement.GetProperty("outputDimensionality").GetInt32());
                body = EmbeddingResponse();
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        private static string ClassificationResponse() =>
            """{"model":"nvidia/nemotron-3.5-super-vl:free","choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"medium\",\"risk\":\"medium\",\"explanation\":\"Synthetic test output\"}"}}]}""";

        private static string EmbeddingResponse()
        {
            var vector = new float[768];
            vector[0] = 1;
            return JsonSerializer.Serialize(new { embedding = new { values = vector } });
        }
    }

    private async Task<(WebApplicationFactory<Program> Host, HttpClient Client, SyntheticProviderBoundary Provider)> AdminClient()
    {
        var provider = new SyntheticProviderBoundary();
        var host = campus.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> {
                ["AI:OpenRouterKey"] = "test-only-openrouter-key",
                ["AI:OpenRouterDailyRequestLimit"] = "50",
                ["AI:OpenRouterRequestsPerMinute"] = "20",
                ["AI:GeminiKey"] = "test-only-gemini-key",
                ["AI:GeminiFreeTierOnly"] = "true",
                ["AI:GeminiDailyRequestLimit"] = "50",
                ["AI:GeminiRequestsPerMinute"] = "10"
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(provider);
            });
        });
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CampusFixture.Csrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(campus.Admin), password = campus.Password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        await CampusFixture.Csrf(client);
        return (host, client, provider);
    }

    [DatabaseFact]
    public async Task Preflight_counts_separate_gallery_text_and_image_embeddings_without_provider_calls()
    {
        var (host, client, provider) = await AdminClient();
        using (host)
        using (client)
        {
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
                db.AiEvaluationRuns.RemoveRange(db.AiEvaluationRuns);
                db.AiEvaluationVectors.RemoveRange(db.AiEvaluationVectors);
                await db.SaveChangesAsync();
            }
            var expected = new Dictionary<string, int> {
                ["integration"] = 4, ["maintenance-text"] = 12, ["maintenance-image"] = 4, ["lost-found"] = 20
            };
            foreach (var suite in expected)
            {
                var response = await CampusFixture.Response(await client.GetAsync("/api/v1/admin/ai-evaluations/preflight?suite=" + suite.Key));
                Assert.Equal(suite.Value, response.GetProperty("caseCount").GetInt32());
                Assert.Equal(suite.Value, response.GetProperty("apiCallsRequired").GetInt32());
                Assert.Equal(0, response.GetProperty("usage").GetProperty("maximumPrice").GetDecimal());
                Assert.False(response.GetProperty("usage").GetProperty("paidFallbacks").GetBoolean());
            }
            Assert.Equal(0, provider.Calls);
        }
    }

    [DatabaseFact]
    public async Task Completed_api_case_is_persisted_and_page_refresh_does_not_repeat_it()
    {
        var (host, client, provider) = await AdminClient();
        using (host)
        using (client)
        {
            var created = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/admin/ai-evaluations/runs", new { suite = "integration" }));
            var runId = created.GetProperty("id").GetGuid();
            Assert.Equal(4, created.GetProperty("apiCallsRequired").GetInt32());
            await CampusFixture.Csrf(client);
            await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/resume", new { }));
            await CampusFixture.Csrf(client);
            var step = await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/next", new { }));
            Assert.Equal("completed", step.GetProperty("case").GetProperty("state").GetString());
            Assert.Equal(1, provider.Calls);

            var saved = await CampusFixture.Response(await client.GetAsync($"/api/v1/admin/ai-evaluations/runs/{runId}"));
            var stored = saved.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("state").GetString() == "completed");
            var completedCaseId = stored.GetProperty("id").GetGuid();
            Assert.False(string.IsNullOrWhiteSpace(stored.GetProperty("actual").GetRawText()));
            Assert.Equal(JsonValueKind.Number, stored.GetProperty("elapsedMilliseconds").ValueKind);
            Assert.True(stored.GetProperty("elapsedMilliseconds").GetInt64() >= 0);

            await CampusFixture.Csrf(client);
            await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/pause", new { }));
            await CampusFixture.Csrf(client);
            await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/resume", new { }));
            await CampusFixture.Csrf(client);
            await CampusFixture.Response(await client.PostAsJsonAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/next", new { }));
            Assert.Equal(2, provider.Calls);

            var resumed = await CampusFixture.Response(await client.GetAsync($"/api/v1/admin/ai-evaluations/runs/{runId}"));
            Assert.Contains(resumed.GetProperty("cases").EnumerateArray(), row => row.GetProperty("id").GetGuid() == completedCaseId &&
                row.GetProperty("state").GetString() == "completed");
            await client.GetAsync("/api/v1/admin/ai-evaluations");
            await client.GetAsync("/api/v1/admin/ai-evaluations");
            Assert.Equal(2, provider.Calls);
            var csv = await client.GetStringAsync($"/api/v1/admin/ai-evaluations/runs/{runId}/csv");
            Assert.Contains("RecordType,Date,Source,Metric,Value", csv, StringComparison.Ordinal);
            Assert.Contains("measured-metric", csv, StringComparison.Ordinal);
            Assert.Contains("integration-embedding-image", csv, StringComparison.Ordinal);
            Assert.Contains("Historical", csv, StringComparison.OrdinalIgnoreCase);
        }
    }

    [DatabaseFact]
    public async Task Admin_evaluation_endpoints_reject_non_admin_accounts()
    {
        var (host, client, provider) = await AdminClient();
        using (host)
        using (client)
        {
            var staff = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using (staff)
            {
                await CampusFixture.Csrf(staff);
                var login = await staff.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(campus.Staff), password = campus.Password });
                Assert.True(login.IsSuccessStatusCode);
                var response = await staff.GetAsync("/api/v1/admin/ai-evaluations");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
            Assert.Equal(0, provider.Calls);
        }
    }
}
