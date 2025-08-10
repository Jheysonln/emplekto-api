using EmplekTo.Models;

namespace EmplekTo.DTOs.Auth
{
    /// <summary>
    /// DTO para representar información del usuario (sin datos sensibles)
    /// </summary>
    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
        public string? PhoneNumber { get; set; }
        public string? ProfilePicture { get; set; }

        // INFORMACIÓN DE ROLES NORMALIZADA
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string RoleDisplayName { get; set; } = string.Empty;

        // INFORMACIÓN DE AUTH PROVIDER
        public int AuthProviderId { get; set; }
        public string AuthProviderName { get; set; } = string.Empty;

        public string? ExternalId { get; set; }
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }
}
