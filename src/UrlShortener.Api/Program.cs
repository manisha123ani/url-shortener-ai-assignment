using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Endpoints;
using UrlShortener.Api.Middleware;
using UrlShortener.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=urlshortener.db";

// ---- Services ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IShortCodeGenerator, Base62ShortCodeGenerator>();
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "URL Shortener API", Version = "v1" });
});

// Fixed-window rate limiting: protects the create endpoint from abuse/spam.
// A token-bucket or sliding-window limiter (or Azure APIM policy) would replace this in production.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("create-policy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("redirect-policy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 300;
        opt.QueueLimit = 0;
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// ---- Migrate/create DB on startup (prototype convenience; production uses managed migrations) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ---- Middleware pipeline ----
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI();

app.MapUrlEndpoints();
app.MapHealthChecks("/health");

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
