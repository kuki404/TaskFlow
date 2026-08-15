using System.Text;
using System.Threading.RateLimiting;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Middleware;
using TaskFlow.Application.Abstractions;
using TaskFlow.Application.Services;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure;
using TaskFlow.Infrastructure.Auth;
using TaskFlow.Infrastructure.Caching;
using TaskFlow.Infrastructure.Hubs;
using TaskFlow.Infrastructure.Identity;
using TaskFlow.Infrastructure.Multitenancy;
using TaskFlow.Infrastructure.Seed;
using TaskFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
// Plain AddDbContext, not AddDbContextPool: TaskFlowDbContext takes a scoped
// ICurrentTenantProvider (resolved per-request from the JWT claim), and pooling a context whose
// constructor depends on a scoped, request-derived service reliably fails DI callsite validation
// at startup ("Cannot resolve scoped service ... from root provider") — pooling's reuse-across-
// requests model doesn't mix with a dependency that must change every request.
var connectionString = SqlConnectionStringFactory.Build(builder.Configuration);
builder.Services.AddDbContext<TaskFlowDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
        // Query text (not parameter values) in logs/exceptions — off by default and never
        // enabled outside Development, since it can leak PII from bound parameter values.
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentTenantProvider, CurrentTenantProvider>();

// --- Identity (API-only: no cookie scheme, JWT is the sole authentication scheme) ---
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;

        // Brute-force protection: after 5 wrong passwords, the account is locked for 15 minutes —
        // enforced by SignInManager.CheckPasswordSignInAsync in AuthService, not by this config alone.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<TaskFlowDbContext>()
    .AddSignInManager();

// --- JWT authentication ---
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException(
        "Jwt:Secret is not configured. Set it with 'dotnet user-secrets set \"Jwt:Secret\" \"<a long random string>\" --project src/TaskFlow.Api'.");
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    // HMAC-SHA256 wants a >=256-bit key; a short secret would be brute-forceable offline from a
    // single captured token. Fail fast at startup rather than silently signing with a weak key.
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters (256 bits) long.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            // Pin the accepted algorithm — without this, a token forged with e.g. "alg: none" or
            // a different algorithm the key material also happens to validate under could pass.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // SignalR sends the JWT via a query string (browsers can't set WebSocket headers), so pull
        // "access_token" for the hub path specifically — everywhere else still requires the header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PolicyNames.ProjectViewer, policy => policy.AddRequirements(new ProjectRoleRequirement(ProjectRole.Viewer)))
    .AddPolicy(PolicyNames.ProjectMember, policy => policy.AddRequirements(new ProjectRoleRequirement(ProjectRole.Member)))
    .AddPolicy(PolicyNames.ProjectOwner, policy => policy.AddRequirements(new ProjectRoleRequirement(ProjectRole.Owner)))
    // Secure by default: any endpoint without an explicit [Authorize]/[AllowAnonymous] requires a
    // valid token. A new controller added later can't accidentally ship wide open.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddScoped<IAuthorizationHandler, ProjectRoleAuthorizationHandler>();

// --- Rate limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Configurable so integration tests can raise this limit — otherwise a fast test run
    // legitimately trips the same brute-force protection it's meant to test.
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 5);
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = authPermitLimit;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// --- Caching ---
builder.Services.AddHybridCache();
builder.Services.AddSingleton<BoardCache>();

// --- SignalR: one hub, BoardHub, broadcasting card/list mutations to everyone viewing that board. ---
builder.Services.AddSignalR();

// --- Application services (talk to TaskFlowDbContext directly — no repository layer) ---
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

// --- Error handling: every unhandled exception becomes RFC 9457 ProblemDetails, never a raw
// stack trace, in every environment (not just non-Development). ---
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// --- CORS: only the Web app's own origin may call this API from a browser. ---
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("WebClient", policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// --- Health checks: /health/live (process is up) vs /health/ready (DB reachable). Docker's
// healthcheck (docker-compose.yml) probes /health/ready. ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TaskFlowDbContext>(name: "database", tags: ["ready"]);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.RunAsync(scope.ServiceProvider);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

// Security headers on every response.
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

app.UseCors("WebClient");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BoardHub>("/hubs/board");

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();

// Exposed so WebApplicationFactory<Program> can be used from integration tests.
public partial class Program;
