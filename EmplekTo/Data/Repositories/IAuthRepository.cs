using EmplekTo.Models;

namespace EmplekTo.Data.Repositories
{
    /// <summary>
    /// Interface para operaciones de autenticación en la base de datos
    /// </summary>
    public interface IAuthRepository
    {
        Task<User?> LoginUserAsync(string email);
        Task<User?> LoginWithGoogleAsync(string email, string googleId, string firstName, string lastName, string? profilePicture = null);

        // CAMBIAR ESTE MÉTODO - usar string en lugar de UserRole
        Task<User?> RegisterUserAsync(string email, string passwordHash, string firstName, string lastName, string? phoneNumber = null, string roleName = "JobSeeker");

        Task<User?> GetUserByIdAsync(int id);
        Task<bool> CreateRefreshTokenAsync(string token, int userId, DateTime expiresAt, string? deviceInfo = null, string? ipAddress = null);
        Task<RefreshTokenInfo?> ValidateRefreshTokenAsync(string token);
        Task<bool> RevokeRefreshTokenAsync(string token);
    }

    /// <summary>
    /// Información del refresh token con datos del usuario
    /// </summary>
    public class RefreshTokenInfo
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty; // CAMBIAR a string
        public bool IsActive { get; set; }
    }
}
