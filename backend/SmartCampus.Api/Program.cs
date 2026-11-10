using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration["Release:Mode"] ??= builder.Environment.IsDevelopment() ? "open" : "controlled";
var administrativeCommand = args.Contains("--migrate") || args.Contains("--bootstrap-admin");
if (!builder.Environment.IsDevelopment() && !administrativeCommand)
    HostingSecurity.Validate(builder.Configuration);
builder.Services.AddDbContext<CampusDb>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("Campus") ?? "Host=localhost;Database=smart_campus;Username=smart_campus",
    pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "smart_campus")));
builder.Services.AddIdentity<CampusUser, IdentityRole<Guid>>(options => {
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Tokens.EmailConfirmationTokenProvider = "Email24";
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<CampusDb>().AddDefaultTokenProviders().AddTokenProvider<EmailConfirmationProvider>("Email24");
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddSingleton<DatabaseTicketStore>();
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<DatabaseTicketStore>((options, store) => options.SessionStore = store);
builder.Services.ConfigureApplicationCookie(options => {
    options.Cookie.Name = "SmartCampus.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
HostingSecurity.ConfigureKeys(builder.Services, builder.Configuration, builder.Environment.IsDevelopment() || administrativeCommand);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Access>();
builder.Services.AddSingleton<ReleasePolicy>();
builder.Services.AddScoped<Events>();
builder.Services.AddScoped<EmailSender>();
builder.Services.AddScoped<ResendEmailTransport>();
builder.Services.AddScoped<PrivateMedia>();
builder.Services.AddScoped<MaintenanceService>();
builder.Services.AddScoped<LostFoundService>();
builder.Services.AddScoped<AiProviders>();
builder.Services.AddScoped<MaintenanceAssessmentProcessor>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInferenceBudget, InferenceBudget>();
builder.Services.AddScoped<RetrievalService>();
builder.Services.AddHttpClient("storage", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("inference", c => c.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddHttpClient("worker", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("email", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
    new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState) {
        Status = 400, Title = "invalid_input", Extensions = { ["code"] = "invalid_input" } }));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
if (System.Net.IPAddress.TryParse(builder.Configuration["ReverseProxy:KnownProxy"], out var proxyAddress))
    builder.Services.Configure<ForwardedHeadersOptions>(options => {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Add(proxyAddress);
        options.ForwardLimit = 1;
    });
builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimits:PerMinute", 120), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("auth", context => HttpMethods.IsGet(context.Request.Method) ? RateLimitPartition.GetNoLimiter("auth-read") : RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 15), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (System.Net.IPAddress.TryParse(builder.Configuration["ReverseProxy:KnownProxy"], out _)) app.UseForwardedHeaders();
if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<CampusDb>().Database.MigrateAsync();
    return;
}
if (args.Contains("--bootstrap-admin"))
{
    using var scope = app.Services.CreateScope();
    await Bootstrap.Admin(scope.ServiceProvider, builder.Configuration);
    return;
}
if (args.Contains("--seed-demo"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Demo data is development-only");
    using var scope = app.Services.CreateScope();
    await DemoSeed.Create(scope.ServiceProvider, builder.Configuration);
    return;
}
app.Use(async (context, next) => {
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'; base-uri 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (Exception exception) when (exception is RuleException or DbUpdateConcurrencyException or DbUpdateException or AntiforgeryValidationException)
    {
        var (status, code) = exception switch {
            RuleException rule => (rule.Status, rule.Code),
            DbUpdateConcurrencyException => (409, "conflict"),
            DbUpdateException update when update.InnerException is Npgsql.PostgresException { SqlState: "23505" } => (409, "conflict"),
            AntiforgeryValidationException => (400, "invalid_csrf"),
            _ => (500, "internal")
        };
        if (status is 400 or 403 or 409) app.Logger.LogWarning("Request rejected: {Code}; endpoint {Endpoint}; trace {TraceId}", code, context.GetEndpoint()?.DisplayName, context.TraceIdentifier);
        if (exception is RuleException { RetryAt: { } retryAt })
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling((retryAt - DateTimeOffset.UtcNow).TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Results.Problem(statusCode: status, title: code, extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(context);
    }
    catch (Exception exception)
    {
        app.Logger.LogError("Request failed: {ExceptionType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        await Results.Problem(statusCode: 500, title: "internal", extensions: new Dictionary<string, object?> { ["code"] = "internal" }).ExecuteAsync(context);
    }
});
if (!app.Environment.IsDevelopment())
{
    if ((string.Equals(builder.Configuration["Hosting:Platform"], "render", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builder.Configuration["Hosting:Platform"], "snapdeploy", StringComparison.OrdinalIgnoreCase)))
    {
        // Managed hosting edges (Render/SnapDeploy) redirect public HTTP to HTTPS and terminate TLS.
        // Never trust arbitrary X-Forwarded-Proto from the internet just to stop
        // an HTTP->HTTPS redirect loop inside the container.
        app.Use(async (context, next) => {
            context.Response.Headers.StrictTransportSecurity = "max-age=31536000";
            await next();
        });
    }
    else { app.UseHsts(); app.UseHttpsRedirection(); }
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
    await next();
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (CampusDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
await app.RunAsync();
public partial class Program;
