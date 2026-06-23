using HybridCachePlayground.Web.Models;
using HybridCachePlayground.Web.Services;
using NCache.OSS.Microsoft.Extensions.Caching.Hybrid;
using Serilog;

// ─── Set SESSION_ID environment variable BEFORE configuration is built ────────
// This allows Serilog to expand %SESSION_ID% in appsettings.json

var sessionId = DateTimeOffset.Now.ToString(@"yyyy-MM-dd_HH-mm-ss.fff");
Environment.SetEnvironmentVariable("SESSION_ID", sessionId);

var logsDirectory  = Path.GetFullPath("logs");
var startupLogPath = Path.Combine(logsDirectory, $"hybridcache-{sessionId}.log");

// Ensure logs directory exists before Serilog tries to write
Directory.CreateDirectory(logsDirectory);

// ─── Bootstrap Serilog early so startup errors are captured ──────────────────

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("HybridCache Playground starting up | SessionId: {SessionId}", sessionId);

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Instance identity ────────────────────────────────────────────────────

    var instanceInfo = new InstanceInfo(
        Id:          builder.Configuration.GetValue("Instance:Id", "instance-1")!,
        Color:       builder.Configuration.GetValue("Instance:Color", "#4d9ef7")!,
        MachineName: System.Environment.MachineName,
        StartedAt:   DateTimeOffset.UtcNow);
    builder.Services.AddSingleton(instanceInfo);

    // ─── Serilog (configured from appsettings.json) ───────────────────────────
    // File path uses %SESSION_ID% which was set above

    builder.Host.UseSerilog((ctx, services, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("InstanceId", instanceInfo.Id));

    // ─── HybridCache ──────────────────────────────────────────────────────────────

    var NCacheHybridCacheOptions = builder.Configuration.GetSection("NCacheHybridCache");
    builder.Services.AddNCacheHybridCache(NCacheHybridCacheOptions);

    // ─── Playground ───────────────────────────────────────────────────────────────

    builder.Services.AddSingleton<ICachePlaygroundService, CachePlaygroundService>();
    builder.Services.AddSingleton(new LogFilePathProvider(startupLogPath));
    builder.Services.AddSingleton<INotificationService, NotificationService>();
    builder.Services.AddSingleton<IDebugToolsService, DebugToolsService>();

    // ─── MVC ──────────────────────────────────────────────────────────────────

    builder.Services.AddControllersWithViews();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Serilog HTTP request logging — replaces default ASP.NET request logs
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0}ms";
    });

    app.UseRouting();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
