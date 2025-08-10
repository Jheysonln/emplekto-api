using EmplekTo.DTOs.Auth;
using EmplekTo.DTOs.Common;

namespace EmplekTo.Services
{
    /// <summary>
    /// Interface para el servicio de autenticación
    /// </summary>
    public interface IAuthService
    {
        Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, string? ipAddress = null);
        Task<ApiResponse<AuthResponseDto>> LoginWithGoogleAsync(GoogleLoginRequestDto request, string? ipAddress = null);
        Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request);
        Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<ApiResponse<bool>> LogoutAsync(string refreshToken);
        Task<ApiResponse<bool>> RevokeAllTokensAsync(int userId);
    }
}
