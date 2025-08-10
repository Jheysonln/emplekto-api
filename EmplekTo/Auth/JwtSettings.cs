namespace EmplekTo.Auth
{
    /// <summary>
    /// Configuraciones para JWT Token
    /// </summary>
    public class JwtSettings
    {
        public const string SectionName = "JwtSettings";

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int AccessTokenExpirationMinutes { get; set; } = 60; // 1 hora
        public int RefreshTokenExpirationDays { get; set; } = 7;    // 7 días
    }

    /// <summary>
    /// Configuraciones para Google OAuth
    /// </summary>
    public class GoogleAuthSettings
    {
        public const string SectionName = "GoogleAuth";

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
