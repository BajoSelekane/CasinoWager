using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OT.Assessment.Domain.Models;
using OT.Assessment.Infrastructure.Data;

namespace OT.Assessment.API.Authentication;

public sealed class AuthenticationService(
    AppDbContext db,
    IPasswordHasher<UserAccount> passwordHasher,
    IConfiguration configuration) : IAuthenticationService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<AuthenticationResponse?> RegisterAsync(
        RegisterAccountRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var username = request.Username.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var normalizedUsername = username.ToUpperInvariant();

        if (await db.UserAccounts.AnyAsync(
                account => account.NormalizedEmail == normalizedEmail
                    || account.NormalizedUsername == normalizedUsername,
                cancellationToken))
        {
            return null;
        }

        var account = new UserAccount(email, normalizedEmail, username, normalizedUsername);
        account.SetPasswordHash(passwordHasher.HashPassword(account, request.Password));
        db.UserAccounts.Add(account);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return null;
        }

        return CreateAuthenticationResponse(account);
    }

    public async Task<AuthenticationResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var account = await db.UserAccounts.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (account is null)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            account,
            account.PasswordHash,
            request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.SetPasswordHash(passwordHasher.HashPassword(account, request.Password));
            await db.SaveChangesAsync(cancellationToken);
        }

        return CreateAuthenticationResponse(account);
    }

    private AuthenticationResponse CreateAuthenticationResponse(UserAccount account)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Set Jwt:Issuer.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Set Jwt:Audience.");
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Set Jwt:Key.");
        var expiresAt = DateTimeOffset.UtcNow.Add(TokenLifetime);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
                new Claim("name", account.Username),
                new Claim(JwtRegisteredClaimNames.Email, account.Email),
                new Claim("scope", "casino.wager.write"),
                new Claim("scope", "casino.wager.read")
            ],
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AuthenticationResponse(
            account.Id,
            account.Email,
            account.Username,
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }
}
