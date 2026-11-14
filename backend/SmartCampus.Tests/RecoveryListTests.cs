using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class RecoveryListTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Reads { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }

    [DatabaseFact]
    public async Task Recovery_page_uses_constant_reads_and_preserves_scope_and_collection_privacy()
    {
        var counter = new CommandCounter();
        using var host = campus.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<CampusDb>(options => options.AddInterceptors(counter))));
        var now = DateTimeOffset.UtcNow;
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            for (var index = 0; index < 12; index++)
            {
                var item = new FoundItem { UploaderId = campus.Staff, CustodianDepartmentId = campus.Department,
                    Description = "TEST ONLY page item", Place = "TEST ONLY place", Storage = "TEST ONLY storage",
                    Date = new DateOnly(2026, 1, 1), State = index == 0 ? "reserved" : "available" };
                db.Items.Add(item);
                db.Claims.Add(new() { ItemId = item.Id, RequesterId = campus.Student, CreatedAt = now.AddSeconds(-index),
                    Status = index == 0 ? "approved" : "pending", ReservationActive = index == 0,
                    Phone = "TEST-PRIVATE-PHONE", Place = "TEST-PRIVATE-COLLECTION", Instructions = "TEST-PRIVATE-INSTRUCTIONS" });
            }
            await db.SaveChangesAsync();
        }

        async Task<JsonElement> Page(Guid actor, int size)
        {
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "test"))
            };
            return JsonSerializer.SerializeToElement(await scope.ServiceProvider.GetRequiredService<LostFoundService>().Claims(1, size),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        counter.Reads = 0;
        var single = await Page(campus.Student, 1);
        var singleReads = counter.Reads;
        counter.Reads = 0;
        var full = await Page(campus.Student, 100);
        Assert.Equal(singleReads, counter.Reads);
        Assert.Equal(1, single.GetArrayLength());
        Assert.Equal(12, full.GetArrayLength());
        Assert.Equal(single[0].GetProperty("id").GetGuid(), full[0].GetProperty("id").GetGuid());
        Assert.Equal("approved", full[0].GetProperty("status").GetString());
        Assert.Equal("TEST-PRIVATE-PHONE", full[0].GetProperty("phone").GetString());
        foreach (var pending in full.EnumerateArray().Skip(1))
        {
            Assert.Equal(JsonValueKind.Null, pending.GetProperty("phone").ValueKind);
            Assert.Equal(JsonValueKind.Null, pending.GetProperty("place").ValueKind);
            Assert.Equal(JsonValueKind.Null, pending.GetProperty("instructions").ValueKind);
            Assert.False(pending.GetProperty("canApprove").GetBoolean());
        }
        var custodian = await Page(campus.Staff, 100);
        Assert.Equal(12, custodian.GetArrayLength());
        Assert.Equal("TEST-PRIVATE-PHONE", custodian[1].GetProperty("phone").GetString());
        Assert.True(custodian[1].GetProperty("canApprove").GetBoolean());
        Assert.Equal(0, (await Page(campus.Outsider, 100)).GetArrayLength());
        Assert.Equal(0, (await Page(campus.Other, 100)).GetArrayLength());
        Assert.Equal(12, (await Page(campus.Admin, 100)).GetArrayLength());
    }
}
