using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OT.Assessment.API.Authentication;

namespace OT.Assessment.API.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async Task<Results<Created<AuthenticationResponse>, Conflict<ProblemDetails>>> (
            RegisterAccountRequest request,
            IAuthenticationService authenticationService,
            CancellationToken cancellationToken) =>
        {
            var result = await authenticationService.RegisterAsync(request, cancellationToken);
            if (result is null)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Email or username is already registered."
                });
            }

            return TypedResults.Created("/api/auth/me", result);
        })
            .RequireRateLimiting("auth")
            .WithName("RegisterAccount")
            .WithSummary("Register a player account")
            .WithDescription("Creates an account and returns a bearer token with player wager permissions.")
            .Produces<AuthenticationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/api/auth/login", async Task<Results<Ok<AuthenticationResponse>, ProblemHttpResult>> (
            LoginRequest request,
            IAuthenticationService authenticationService,
            CancellationToken cancellationToken) =>
        {
            var result = await authenticationService.LoginAsync(request, cancellationToken);
            return result is null
                ? TypedResults.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid email or password.")
                : TypedResults.Ok(result);
        })
            .RequireRateLimiting("auth")
            .WithName("Login")
            .WithSummary("Sign in to a player account")
            .WithDescription("Validates account credentials and returns a bearer token.")
            .Produces<AuthenticationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
                TypedResults.Ok(new AccountResponse(
                    Guid.Parse(user.FindFirstValue("sub")!),
                    user.FindFirstValue("email")!,
                    user.FindFirstValue("name")!)))
            .RequireAuthorization()
            .WithName("GetCurrentAccount")
            .WithSummary("Get the authenticated account")
            .Produces<AccountResponse>(StatusCodes.Status200OK);
    }
}
