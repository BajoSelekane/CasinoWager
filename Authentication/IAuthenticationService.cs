namespace OT.Assessment.API.Authentication;

public interface IAuthenticationService
{
    Task<AuthenticationResponse?> RegisterAsync(
        RegisterAccountRequest request,
        CancellationToken cancellationToken);

    Task<AuthenticationResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken);
}
