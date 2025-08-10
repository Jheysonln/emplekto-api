using System.ComponentModel.DataAnnotations;

namespace EmplekTo.DTOs
{
    /// <summary>
    /// DTO para actualizar información del usuario
    /// </summary>
    public class UpdateUserRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string LastName { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [Url]
        [StringLength(500)]
        public string? ProfilePicture { get; set; }
    }
}
