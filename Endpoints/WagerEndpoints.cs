using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using OT.Assessment.Application.Commands;
using OT.Assessment.Application.Interfaces;
using OT.Assessment.Application.Requests;

namespace OT.Assessment.API.Endpoints;

public static class WagerEndpoints
{
    public static void MapWagerEndpoints(this WebApplication app)
    {
        app.MapPost("/api/player/casinowager", async Task<Results<Accepted<WagerAcceptedResponse>, ForbidHttpResult>> (
            CreateWagerRequest request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(GetSubject(httpContext.User), out var subject)
                || subject != request.AccountId
                || !string.Equals(
                    httpContext.User.FindFirstValue("name"),
                    request.Username,
                    StringComparison.Ordinal))
            {
                return TypedResults.Forbid();
            }

            var wagerId = await sender.Send(new CreateWagerCommand(
                request.WagerId,
                request.Theme,
                request.Provider,
                request.GameName,
                request.TransactionId,
                request.BrandId,
                request.AccountId,
                request.Username,
                request.ExternalReferenceId,
                request.TransactionTypeId,
                request.Amount,
                request.CreatedDateTime,
                request.NumberOfBets,
                request.CountryCode,
                request.SessionData,
                request.Duration), cancellationToken);

            return TypedResults.Accepted(
                $"/api/player/{request.AccountId}/casino",
                new WagerAcceptedResponse(wagerId, "accepted"));
        })
            .RequireAuthorization("WagerWrite")
            .RequireRateLimiting("wagers")
            .WithName("CreateCasinoWager")
            .WithSummary("Submit a casino wager")
            .WithDescription("Accepts a wager for the authenticated player and queues it for publication.")
            .Produces<WagerAcceptedResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/api/player/{playerId:guid}/casino", async Task<Results<Ok<PagedResult<WagerDto>>, ForbidHttpResult>> (
            Guid playerId,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(GetSubject(httpContext.User), out var subject) || subject != playerId)
            {
                return TypedResults.Forbid();
            }

            var result = await sender.Send(
                new GetPlayerWagersQuery(playerId, page ?? 1, pageSize ?? 10),
                cancellationToken);
            return TypedResults.Ok(result);
        })
            .RequireAuthorization("WagerRead")
            .WithName("GetPlayerCasinoWagers")
            .WithSummary("Get a player's casino wagers")
            .WithDescription("Returns a page of wagers belonging to the authenticated player.")
            .Produces(StatusCodes.Status403Forbidden);

        app.MapGet("/api/player/topSpenders", async (
            int? count,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetTopSpendersQuery(count ?? 10), cancellationToken);
            return TypedResults.Ok(result);
        })
            .RequireAuthorization("Reporting")
            .WithName("GetTopSpenders")
            .WithSummary("Get top casino spenders")
            .WithDescription("Returns the highest-spending accounts for reporting.")
            .Produces(StatusCodes.Status200OK);
    }

    private static string? GetSubject(ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
}

/// <summary>Response returned after a wager has been accepted for processing.</summary>
public sealed record WagerAcceptedResponse(Guid WagerId, string Status);
