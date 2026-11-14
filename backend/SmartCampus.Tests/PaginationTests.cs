using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class PaginationTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private static readonly DateTimeOffset EqualCreatedAt = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    [DatabaseFact]
    public async Task Maintenance_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_scoped_to_the_reporter()
    {
        var ids = CreateIds("10000000");
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Reports.AddRange(ids.Select(id => new MaintenanceReport
            {
                Id = id,
                ReporterId = campus.Student,
                DepartmentId = campus.Department,
                FacultyId = campus.Faculty,
                BuildingId = campus.Building,
                FloorId = campus.Floor,
                Description = "TEST ONLY pagination maintenance",
                Details = "TEST ONLY",
                Status = "submitted",
                CreatedAt = EqualCreatedAt
            }));
            await db.SaveChangesAsync();
        }

        using var reporter = await campus.Login(campus.Student);
        using var unrelated = await campus.Login(campus.Other);
        await AssertPages(reporter, "/api/v1/maintenance", ids);
        Assert.Empty(await ReadAllPages(unrelated, "/api/v1/maintenance", 2));
    }

    [DatabaseFact]
    public async Task Found_item_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_scoped_to_the_uploader()
    {
        var ids = CreateIds("20000000");
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Items.AddRange(ids.Select(id => new FoundItem
            {
                Id = id,
                UploaderId = campus.Other,
                CustodianId = campus.Other,
                Description = "TEST ONLY pagination found item",
                Place = "TEST ONLY",
                Date = new DateOnly(2026, 1, 15),
                Storage = "TEST ONLY",
                State = "available",
                CreatedAt = EqualCreatedAt
            }));
            await db.SaveChangesAsync();
        }

        using var uploader = await campus.Login(campus.Other);
        using var unrelated = await campus.Login(campus.Outsider);
        await AssertPages(uploader, "/api/v1/found", ids);
        Assert.Empty(await ReadAllPages(unrelated, "/api/v1/found", 2));
    }

    [DatabaseFact]
    public async Task Claim_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_scoped_to_the_requester()
    {
        var itemIds = CreateIds("30000000");
        var claimIds = CreateIds("40000000");
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Items.AddRange(itemIds.Select(id => new FoundItem
            {
                Id = id,
                UploaderId = campus.Staff,
                CustodianId = campus.Staff,
                Description = "TEST ONLY pagination claim item",
                Place = "TEST ONLY",
                Date = new DateOnly(2026, 1, 15),
                Storage = "TEST ONLY",
                State = "available",
                CreatedAt = EqualCreatedAt
            }));
            db.Claims.AddRange(claimIds.Select((id, index) => new RecoveryRequest
            {
                Id = id,
                ItemId = itemIds[index],
                RequesterId = campus.Student,
                Message = "TEST ONLY pagination claim",
                Status = "pending",
                CreatedAt = EqualCreatedAt
            }));
            await db.SaveChangesAsync();
        }

        using var requester = await campus.Login(campus.Student);
        using var unrelated = await campus.Login(campus.Other);
        await AssertPages(requester, "/api/v1/claims", claimIds);
        Assert.Empty(await ReadAllPages(unrelated, "/api/v1/claims", 2));
    }

    [DatabaseFact]
    public async Task Notification_pages_are_complete_disjoint_and_stable_for_equal_timestamps_and_recipient_owned()
    {
        var ids = CreateIds("50000000");
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Notifications.AddRange(ids.Select(id => new Notification
            {
                Id = id,
                RecipientId = campus.Student,
                Kind = "TEST ONLY",
                EntityType = "maintenance",
                EntityId = Guid.NewGuid(),
                CreatedAt = EqualCreatedAt
            }));
            await db.SaveChangesAsync();
        }

        using var recipient = await campus.Login(campus.Student);
        using var unrelated = await campus.Login(campus.Other);
        await AssertPages(recipient, "/api/v1/notifications", ids);
        Assert.Empty(await ReadAllPages(unrelated, "/api/v1/notifications", 2));
    }

    [DatabaseFact]
    public async Task Paginated_operational_endpoints_require_an_authenticated_user()
    {
        using var anonymous = campus.CreateClient();
        foreach (var endpoint in new[]
        {
            "/api/v1/maintenance?page=1&pageSize=2",
            "/api/v1/found?page=1&pageSize=2",
            "/api/v1/claims?page=1&pageSize=2",
            "/api/v1/notifications?page=1&pageSize=2"
        })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(endpoint)).StatusCode);
        }
    }

    [DatabaseFact]
    public async Task Maximum_page_numbers_do_not_overflow_any_operational_offset()
    {
        using var student = await campus.Login(campus.Student);
        using var admin = await campus.Login(campus.Admin);
        var requests = new (HttpClient Client, string Endpoint)[]
        {
            (student, "/api/v1/maintenance"),
            (student, "/api/v1/found"),
            (student, "/api/v1/claims"),
            (student, "/api/v1/notifications"),
            (admin, "/api/v1/admin/accounts")
        };

        foreach (var (client, endpoint) in requests)
        {
            var response = await CampusFixture.Response(await client.GetAsync($"{endpoint}?page={int.MaxValue}&pageSize=100"));
            Assert.Empty(response.GetProperty("items").EnumerateArray());
        }
    }

    private static async Task AssertPages(HttpClient client, string endpoint, IEnumerable<Guid> seededIds)
    {
        var expected = seededIds.OrderBy(id => id.ToString("N"), StringComparer.Ordinal).ToArray();
        foreach (var pageSize in new[] { 1, 2, 3 })
        {
            var actual = await ReadAllPages(client, endpoint, pageSize);
            Assert.Equal(expected, actual);
            Assert.Equal(actual.Length, actual.Distinct().Count());
        }
    }

    private static async Task<Guid[]> ReadAllPages(HttpClient client, string endpoint, int pageSize)
    {
        var results = new List<Guid>();
        for (var page = 1; page <= 10; page++)
        {
            var response = await CampusFixture.Response(await client.GetAsync($"{endpoint}?page={page}&pageSize={pageSize}"));
            var ids = response.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid()).ToArray();
            if (ids.Length == 0) return results.ToArray();

            Assert.InRange(ids.Length, 1, pageSize);
            Assert.Empty(ids.Intersect(results));
            results.AddRange(ids);
        }

        throw new Xunit.Sdk.XunitException("Pagination did not terminate within ten pages.");
    }

    private static Guid[] CreateIds(string prefix) => Enumerable.Range(1, 5)
        .Select(index => Guid.Parse($"{prefix}-0000-0000-0000-{index:D12}"))
        .ToArray();
}
