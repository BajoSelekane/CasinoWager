using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MediatR;
using OT.Assessment.API.Endpoints;
using OT.Assessment.Application.Commands;
using OT.Assessment.Application.Interfaces;
using OT.Assessment.API.Authentication;
using OT.Assessment.Infrastructure.Data;
using OT.Assessment.Infrastructure.OutBox;
using OT.Assessment.Domain.Models;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var enableAnonymousWagerLoadTesting =
    builder.Configuration.GetValue<bool>("DevelopmentFeatures:EnableAnonymousWagerLoadTesting");

if (enableAnonymousWagerLoadTesting && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Anonymous wager load testing can only be enabled in the Development environment.");
}

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Set Jwt:Key.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Set Jwt:Issuer.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Set Jwt:Audience.");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Set ConnectionStrings:DefaultConnection.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name"
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("WagerWrite", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("scope", "casino.wager.write"))
    .AddPolicy("WagerRead", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("scope", "casino.wager.read"))
    .AddPolicy("Reporting", policy =>
        policy.RequireAuthenticatedUser().RequireRole("Admin", "RiskAnalyst"));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddFixedWindowLimiter("wagers", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.AddPolicy("wager-load-test", context =>
        RateLimitPartition.GetConcurrencyLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = 80,
                QueueLimit = 7000,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

builder.Services.AddDbContextPool<AppDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<WagerRepository>();
builder.Services.AddScoped<IWagerRepository>(services => services.GetRequiredService<WagerRepository>());
builder.Services.AddScoped<IOutboxWriter>(services => services.GetRequiredService<WagerRepository>());
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(typeof(CreateWagerCommand).Assembly));
builder.Services.AddHostedService(services => new OutboxPublisher(
    services.GetRequiredService<IServiceScopeFactory>(),
    services.GetRequiredService<ILogger<OutboxPublisher>>(),
    builder.Configuration["RabbitMq:Host"] ?? "localhost",
    builder.Configuration["RabbitMq:UserName"] ?? "guest",
    builder.Configuration["RabbitMq:Password"] ?? "guest",
    builder.Configuration["RabbitMq:Queue"] ?? "casino.wagers"));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter a bearer token returned by /api/auth/register or /api/auth/login."
    });
    options.OperationFilter<BearerSecurityOperationFilter>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthenticationEndpoints();
app.MapWagerEndpoints(enableAnonymousWagerLoadTesting);
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
