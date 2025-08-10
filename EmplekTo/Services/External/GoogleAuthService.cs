using EmplekTo.Auth;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EmplekTo.Services.External
{
    /// <summary>
    /// Servicio para validar tokens de Google OAuth
    /// </summary>
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly GoogleAuthSettings _googleSettings;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(
            HttpClient httpClient,
            IOptions<GoogleAuthSettings> googleSettings,
            ILogger<GoogleAuthService> logger)
        {
            _httpClient = httpClient;
            _googleSettings = googleSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Valida un token de Google y obtiene la información del usuario
        /// </summary>
        public async Task<GoogleUserInfo?> ValidateGoogleTokenAsync(string googleToken)
        {
            try
            {
                // URL para validar el token con Google
                var url = $"https://oauth2.googleapis.com/tokeninfo?access_token={googleToken}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Google token validation failed with status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var googleResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (googleResponse == null)
                {
                    _logger.LogWarning("Failed to parse Google token response");
                    return null;
                }

                // Verificar que el token sea para nuestra aplicación
                if (googleResponse.Aud != _googleSettings.ClientId)
                {
                    _logger.LogWarning("Google token audience mismatch");
                    return null;
                }

                // Obtener información adicional del perfil del usuario
                var userInfo = await GetGoogleUserInfoAsync(googleToken);
                if (userInfo != null)
                {
                    _logger.LogInformation("Google token validated for user: {Email}", userInfo.Email);
                    return userInfo;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Google token");
                return null;
            }
        }

        /// <summary>
        /// Obtiene información detallada del usuario desde Google
        /// </summary>
        private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string accessToken)
        {
            try
            {
                var url = $"https://www.googleapis.com/oauth2/v2/userinfo?access_token={accessToken}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get Google user info: {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var userResponse = JsonSerializer.Deserialize<GoogleUserResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (userResponse == null)
                {
                    return null;
                }

                return new GoogleUserInfo
                {
                    GoogleId = userResponse.Id ?? string.Empty,
                    Email = userResponse.Email ?? string.Empty,
                    FirstName = userResponse.GivenName ?? string.Empty,
                    LastName = userResponse.FamilyName ?? string.Empty,
                    ProfilePicture = userResponse.Picture,
                    EmailVerified = userResponse.VerifiedEmail ?? false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Google user info");
                return null;
            }
        }
    }

    /// <summary>
    /// Respuesta de validación de token de Google
    /// </summary>
    internal class GoogleTokenResponse
    {
        public string? Aud { get; set; }  // Audience (Client ID)
        public string? Exp { get; set; }  // Expiration time
        public string? Iat { get; set; }  // Issued at
        public string? Iss { get; set; }  // Issuer
        public string? Sub { get; set; }  // Subject (User ID)
    }

    /// <summary>
    /// Respuesta de información de usuario de Google
    /// </summary>
    internal class GoogleUserResponse
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public bool? VerifiedEmail { get; set; }
        public string? Name { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? Picture { get; set; }
        public string? Locale { get; set; }
    }
}
