using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prometheus;
using System.Text.Json.Serialization;
using TestingAppWeb.Data;
using TestingAppWeb.Extensions;
using TestingAppWeb.Middleware;
using TestingAppWeb.MiddleWare;
using TestingAppWeb.Options;
using TestingAppWeb.Services.Friends;
using TestingAppWeb.Services.Observability;
using TestingAppWeb.Services.Seed;
using TestingAppWeb.Services.Users;
using TestingAppWeb.Services.Chat;
using TestingAppWeb.Services.Chat.Bots;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .Validate(
        options => options.Limit > 0 && options.WindowSeconds > 0,
        "RateLimit:Limit and RateLimit:WindowSeconds must be positive.")
    .ValidateOnStart();

builder.Services
    .AddOptions<IpFilterOptions>()
    .Bind(builder.Configuration.GetSection(IpFilterOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<SeedOptions>()
    .Bind(builder.Configuration.GetSection(SeedOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<MetricsOptions>()
    .Bind(builder.Configuration.GetSection(MetricsOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<PrometheusOptions>()
    .Bind(builder.Configuration.GetSection(PrometheusOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "Prometheus:BaseUrl must be an absolute URL.")
    .ValidateOnStart();

builder.Services.AddSingleton<AppMetrics>();
builder.Services.AddHttpClient<IPrometheusMetricsService, PrometheusMetricsService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<PrometheusOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
    });

builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IFriendshipService, FriendshipService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<ChatBotHandler>();
builder.Services.AddSingleton<ChatSessionManager>();
builder.Services.AddSingleton<ChatBotManager>();
builder.Services.AddSingleton<ChatMessageProcessor>();
builder.Services.AddHostedService<ChatBackgroundService>();

builder.Services.AddSingleton<IChatBot, CensorshipBot>();
builder.Services.AddSingleton<IChatBot, HelpBot>();
builder.Services.AddSingleton<IChatBot, MemeBot>();

var app = builder.Build();

if (builder.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    await app.Services.MigrateDatabaseAsync<AppDbContext>(app.Logger);
}

var seedOptions = app.Services.GetRequiredService<IOptions<SeedOptions>>().Value;
await AdminSeeder.SeedAsync(app.Services, seedOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpMetrics();

app.UseMiddleware<RateLimitMiddleware>();

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/Error"),
    appBuilder => appBuilder.UseMiddleware<IpFilterMiddleWare>());

if (builder.Configuration.GetValue("Https:Redirect", true))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

var metricsToken = app.Services
    .GetRequiredService<IOptions<MetricsOptions>>()
    .Value
    .Token;

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/metrics"),
    branch =>
    {
        branch.Use(async (context, next) =>
        {
            if (string.IsNullOrWhiteSpace(metricsToken) ||
                context.Request.Headers.Authorization != $"Bearer {metricsToken}")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });
    });

app.MapMetrics("/metrics");

app.MapGet("/health", async (AppDbContext db) =>
{
    try
    {
        return await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "Healthy" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
