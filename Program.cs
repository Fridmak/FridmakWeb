using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TestingAppWeb.Data;
using TestingAppWeb.Interfaces;
using TestingAppWeb.Middleware;
using TestingAppWeb.MiddleWare;
using TestingAppWeb.Services;
using TestingAppWeb.Services.Chat;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/User/Login";
        options.AccessDeniedPath = "/User/AccessDenied";
    });
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IHomeService, HomeService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<ChatHandlerManager>();
builder.Services.AddSingleton<ChatServer>();
builder.Services.AddHostedService<ChatBackgroundService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseMiddleware<RateLimitMiddleware>(); // custom

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/Error"),
    appBuilder => appBuilder.UseMiddleware<IpFilterMiddleWare>() // custom
);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
