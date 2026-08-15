using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Dtos;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(8), MaxLength(100)] string Password,
    [Required, MaxLength(100)] string DisplayName,
    [Required, MaxLength(200)] string TenantName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string DisplayName,
    Guid TenantId);
