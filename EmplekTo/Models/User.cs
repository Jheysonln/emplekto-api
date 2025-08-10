namespace EmplekTo.Models
{
    /// <summary>
    /// Entidad Usuario normalizada que usa tablas de referencia
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; } // Nullable para usuarios OAuth
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? ProfilePicture { get; set; }

        // RELACIONES NORMALIZADAS
        public int RoleId { get; set; } // FK a tabla Roles
        public int AuthProviderId { get; set; } // FK a tabla AuthProviders

        // Información OAuth
        public string? ExternalId { get; set; } // GoogleId, etc.

        // Estado
        public bool IsActive { get; set; } = true;
        public bool EmailConfirmed { get; set; } = false;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        // PROPIEDADES DE NAVEGACIÓN (para el frontend)
        public string RoleName { get; set; } = string.Empty; // Viene del JOIN en SP
        public string RoleDisplayName { get; set; } = string.Empty;
        public string AuthProviderName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Entidad Role normalizada
    /// </summary>
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // JobSeeker, Employer, Admin, Moderator
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Entidad AuthProvider normalizada
    /// </summary>
    public class AuthProvider
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // Local, Google
        public string DisplayName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Constantes para nombres de roles (para validaciones)
    /// </summary>
    public static class RoleNames
    {
        public const string JobSeeker = "JobSeeker";
        public const string Employer = "Employer";
        public const string Admin = "Admin";
        public const string Moderator = "Moderator";
    }

    /// <summary>
    /// Constantes para proveedores de auth
    /// </summary>
    public static class AuthProviderNames
    {
        public const string Local = "Local";
        public const string Google = "Google";
    }

}