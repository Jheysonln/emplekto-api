using EmplekTo.Models;
using System.ComponentModel.DataAnnotations;

namespace EmplekTo.DTOs.Auth
{
    /// <summary>
    /// DTO para la solicitud de login tradicional (email/password)
    /// </summary>
    public class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }

    /// <summary>
    /// DTO para la solicitud de login con Google OAuth
    /// </summary>
    public class GoogleLoginRequestDto
    {
        [Required]
        public string GoogleToken { get; set; } = string.Empty;
        public bool RememberMe { get; set; } = false;
    }

    /// <summary>
    /// DTO para la respuesta de autenticación exitosa
    /// </summary>
    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = new();
    }

    /// <summary>
    /// DTO para solicitud de refresh token
    /// </summary>
    public class RefreshTokenRequestDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para solicitud de registro de usuario
    /// </summary>
    public class RegisterRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        [Required]
        public string RoleName { get; set; } = RoleNames.JobSeeker; // Ahora usa string en lugar de enum
    }
}
