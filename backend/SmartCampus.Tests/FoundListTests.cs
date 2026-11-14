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

public sealed class FoundListTests(CampusFixture campus) : IClassFixture<CampusFixture>
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
    public async Task Found_item_page_batches_reads_and_includes_claimant_and_receiving_department_access()
    {
        var counter = new CommandCounter();
        using var host = campus.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<CampusDb>(options => options.AddInterceptors(counter))));
        var now = DateTimeOffset.UtcNow;
        var ids = new List<Guid>();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            for (var index = 0; index < 13; index++)
            {
                var holder = index == 12 ? campus.Staff : campus.Student;
                var item = new FoundItem { UploaderId = holder, CustodianId = holder,
                    Description = $"TEST ONLY item {index}", Place = "TEST ONLY place", Storage = "TEST ONLY storage",
                    Date = new DateOnly(2026, 1, 1), State = index == 12 ? "reserved" : "available",
                    CreatedAt = now.AddSeconds(-index) };
                ids.Add(item.Id);
                db.Items.Add(item);
                db.Media.Add(new MediaObject { ItemId = item.Id, Bucket = "lost-found-images",
                    ObjectKey = $"TEST-PRIVATE-OBJECT-KEY/{item.Id}" });
                if (index == 1)
                {
                    db.SearchGrants.Add(new SearchGrant { ItemId = item.Id, UserId = campus.Other,
                        ExpiresAt = now.AddMinutes(15) });
                    db.Handovers.Add(new CustodyHandover { ItemId = item.Id, FromUserId = campus.Student,
                        DepartmentId = campus.SecondDepartment, Office = "TEST ONLY cancelled desk", Status = "cancelled",
                        CreatedAt = now.AddMinutes(-1) });
                    db.Handovers.Add(new CustodyHandover { ItemId = item.Id, FromUserId = campus.Student,
                        DepartmentId = campus.Department, Office = "TEST ONLY receiving desk", Status = "pending",
                        CreatedAt = now });
                }
                if (index == 12)
                    db.Claims.Add(new RecoveryRequest { ItemId = item.Id, RequesterId = campus.Other,
                        Message = "TEST ONLY claim", Status = "approved", ReservationActive = true,
                        ReviewedBy = campus.Staff, ReviewedAt = now, Phone = "TEST-PRIVATE-PHONE" });
            }
            await db.SaveChangesAsync();
        }

        async Task<JsonElement> Page(Guid actor, int pageSize)
        {
            using var scope = host.Services.CreateScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "test"))
            };
            try
            {
                var result = await scope.ServiceProvider.GetRequiredService<LostFoundService>().List(1, pageSize);
                return JsonSerializer.SerializeToElement(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            finally { accessor.HttpContext = null; }
        }

        foreach (var actor in new[] { campus.Student, campus.Staff })
        {
            counter.Reads = 0;
            var single = await Page(actor, 1);
            var singleReads = counter.Reads;
            Assert.InRange(singleReads, 1, 10);
            counter.Reads = 0;
            var full = await Page(actor, 100);
            Assert.Equal(singleReads, counter.Reads);
            Assert.Equal(single[0].GetProperty("id").GetGuid(), full[0].GetProperty("id").GetGuid());
            Assert.DoesNotContain("TEST-PRIVATE-OBJECT-KEY", full.GetRawText());
            Assert.DoesNotContain("TEST-PRIVATE-PHONE", full.GetRawText());
        }

        var studentItems = await Page(campus.Student, 100);
        Assert.Equal(12, studentItems.GetArrayLength());
        var held = studentItems.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == ids[1]);
        Assert.Equal("TEST ONLY storage", held.GetProperty("storage").GetString());
        Assert.Equal("TEST ONLY receiving desk", held.GetProperty("handover").GetProperty("office").GetString());
        var departmentItems = await Page(campus.Staff, 100);
        Assert.Equal(2, departmentItems.GetArrayLength());
        var incoming = departmentItems.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == ids[1]);
        Assert.True(incoming.GetProperty("canReceive").GetBoolean());
        Assert.Null(incoming.GetProperty("storage").GetString());
        Assert.Equal("TEST ONLY receiving desk", incoming.GetProperty("handover").GetProperty("office").GetString());
        var claimantItems = await Page(campus.Other, 100);
        Assert.Equal(2, claimantItems.GetArrayLength());
        var granted = claimantItems.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == ids[1]);
        Assert.Null(granted.GetProperty("storage").GetString());
        Assert.True(granted.GetProperty("handover").ValueKind is JsonValueKind.Null);
        Assert.True(granted.GetProperty("canClaim").GetBoolean());
        var claimed = claimantItems.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == ids[12]);
        Assert.Null(claimed.GetProperty("storage").GetString());
        Assert.False(claimed.GetProperty("canClaim").GetBoolean());
        Assert.Equal(0, (await Page(campus.Outsider, 100)).GetArrayLength());
        var adminItems = await Page(campus.Admin, 100);
        Assert.Equal(13, adminItems.GetArrayLength());
        var adminIncoming = adminItems.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == ids[1]);
        Assert.Equal("TEST ONLY storage", adminIncoming.GetProperty("storage").GetString());
        Assert.Equal("TEST ONLY receiving desk", adminIncoming.GetProperty("handover").GetProperty("office").GetString());
    }
}
