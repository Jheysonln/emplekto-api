using EmplekTo.Models;
using System.Security.Claims;

namespace EmplekTo.Auth
{
    /// <summary>
    /// Interface para el servicio de tokens JWT
    /// </summary>
    public interface IJwtTokenService
    {
        Task<string> GenerateAccessTokenAsync(User user);
        Task<string> GenerateRefreshTokenAsync();
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
        Task<bool> SaveRefreshTokenAsync(string token, int userId, string? deviceInfo = null, string? ipAddress = null);
        Task<bool> ValidateRefreshTokenAsync(string token);
        Task<bool> RevokeRefreshTokenAsync(string token);
        Task<User?> GetUserFromRefreshTokenAsync(string token);
    }
}
