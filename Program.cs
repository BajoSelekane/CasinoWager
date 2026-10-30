using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OT.Assessment.Application.Commands;
using OT.Assessment.Application.Requests;
using OT.Assessment.Application.Interfaces;
using OT.Assessment.Infrastructure.Data;
using OT.Assessment.Infrastructure.OutBox;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var jwtKey = builder.Configuration["Jwt:Key"] ;
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => { o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30) }; 
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("WagerWrite", p => p.RequireAuthenticatedUser()
    .RequireClaim("scope", "casino.wager.write"));
    o.AddPolicy("WagerRead", p => p.RequireAuthenticatedUser()
    .RequireClaim("scope", "casino.wager.read"));
    o.AddPolicy("Reporting", p => p.RequireAuthenticatedUser()
    .RequireRole("Admin", "RiskAnalyst"));
});

builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("wagers", x =>
{
    x.PermitLimit = 60; 
    x.Window = TimeSpan.FromMinutes(1);
    x.QueueLimit = 0; 
}));

builder.Services.AddScoped<WagerRepository>();
builder.Services.AddScoped<IWagerRepository>(sp => sp.GetRequiredService<WagerRepository>());
builder.Services.AddScoped<IOutboxWriter>(sp => sp.GetRequiredService<WagerRepository>());
builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(CreateWagerCommand).Assembly));
builder.Services.AddHostedService(sp => new OutboxPublisher(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<ILogger<OutboxPublisher>>(),
    builder.Configuration["RabbitMq:Host"] ?? "localhost"));


var app = builder.Build();
app.UseRateLimiter(); 
app.UseAuthentication();
app.UseAuthorization();
//Minimal API endpoints
app.MapPost("/api/player/casinowager", async (CreateWagerRequest r, HttpContext http, ISender sender, CancellationToken ct) => {
    if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var subject) || subject != r.AccountId) return Results.Forbid();
    var result = await sender.Send(new CreateWagerCommand
        (r.WagerId,
        r.Theme,
        r.Provider,
        r.GameName, 
        r.TransactionId, 
        r.BrandId,
        r.AccountId,
        r.Username,
        r.ExternalReferenceId,
        r.TransactionTypeId,
        r.Amount,
        r.CreatedDateTime,
        r.NumberOfBets,
        r.CountryCode,
        r.SessionData,
        r.Duration),
        ct);
    return Results.Accepted($"/api/player/{r.AccountId}/casino", new { wagerId = result, status = "accepted" });
}).RequireAuthorization("WagerWrite").RequireRateLimiting("wagers");


app.MapGet("/api/player/{playerId:guid}/casino", async (Guid playerId, int? page, int? pageSize, HttpContext http, ISender sender, CancellationToken ct) => {
    if (!Guid.TryParse(http.User.FindFirstValue("sub"), out var subject) || subject != playerId) return Results.Forbid();
    var result = await sender.Send(new GetPlayerWagersQuery(playerId, page ?? 1, pageSize ?? 10), ct); return Results.Ok(result);
}).RequireAuthorization("WagerRead");

app.MapGet("/api/player/topSpenders", async (int? count, ISender sender, CancellationToken ct) => Results.Ok(await sender.Send(new GetTopSpendersQuery(count ?? 10), ct))).RequireAuthorization("Reporting");
//app.MapHealthChecks("/health").AllowAnonymous();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();

}

app.UseHttpsRedirection();


app.UseAuthorization();

app.MapControllers();

app.Run();
