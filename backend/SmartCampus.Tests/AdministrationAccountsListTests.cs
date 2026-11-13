using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Api;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class AdministrationAccountsListTests(CampusFixture campus) : IClassFixture<CampusFixture>
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
    public async Task Admin_account_page_batches_roles_scopes_and_demo_markers()
    {
        var counter = new CommandCounter();
        using var host = campus.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<CampusDb>(options => options.AddInterceptors(counter))));

        async Task<JsonElement> Page(int pageSize)
        {
            using var scope = host.Services.CreateScope();
            var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, campus.Admin.ToString())], "test"))
            };
            try
            {
                var controller = new AdministrationController(
                    scope.ServiceProvider.GetRequiredService<CampusDb>(),
                    scope.ServiceProvider.GetRequiredService<Access>(),
                    scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>(),
                    scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
                    scope.ServiceProvider.GetRequiredService<Events>());
                return JsonSerializer.SerializeToElement(await controller.Accounts(1, pageSize), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            finally { accessor.HttpContext = null; }
        }

        counter.Reads = 0;
        var single = await Page(1);
        var singleReads = counter.Reads;
        Assert.InRange(singleReads, 1, 8);
        counter.Reads = 0;
        var full = await Page(100);
        Assert.Equal(singleReads, counter.Reads);
        Assert.Equal(single.GetProperty("items")[0].GetProperty("key").GetGuid(), full.GetProperty("items")[0].GetProperty("key").GetGuid());
        Assert.Equal(7, full.GetProperty("items").GetArrayLength());

        var staff = full.GetProperty("items").EnumerateArray()
            .Single(entry => entry.GetProperty("account").GetProperty("email").GetString() == campus.Email(campus.Staff));
        var account = staff.GetProperty("account");
        Assert.Equal("maintenance", account.GetProperty("role").GetString());
        Assert.Contains("maintenance", account.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.Contains("department", account.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.Contains("test-maintenance", account.GetProperty("departments").EnumerateArray()
            .Select(department => department.GetProperty("code").GetString()));
        Assert.False(account.GetProperty("isDemo").GetBoolean());
    }
}
