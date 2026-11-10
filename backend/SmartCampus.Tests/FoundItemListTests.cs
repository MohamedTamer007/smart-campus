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

public sealed class FoundItemListTests(CampusFixture campus) : IClassFixture<CampusFixture>
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
    public async Task Found_item_pages_batch_reads_and_preserve_handover_authority_and_claim_eligibility()
    {
        var counter = new CommandCounter();
        using var host = campus.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<CampusDb>(options => options.AddInterceptors(counter))));
        var now = DateTimeOffset.UtcNow;
        var imageIds = new Dictionary<Guid, Guid>();
        var handoverIds = new Dictionary<Guid, Guid>();
        var expiredGrantItemId = Guid.Empty;
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            for (var index = 0; index < 12; index++)
            {
                var found = new FoundItem { UploaderId = campus.Student, CustodianId = campus.Student,
                    Description = "TEST ONLY inventory item", Place = "TEST ONLY location", Storage = "TEST ONLY storage",
                    Date = new DateOnly(2026, 1, 1), CreatedAt = now.AddSeconds(-index) };
                db.Items.Add(found);
                var image = new MediaObject { ItemId = found.Id, Bucket = "lost-found-images", ObjectKey = "TEST-PRIVATE-OBJECT-KEY/" + found.Id };
                imageIds.Add(found.Id, image.Id);
                db.Media.Add(image);
                if (index < 3)
                {
                    var handover = new CustodyHandover { ItemId = found.Id, FromUserId = campus.Student,
                        DepartmentId = campus.SecondDepartment, Office = "TEST ONLY office",
                        Status = index == 1 ? "not-received" : "pending" };
                    db.Handovers.Add(handover);
                    handoverIds.Add(found.Id, handover.Id);
                }
                if (index < 2)
                {
                    db.SearchGrants.Add(new() { UserId = campus.Outsider, ItemId = found.Id,
                        ExpiresAt = now.AddMinutes(index == 0 ? 15 : -15) });
                    if (index == 1) expiredGrantItemId = found.Id;
                }
            }
            await db.SaveChangesAsync();
        }

        async Task<JsonElement> Page(Guid actor, int size)
        {
            using var scope = host.Services.CreateScope();
            var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            http.HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "test"))
            };
            try
            {
                return JsonSerializer.SerializeToElement(await scope.ServiceProvider.GetRequiredService<LostFoundService>().List(1, size),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            finally { http.HttpContext = null; }
        }

        counter.Reads = 0;
        var single = await Page(campus.Student, 1);
        var singleReads = counter.Reads;
        counter.Reads = 0;
        var ownerPage = await Page(campus.Student, 100);
        Assert.Equal(singleReads, counter.Reads);
        Assert.Equal(12, ownerPage.GetArrayLength());
        Assert.Equal(single[0].GetProperty("id").GetGuid(), ownerPage[0].GetProperty("id").GetGuid());
        Assert.DoesNotContain("TEST-PRIVATE-OBJECT-KEY", ownerPage.GetRawText());

        var receiverPage = await Page(campus.Outsider, 100);
        Assert.Equal(3, receiverPage.GetArrayLength());
        var first = receiverPage[0];
        var firstId = first.GetProperty("id").GetGuid();
        Assert.Equal(imageIds[firstId], Guid.Parse(first.GetProperty("mediaUrl").GetString()!.Split('/').Last()));
        Assert.True(first.GetProperty("canReceive").GetBoolean());
        Assert.True(first.GetProperty("canClaim").GetBoolean());
        Assert.False(first.GetProperty("canHandover").GetBoolean());
        Assert.Equal("TEST ONLY office", first.GetProperty("handover").GetProperty("office").GetString());
        Assert.Equal(handoverIds[firstId], first.GetProperty("handover").GetProperty("id").GetGuid());
        var expiredItem = receiverPage.EnumerateArray().Single(row => row.GetProperty("id").GetGuid() == expiredGrantItemId);
        Assert.Equal("not-received", expiredItem.GetProperty("handover").GetProperty("status").GetString());
        Assert.False(expiredItem.GetProperty("canClaim").GetBoolean());
        Assert.Equal(0, (await Page(campus.Other, 100)).GetArrayLength());
        Assert.Equal(12, (await Page(campus.Admin, 100)).GetArrayLength());
    }
}
