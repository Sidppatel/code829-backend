namespace Contracts.DTOs.Auth;

public record SignupRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password);

public record SigninRequest(string Email, string Password);

public record VerifyEmailRequest(string Token);

public record SignupResponse(string Message);
