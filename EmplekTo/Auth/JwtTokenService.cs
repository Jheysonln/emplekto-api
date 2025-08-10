using EmplekTo.Data.Repositories;
using EmplekTo.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EmplekTo.Auth
{
    /// <summary>
    /// Servicio para manejo de tokens JWT y refresh tokens
    /// </summary>
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IAuthRepository _authRepository;
        private readonly ILogger<JwtTokenService> _logger;

        public JwtTokenService(
            IOptions<JwtSettings> jwtSettings,
            IAuthRepository authRepository,
            ILogger<JwtTokenService> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _authRepository = authRepository;
            _logger = logger;
        }

        /// <summary>
        /// Genera un access token JWT para el usuario
        /// </summary>
        public async Task<string> GenerateAccessTokenAsync(User user)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

                // Claims del usuario - ACTUALIZADO para arquitectura normalizada
                var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Role, user.RoleName), // CAMBIAR: user.RoleName
            new("auth_provider", user.AuthProviderName), // CAMBIAR: user.AuthProviderName
            new("email_confirmed", user.EmailConfirmed.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

                // Agregar claim específico de Google si es necesario
                if (!string.IsNullOrEmpty(user.ExternalId)) // CAMBIAR: user.ExternalId
                {
                    claims.Add(new Claim("external_id", user.ExternalId)); // CAMBIAR: external_id
                }

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                    Issuer = _jwtSettings.Issuer,
                    Audience = _jwtSettings.Audience,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                _logger.LogInformation("Access token generated for user {UserId}", user.Id);
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating access token for user {UserId}", user.Id);
                throw;
            }
        }


        public async Task<string> GenerateRefreshTokenAsync()
        {
            try
            {
                var randomBytes = new byte[32];
                using var rng = RandomNumberGenerator.Create();
                rng.GetBytes(randomBytes);

                var token = Convert.ToBase64String(randomBytes);
                _logger.LogDebug("Refresh token generated");

                return await Task.FromResult(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating refresh token");
                throw;
            }
        }

        /// <summary>
        /// Valida un access token JWT
        /// </summary>
        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return await Task.FromResult(principal);
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning("Token validation failed: {Message}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return null;
            }
        }

        /// <summary>
        /// Guarda un refresh token en la base de datos
        /// </summary>
        public async Task<bool> SaveRefreshTokenAsync(string token, int userId, string? deviceInfo = null, string? ipAddress = null)
        {
            try
            {
                var expiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
                var result = await _authRepository.CreateRefreshTokenAsync(token, userId, expiresAt, deviceInfo, ipAddress);

                _logger.LogInformation("Refresh token saved for user {UserId}", userId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving refresh token for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Valida un refresh token
        /// </summary>
        public async Task<bool> ValidateRefreshTokenAsync(string token)
        {
            try
            {
                var refreshToken = await _authRepository.ValidateRefreshTokenAsync(token);
                var isValid = refreshToken != null;

                _logger.LogDebug("Refresh token validation result: {IsValid}", isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating refresh token");
                return false;
            }
        }

        /// <summary>
        /// Revoca un refresh token
        /// </summary>
        public async Task<bool> RevokeRefreshTokenAsync(string token)
        {
            try
            {
                var result = await _authRepository.RevokeRefreshTokenAsync(token);
                _logger.LogInformation("Refresh token revoked");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token");
                throw;
            }
        }

        /// <summary>
        /// Obtiene el usuario asociado a un refresh token
        /// </summary>
        public async Task<User?> GetUserFromRefreshTokenAsync(string token)
        {
            try
            {
                var refreshToken = await _authRepository.ValidateRefreshTokenAsync(token);
                if (refreshToken != null)
                {
                    var user = await _authRepository.GetUserByIdAsync(refreshToken.UserId);
                    _logger.LogDebug("User retrieved from refresh token");
                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user from refresh token");
                return null;
            }
        }
    }

}