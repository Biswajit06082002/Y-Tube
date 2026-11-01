namespace Auth.Entities.Contracts;

public sealed record SignUpRequest(string Email, string Password);

public sealed record SignUpResponse(string SignupId, int OtpExpiresInSeconds);

public sealed record VerifyOtpRequest(string SignupId, string Otp);

public sealed record ResendOtpRequest(string SignupId);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthTokensResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserSummary User);

public sealed record UserSummary(Guid Id, string Email);
