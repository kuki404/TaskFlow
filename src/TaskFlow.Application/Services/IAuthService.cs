using TaskFlow.Application.Common;
using TaskFlow.Application.Dtos;

namespace TaskFlow.Application.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string rawRefreshToken, CancellationToken cancellationToken = default);
}
