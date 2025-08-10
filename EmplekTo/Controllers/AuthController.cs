using EmplekTo.DTOs.Auth;
using EmplekTo.DTOs.Common;
using EmplekTo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmplekTo.Controllers
{

    /// <summary>
    /// Controller para operaciones de autenticación
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Login tradicional con email y password
        /// </summary>
        /// <param name="request">Credenciales de login</param>
        /// <returns>Token de acceso y información del usuario</returns>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse("Datos inválidos", errors));
                }

                var ipAddress = GetClientIpAddress();
                var result = await _authService.LoginAsync(request, ipAddress);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                // Establecer cookie HttpOnly con el refresh token para mayor seguridad
                SetRefreshTokenCookie(result.Data!.RefreshToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Login endpoint");
                return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Login con Google OAuth
        /// </summary>
        /// <param name="request">Token de Google</param>
        /// <returns>Token de acceso y información del usuario</returns>
        [HttpPost("google-login")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> GoogleLogin([FromBody] GoogleLoginRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse("Datos inválidos", errors));
                }

                var ipAddress = GetClientIpAddress();
                var result = await _authService.LoginWithGoogleAsync(request, ipAddress);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                // Establecer cookie HttpOnly con el refresh token
                SetRefreshTokenCookie(result.Data!.RefreshToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GoogleLogin endpoint");
                return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Registro de nuevo usuario
        /// </summary>
        /// <param name="request">Datos del nuevo usuario</param>
        /// <returns>Token de acceso y información del usuario</returns>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register([FromBody] RegisterRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse("Datos inválidos", errors));
                }

                var result = await _authService.RegisterAsync(request);

                if (!result.Success)
                {
                    return BadRequest(result);
                }

                // Establecer cookie HttpOnly con el refresh token
                SetRefreshTokenCookie(result.Data!.RefreshToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Register endpoint");
                return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Renovar access token usando refresh token
        /// </summary>
        /// <returns>Nuevos tokens</returns>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken()
        {
            try
            {
                // Intentar obtener refresh token de cookie o header
                var refreshToken = GetRefreshTokenFromRequest();

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return BadRequest(ApiResponse<AuthResponseDto>.ErrorResponse("Refresh token requerido"));
                }

                var request = new RefreshTokenRequestDto { RefreshToken = refreshToken };
                var result = await _authService.RefreshTokenAsync(request);

                if (!result.Success)
                {
                    // Limpiar cookie si el token no es válido
                    ClearRefreshTokenCookie();
                    return BadRequest(result);
                }

                // Actualizar cookie con el nuevo refresh token
                SetRefreshTokenCookie(result.Data!.RefreshToken);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RefreshToken endpoint");
                return StatusCode(500, ApiResponse<AuthResponseDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Logout del usuario actual
        /// </summary>
        /// <returns>Confirmación de logout</returns>
        [HttpPost("logout")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<bool>>> Logout()
        {
            try
            {
                var refreshToken = GetRefreshTokenFromRequest();

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await _authService.LogoutAsync(refreshToken);
                }

                // Limpiar cookie
                ClearRefreshTokenCookie();

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Logout exitoso"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Logout endpoint");
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error durante logout"));
            }
        }

        /// <summary>
        /// Obtiene información del usuario autenticado actual
        /// </summary>
        /// <returns>Información del usuario</returns>
        [HttpGet("me")]
        [Authorize]
        public ActionResult<ApiResponse<object>> GetCurrentUser()
        {
            try
            {
                var userId = GetCurrentUserId();
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
                var lastName = User.FindFirst(ClaimTypes.Surname)?.Value;

                var userInfo = new
                {
                    Id = userId,
                    Email = userEmail,
                    FirstName = firstName,
                    LastName = lastName,
                    Role = userRole,
                    Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
                };

                return Ok(ApiResponse<object>.SuccessResponse(userInfo, "Información del usuario obtenida"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user info");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Valida si un access token es válido
        /// </summary>
        /// <returns>Estado de validez del token</returns>
        [HttpGet("validate")]
        [Authorize]
        public ActionResult<ApiResponse<object>> ValidateToken()
        {
            try
            {
                var userId = GetCurrentUserId();
                var tokenInfo = new
                {
                    IsValid = true,
                    UserId = userId,
                    ExpiresAt = User.FindFirst(ClaimTypes.Expiration)?.Value,
                    IssuedAt = User.FindFirst("iat")?.Value
                };

                return Ok(ApiResponse<object>.SuccessResponse(tokenInfo, "Token válido"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, ApiResponse<object>.ErrorResponse("Error validando token"));
            }
        }

        #region Helper Methods

        /// <summary>
        /// Obtiene la IP del cliente
        /// </summary>
        private string GetClientIpAddress()
        {
            // Buscar IP en headers de proxy (si se usa load balancer/proxy)
            var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            // IP directa de la conexión
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// Obtiene el ID del usuario actual autenticado
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Obtiene el refresh token del request (cookie o header)
        /// </summary>
        private string? GetRefreshTokenFromRequest()
        {
            // Primero intentar obtener de cookie (más seguro)
            var refreshToken = Request.Cookies["refreshToken"];

            // Si no está en cookie, intentar obtener del header Authorization
            if (string.IsNullOrEmpty(refreshToken))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Refresh "))
                {
                    refreshToken = authHeader["Refresh ".Length..];
                }
            }

            return refreshToken;
        }

        /// <summary>
        /// Establece cookie HttpOnly con el refresh token
        /// </summary>
        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Solo HTTPS en producción
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromDays(7), // 7 días
                Path = "/api/auth"
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
        }

        /// <summary>
        /// Limpia la cookie del refresh token
        /// </summary>
        private void ClearRefreshTokenCookie()
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                Path = "/api/auth"
            });
        }

        #endregion
    }
}
