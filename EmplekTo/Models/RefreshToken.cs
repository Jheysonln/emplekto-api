namespace EmplekTo.Models
{
    /// <summary>
    /// Entidad para manejar refresh tokens
    /// </summary>
    public class RefreshToken
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? DeviceInfo { get; set; } // Información del dispositivo para seguridad
        public string? IpAddress { get; set; }   // IP de donde se generó el token
    }
}
