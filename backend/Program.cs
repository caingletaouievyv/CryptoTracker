using System.Text;
using System.Text.Json;
using CryptoTracker.Data;
using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using CryptoTracker.Middleware;
using CryptoTracker.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CryptoTracker API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT from POST /api/auth/login or /api/auth/register",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var parts = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m));
        var msg = string.Join("; ", parts);
        if (string.IsNullOrWhiteSpace(msg))
            msg = "Invalid request.";
        return new BadRequestObjectResult(ApiResponse<object?>.Fail(msg));
    };
});

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");
var usePostgres = IsPostgresConnection(conn);

if (!usePostgres && !string.IsNullOrEmpty(conn) && conn.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    var name = conn.TrimStart().Substring("Data Source=".Length).Trim();
    if (name.Length > 0 && !Path.IsPathRooted(name))
    {
        var dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, name));
        conn = "Data Source=" + dbPath;
    }
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
        options.UseNpgsql(conn);
    else
        options.UseSqlite(conn ?? "Data Source=CryptoTracker.db");
});

var jwtKey = builder.Configuration["Jwt:SigningKey"] ?? "";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "CryptoTracker";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "CryptoTracker";
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters (set in appsettings or environment).");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("PriceProvider", (sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var ua = config["PriceProvider:UserAgent"] ?? "CryptoTracker/1.0";
    client.DefaultRequestHeaders.UserAgent.ParseAdd(ua);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    var cgKey = config["PriceProvider:CoinGeckoApiKey"];
    if (!string.IsNullOrWhiteSpace(cgKey))
        client.DefaultRequestHeaders.Add("x-cg-demo-api-key", cgKey);
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IPriceService, PriceService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IHoldingService, HoldingService>();
builder.Services.AddScoped<IOkxService, OkxService>();
builder.Services.AddScoped<IOkxSyncService, OkxSyncService>();
builder.Services.AddScoped<IBinanceService, BinanceService>();
builder.Services.AddScoped<IBinanceSyncService, BinanceSyncService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var raw = builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173";
        var origins = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (origins.Length == 0)
            origins = ["http://localhost:5173"];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        if (usePostgres)
        {
            db.Database.EnsureCreated();
            logger.LogInformation("PostgreSQL schema ensured.");
        }
        else
        {
            var path = db.Database.GetDbConnection().DataSource;
            logger.LogInformation("Database: {Path}", path);
            db.Database.Migrate();
            logger.LogInformation("Migrations applied.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database setup failed.");
        throw;
    }
}

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static bool IsPostgresConnection(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString)) return false;
    var c = connectionString.Trim();
    return c.StartsWith("Host=", StringComparison.OrdinalIgnoreCase)
        || c.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || c.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
}
