namespace EmplekTo.Services.External
{
    /// <summary>
    /// Interface para el servicio de autenticación con Google
    /// </summary>
    public interface IGoogleAuthService
    {
        Task<GoogleUserInfo?> ValidateGoogleTokenAsync(string googleToken);
    }

    /// <summary>
    /// Información del usuario obtenida de Google
    /// </summary>
    public class GoogleUserInfo
    {
        public string GoogleId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? ProfilePicture { get; set; }
        public bool EmailVerified { get; set; }
    }
}
