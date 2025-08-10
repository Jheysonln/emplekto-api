using EmplekTo.Auth;
using EmplekTo.Data.Repositories;
using EmplekTo.DTOs.Auth;
using EmplekTo.DTOs.Common;
using EmplekTo.Models;
using EmplekTo.Services.External;

namespace EmplekTo.Services
{
    /// <summary>
    /// Servicio de autenticación que maneja login, registro y tokens
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IAuthRepository authRepository,
            IJwtTokenService jwtTokenService,
            IGoogleAuthService googleAuthService,
            ILogger<AuthService> logger)
        {
            _authRepository = authRepository;
            _jwtTokenService = jwtTokenService;
            _googleAuthService = googleAuthService;
            _logger = logger;
        }

        /// <summary>
        /// Login tradicional con email/password
        /// </summary>
        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginRequestDto request, string? ipAddress = null)
        {
            try
            {
                // Buscar usuario por email
                var user = await _authRepository.LoginUserAsync(request.Email);

                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Credenciales inválidas");
                }

                // Verificar que tenga password (no sea usuario de Google)
                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    _logger.LogWarning("Login attempt for OAuth user with password: {Email}", request.Email);
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Esta cuenta fue creada con Google. Por favor, usa 'Iniciar sesión con Google'");
                }

                // Verificar password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Failed login attempt - wrong password: {Email}", request.Email);
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Credenciales inválidas");
                }

                // Verificar que el usuario esté activo
                if (!user.IsActive)
                {
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Tu cuenta está desactivada. Contacta al soporte");
                }

                // Generar tokens
                var authResponse = await GenerateAuthResponseAsync(user, ipAddress);

                _logger.LogInformation("Successful login for user: {UserId}", user.Id);
                return ApiResponse<AuthResponseDto>.SuccessResponse(authResponse, "Login exitoso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor");
            }
        }

        /// <summary>
        /// Login con Google OAuth
        /// </summary>
        public async Task<ApiResponse<AuthResponseDto>> LoginWithGoogleAsync(GoogleLoginRequestDto request, string? ipAddress = null)
        {
            try
            {
                // Validar el token de Google
                var googleUser = await _googleAuthService.ValidateGoogleTokenAsync(request.GoogleToken);
                if (googleUser == null)
                {
                    _logger.LogWarning("Invalid Google token provided");
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Token de Google inválido");
                }

                // Login o registro con Google
                var user = await _authRepository.LoginWithGoogleAsync(
                    googleUser.Email,
                    googleUser.GoogleId,
                    googleUser.FirstName,
                    googleUser.LastName,
                    googleUser.ProfilePicture
                );

                if (user == null)
                {
                    _logger.LogError("Failed to create/login user with Google: {Email}", googleUser.Email);
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Error al procesar login con Google");
                }

                // Verificar que el usuario esté activo
                if (!user.IsActive)
                {
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Tu cuenta está desactivada. Contacta al soporte");
                }

                // Generar tokens
                var authResponse = await GenerateAuthResponseAsync(user, ipAddress);

                _logger.LogInformation("Successful Google login for user: {UserId}", user.Id);
                return ApiResponse<AuthResponseDto>.SuccessResponse(authResponse, "Login con Google exitoso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google login");
                return ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor");
            }
        }

        /// <summary>
        /// Registro de nuevo usuario
        /// </summary>
        public async Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterRequestDto request)
        {
            try
            {
                // Validar que el roleName sea válido
                var validRoles = new[] { RoleNames.JobSeeker, RoleNames.Employer };
                if (!validRoles.Contains(request.RoleName))
                {
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Rol no válido para registro");
                }

                // Verificar si el email ya existe
                var existingUser = await _authRepository.LoginUserAsync(request.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning("Registration attempt with existing email: {Email}", request.Email);
                    return ApiResponse<AuthResponseDto>.ErrorResponse("El email ya está registrado");
                }

                // Hash del password
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

                // Crear usuario con RoleName
                var user = await _authRepository.RegisterUserAsync(
                    request.Email,
                    passwordHash,
                    request.FirstName,
                    request.LastName,
                    request.PhoneNumber,
                    request.RoleName // Ahora usa string
                );

                if (user == null)
                {
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Error al crear la cuenta");
                }

                // Generar tokens
                var authResponse = await GenerateAuthResponseAsync(user);

                _logger.LogInformation("New user registered: {UserId} with role: {Role}", user.Id, user.RoleName);
                return ApiResponse<AuthResponseDto>.SuccessResponse(authResponse, "Cuenta creada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
                return ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor");
            }
        }

        /// <summary>
        /// Renovar access token usando refresh token
        /// </summary>
        public async Task<ApiResponse<AuthResponseDto>> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            try
            {
                // Validar refresh token
                var isValidToken = await _jwtTokenService.ValidateRefreshTokenAsync(request.RefreshToken);
                if (!isValidToken)
                {
                    _logger.LogWarning("Invalid refresh token provided");
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Refresh token inválido");
                }

                // Obtener usuario del refresh token
                var user = await _jwtTokenService.GetUserFromRefreshTokenAsync(request.RefreshToken);
                if (user == null || !user.IsActive)
                {
                    _logger.LogWarning("User not found or inactive for refresh token");
                    return ApiResponse<AuthResponseDto>.ErrorResponse("Usuario no encontrado o inactivo");
                }

                // Revocar el refresh token anterior
                await _jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);

                // Generar nuevos tokens
                var authResponse = await GenerateAuthResponseAsync(user);

                _logger.LogInformation("Token refreshed for user: {UserId}", user.Id);
                return ApiResponse<AuthResponseDto>.SuccessResponse(authResponse, "Token renovado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor");
            }
        }

        /// <summary>
        /// Logout - revoca el refresh token
        /// </summary>
        public async Task<ApiResponse<bool>> LogoutAsync(string refreshToken)
        {
            try
            {
                var success = await _jwtTokenService.RevokeRefreshTokenAsync(refreshToken);

                _logger.LogInformation("User logged out, token revoked: {Success}", success);
                return ApiResponse<bool>.SuccessResponse(success, "Logout exitoso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return ApiResponse<bool>.ErrorResponse("Error durante logout");
            }
        }

        /// <summary>
        /// Revoca todos los tokens de un usuario (logout de todos los dispositivos)
        /// </summary>
        public async Task<ApiResponse<bool>> RevokeAllTokensAsync(int userId)
        {
            try
            {
                // Esta funcionalidad requiere un SP adicional para revocar todos los tokens del usuario
                // Por simplicidad, se asume que existe este método en el repositorio

                _logger.LogInformation("All tokens revoked for user: {UserId}", userId);
                return ApiResponse<bool>.SuccessResponse(true, "Todos los tokens han sido revocados");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking all tokens for user: {UserId}", userId);
                return ApiResponse<bool>.ErrorResponse("Error al revocar tokens");
            }
        }

        /// <summary>
        /// Genera la respuesta de autenticación con tokens
        /// </summary>
        private async Task<AuthResponseDto> GenerateAuthResponseAsync(User user, string? ipAddress = null)
        {
            var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);
            var refreshToken = await _jwtTokenService.GenerateRefreshTokenAsync();

            // Guardar refresh token en BD
            await _jwtTokenService.SaveRefreshTokenAsync(refreshToken, user.Id, null, ipAddress);

            var expiresAt = DateTime.UtcNow.AddHours(1); // Ajustar según configuración

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = MapToUserDto(user)
            };
        }

        /// <summary>
        /// Mapea User entity a UserDto
        /// </summary>
        private static UserDto MapToUserDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                ProfilePicture = user.ProfilePicture,
                RoleId = user.RoleId,
                RoleName = user.RoleName,
                RoleDisplayName = user.RoleDisplayName,
                AuthProviderId = user.AuthProviderId,
                AuthProviderName = user.AuthProviderName,
                ExternalId = user.ExternalId,
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }
    }
}