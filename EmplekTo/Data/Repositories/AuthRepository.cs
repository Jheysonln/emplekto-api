using Dapper;
using EmplekTo.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EmplekTo.Data.Repositories
{
    /// <summary>
    /// Implementación del repositorio de autenticación usando Dapper
    /// </summary>
    public class AuthRepository : IAuthRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<AuthRepository> _logger;

        public AuthRepository(IConfiguration configuration, ILogger<AuthRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
            _logger = logger;
        }
        /// <summary>
        /// Obtiene usuario por email para login tradicional
        /// </summary>
        public async Task<User?> LoginUserAsync(string email)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SP_Auth_LoginUser",
                    new { Email = email, IpAddress = GetClientIpAddress() },
                    commandType: CommandType.StoredProcedure
                );

                _logger.LogInformation("Login attempt for email: {Email}", email);
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", email);
                throw;
            }
        }


        /// <summary>
        /// Login o registro con Google OAuth
        /// </summary>
        public async Task<User?> LoginWithGoogleAsync(string email, string googleId, string firstName, string lastName, string? profilePicture = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new
                {
                    Email = email,
                    GoogleId = googleId,
                    FirstName = firstName,
                    LastName = lastName,
                    ProfilePicture = profilePicture,
                    IpAddress = GetClientIpAddress()
                };

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SP_Auth_LoginGoogle",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                if (user != null)
                {
                    var isNewUser = user.CreatedAt > DateTime.UtcNow.AddMinutes(-1); // Si se creó hace menos de 1 minuto

                    _logger.LogInformation("Google {Action} for email: {Email}",
                        isNewUser ? "registration" : "login", email);

                    return user;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login for email: {Email}", email);
                throw;
            }
        }

        /// <summary>
        /// Registra un nuevo usuario con email/password
        /// </summary>
        public async Task<User?> RegisterUserAsync(string email, string passwordHash, string firstName, string lastName, string? phoneNumber = null, string roleName = RoleNames.JobSeeker)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    RoleName = roleName
                };

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SP_Auth_RegisterUser",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                _logger.LogInformation("User registered with email: {Email}, role: {Role}", email, roleName);
                return user;
            }
            catch (SqlException ex) when (ex.Message.Contains("El email ya está registrado"))
            {
                _logger.LogWarning("Registration attempt with existing email: {Email}", email);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", email);
                throw;
            }
        }


        /// <summary>
        /// Obtiene usuario por ID
        /// </summary>
        public async Task<User?> GetUserByIdAsync(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SP_User_GetById",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure
                );

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", id);
                throw;
            }
        }

        /// <summary>
        /// Crea un nuevo refresh token
        /// </summary>
        public async Task<bool> CreateRefreshTokenAsync(string token, int userId, DateTime expiresAt, string? deviceInfo = null, string? ipAddress = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var parameters = new
                {
                    Token = token,
                    UserId = userId,
                    ExpiresAt = expiresAt,
                    DeviceInfo = deviceInfo,
                    IpAddress = ipAddress ?? GetClientIpAddress()
                };

                var result = await connection.QueryFirstOrDefaultAsync<int>(
                    "SP_RefreshToken_Create",
                    parameters,
                    commandType: CommandType.StoredProcedure
                );

                var success = result > 0;
                _logger.LogInformation("Refresh token created for user {UserId}: {Success}", userId, success);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating refresh token for user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Valida un refresh token y obtiene información del usuario
        /// </summary>
        public async Task<RefreshTokenInfo?> ValidateRefreshTokenAsync(string token)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var refreshTokenInfo = await connection.QueryFirstOrDefaultAsync<RefreshTokenInfo>(
                    "SP_RefreshToken_Validate",
                    new { Token = token },
                    commandType: CommandType.StoredProcedure
                );

                var isValid = refreshTokenInfo != null;
                _logger.LogDebug("Refresh token validation: {IsValid}", isValid);

                return refreshTokenInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating refresh token");
                throw;
            }
        }

        /// <summary>
        /// Revoca un refresh token
        /// </summary>
        public async Task<bool> RevokeRefreshTokenAsync(string token)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var result = await connection.QueryFirstOrDefaultAsync<int>(
                    "SP_RefreshToken_Revoke",
                    new { Token = token },
                    commandType: CommandType.StoredProcedure
                );

                var success = result > 0;
                _logger.LogInformation("Refresh token revoked: {Success}", success);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking refresh token");
                throw;
            }
        }

        /// <summary>
        /// Mapea un objeto dynamic a User (helper para resultados de SP)
        /// </summary>
        private static User MapDynamicToUser(dynamic result)
        {
            return new User
            {
                Id = result.Id,
                Email = result.Email,
                PasswordHash = result.PasswordHash,
                FirstName = result.FirstName,
                LastName = result.LastName,
                PhoneNumber = result.PhoneNumber,
                ProfilePicture = result.ProfilePicture,

                // CAMPOS NORMALIZADOS - IDs
                RoleId = result.RoleId,
                AuthProviderId = result.AuthProviderId,

                // CAMPOS NORMALIZADOS - Nombres (vienen del JOIN en SP)
                RoleName = result.RoleName,
                RoleDisplayName = result.RoleDisplayName,
                AuthProviderName = result.AuthProviderName,

                // OAUTH DATA
                ExternalId = result.ExternalId, // CAMBIAR: de GoogleId a ExternalId

                // RESTO IGUAL
                IsActive = result.IsActive,
                EmailConfirmed = result.EmailConfirmed,
                CreatedAt = result.CreatedAt,
                UpdatedAt = result.UpdatedAt,
                LastLoginAt = result.LastLoginAt
            };
        }

        /// <summary>
        /// Obtiene la IP del cliente (simplificado para el ejemplo)
        /// En producción, se obtendría del HttpContext
        /// </summary>
        private static string GetClientIpAddress()
        {
            return "127.0.0.1"; // Simplificado para el ejemplo
        }
    }
}
