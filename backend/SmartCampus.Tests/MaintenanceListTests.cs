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

public sealed class MaintenanceListTests(CampusFixture campus) : IClassFixture<CampusFixture>
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
    public async Task Maintenance_page_batches_reads_and_keeps_assessments_and_assignees_within_staff_scope()
    {
        var counter = new CommandCounter();
        using var host = campus.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<CampusDb>(options => options.AddInterceptors(counter))));
        var now = DateTimeOffset.UtcNow;
        var images = new Dictionary<Guid, Guid>();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            for (var index = 0; index < 13; index++)
            {
                var report = new MaintenanceReport { ReporterId = index == 12 ? campus.Staff : campus.Student,
                    DepartmentId = index == 12 ? campus.SecondDepartment : campus.Department,
                    FacultyId = campus.Faculty, BuildingId = campus.Building, FloorId = campus.Floor,
                    CreatedAt = now.AddSeconds(-index), Description = "TEST ONLY report", Details = "TEST ONLY details",
                    ContentVersion = 2, FaultType = "TEST-PRIVATE-DECISION" };
                db.Reports.Add(report);
                if (index < 2)
                {
                    var image = new MediaObject { ReportId = report.Id, Bucket = "maintenance-images", ObjectKey = "TEST-PRIVATE-OBJECT-KEY/" + report.Id };
                    images.Add(report.Id, image.Id);
                    db.Media.Add(image);
                }
                db.History.Add(new() { ReportId = report.Id, ActorId = campus.Student, Status = "submitted",
                    Note = "TEST visible history", DecisionJson = "TEST-PRIVATE-HISTORY-DECISION" });
                db.Assessments.AddRange(new Assessment { ReportId = report.Id, InputVersion = 1, State = "completed", FaultType = "TEST-STALE-ASSESSMENT" },
                    new Assessment { ReportId = report.Id, InputVersion = 2, State = "completed", FaultType = "TEST-CURRENT-ASSESSMENT", Explanation = "TEST-PRIVATE-EXPLANATION" });
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
                return JsonSerializer.SerializeToElement(await scope.ServiceProvider.GetRequiredService<MaintenanceService>().List(1, size),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            finally { http.HttpContext = null; }
        }

        foreach (var actor in new[] { campus.Student, campus.Staff })
        {
            counter.Reads = 0;
            var single = await Page(actor, 1);
            var singleReads = counter.Reads;
            counter.Reads = 0;
            var page = await Page(actor, 100);
            Assert.Equal(singleReads, counter.Reads);
            Assert.Equal(single[0].GetProperty("id").GetGuid(), page[0].GetProperty("id").GetGuid());
            Assert.Equal(actor == campus.Student ? 12 : 13, page.GetArrayLength());
            Assert.DoesNotContain("TEST-STALE-ASSESSMENT", page.GetRawText());
            Assert.DoesNotContain("TEST-PRIVATE-EXPLANATION", page.GetRawText());
            Assert.DoesNotContain("TEST-PRIVATE-HISTORY-DECISION", page.GetRawText());
            Assert.DoesNotContain("TEST-PRIVATE-OBJECT-KEY", page.GetRawText());
            foreach (var report in page.EnumerateArray())
            {
                var expectedMedia = images.TryGetValue(report.GetProperty("id").GetGuid(), out var image) ? $"/api/v1/media/{image}" : null;
                Assert.Equal(expectedMedia, report.GetProperty("mediaUrl").GetString());
                Assert.Equal("TEST visible history", report.GetProperty("history")[0].GetProperty("note").GetString());
                Assert.Equal("TEST ONLY FACULTY", report.GetProperty("locationNames").GetProperty("faculty").GetProperty("en").GetString());
                var staff = actor == campus.Staff && report.GetProperty("departmentId").GetGuid() == campus.Department;
                Assert.Equal(staff, report.GetProperty("canViewAssessment").GetBoolean());
                if (staff)
                {
                    Assert.Equal("TEST-CURRENT-ASSESSMENT", report.GetProperty("assessment").GetProperty("type").GetString());
                    Assert.Equal("TEST-PRIVATE-DECISION", report.GetProperty("decision").GetProperty("type").GetString());
                    var assignees = report.GetProperty("allowedAssignees").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToArray();
                    Assert.Equal(2, assignees.Length);
                    Assert.Contains(campus.Staff, assignees);
                    Assert.Contains(campus.StaffTwo, assignees);
                }
                else
                {
                    Assert.Equal(JsonValueKind.Null, report.GetProperty("assessment").ValueKind);
                    Assert.Equal(JsonValueKind.Null, report.GetProperty("decision").ValueKind);
                    Assert.Equal(0, report.GetProperty("allowedAssignees").GetArrayLength());
                }
            }
        }
        Assert.Equal(0, (await Page(campus.Other, 100)).GetArrayLength());
        Assert.Equal(1, (await Page(campus.Outsider, 100)).GetArrayLength());
        Assert.Equal(13, (await Page(campus.Admin, 100)).GetArrayLength());
    }
}
