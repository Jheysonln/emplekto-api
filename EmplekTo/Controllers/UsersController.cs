using EmplekTo.Data.Repositories;
using EmplekTo.DTOs;
using EmplekTo.DTOs.Auth;
using EmplekTo.DTOs.Common;
using EmplekTo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmplekTo.Controllers
{
    /// <summary>
    /// Controller para operaciones de usuarios
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación para todos los endpoints
    [Produces("application/json")]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene una lista paginada de usuarios
        /// Solo admins y moderadores pueden ver todos los usuarios
        /// </summary>
        /// <param name="page">Número de página (default: 1)</param>
        /// <param name="pageSize">Tamaño de página (default: 10)</param>
        /// <param name="role">Filtrar por rol (opcional)</param>
        /// <returns>Lista paginada de usuarios</returns>
        [HttpGet]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<ApiResponse<PaginatedResponse<UserDto>>>> GetUsers(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? roleName = null) // CAMBIAR: string? roleName
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var result = await _userRepository.GetAllAsync(page, pageSize, roleName); // CAMBIAR: roleName

                // Mapear Users a UserDtos
                var userDtos = result.Items.Select(MapToUserDto).ToList();

                var response = new PaginatedResponse<UserDto>
                {
                    Items = userDtos,
                    TotalItems = result.TotalItems,
                    CurrentPage = result.CurrentPage,
                    PageSize = result.PageSize,
                    TotalPages = result.TotalPages
                };

                return Ok(ApiResponse<PaginatedResponse<UserDto>>.SuccessResponse(response, "Usuarios obtenidos exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users list");
                return StatusCode(500, ApiResponse<PaginatedResponse<UserDto>>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Obtiene un usuario por ID
        /// Los usuarios solo pueden ver su propia información, admins pueden ver cualquiera
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <returns>Información del usuario</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                // Verificar permisos: propio usuario o admin/moderator
                if (currentUserId != id && !IsAdminOrModerator(currentUserRole))
                {
                    return Forbid();
                }

                var user = await _userRepository.GetByIdAsync(id);

                if (user == null)
                {
                    return NotFound(ApiResponse<UserDto>.ErrorResponse("Usuario no encontrado"));
                }

                var userDto = MapToUserDto(user);
                return Ok(ApiResponse<UserDto>.SuccessResponse(userDto, "Usuario obtenido exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID: {UserId}", id);
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Actualiza información del usuario
        /// Los usuarios solo pueden actualizar su propia información
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <param name="updateRequest">Datos a actualizar</param>
        /// <returns>Usuario actualizado</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserRequestDto updateRequest)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                // Solo puede actualizar su propia información
                if (currentUserId != id)
                {
                    return Forbid();
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return BadRequest(ApiResponse<UserDto>.ErrorResponse("Datos inválidos", errors));
                }

                // Obtener usuario actual
                var user = await _userRepository.GetByIdAsync(id);
                if (user == null)
                {
                    return NotFound(ApiResponse<UserDto>.ErrorResponse("Usuario no encontrado"));
                }

                // Actualizar campos permitidos
                user.FirstName = updateRequest.FirstName;
                user.LastName = updateRequest.LastName;
                user.PhoneNumber = updateRequest.PhoneNumber;

                // Solo actualizar ProfilePicture si se proporciona
                if (!string.IsNullOrEmpty(updateRequest.ProfilePicture))
                {
                    user.ProfilePicture = updateRequest.ProfilePicture;
                }

                var success = await _userRepository.UpdateAsync(user);

                if (!success)
                {
                    return BadRequest(ApiResponse<UserDto>.ErrorResponse("Error al actualizar usuario"));
                }

                var updatedUser = await _userRepository.GetByIdAsync(id);
                var userDto = MapToUserDto(updatedUser!);

                return Ok(ApiResponse<UserDto>.SuccessResponse(userDto, "Usuario actualizado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", id);
                return StatusCode(500, ApiResponse<UserDto>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Desactiva un usuario (solo admins)
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <returns>Confirmación de desactivación</returns>
        [HttpPost("{id}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateUser(int id)
        {
            try
            {
                var success = await _userRepository.DeactivateAsync(id);

                if (!success)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse("Usuario no encontrado"));
                }

                _logger.LogInformation("User deactivated by admin: {UserId}", id);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Usuario desactivado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user: {UserId}", id);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error interno del servidor"));
            }
        }

        /// <summary>
        /// Activa un usuario (solo admins)
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <returns>Confirmación de activación</returns>
        [HttpPost("{id}/activate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateUser(int id)
        {
            try
            {
                var success = await _userRepository.ActivateAsync(id);

                if (!success)
                {
                    return NotFound(ApiResponse<bool>.ErrorResponse("Usuario no encontrado"));
                }

                _logger.LogInformation("User activated by admin: {UserId}", id);
                return Ok(ApiResponse<bool>.SuccessResponse(true, "Usuario activado exitosamente"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating user: {UserId}", id);
                return StatusCode(500, ApiResponse<bool>.ErrorResponse("Error interno del servidor"));
            }
        }

        #region Helper Methods

        /// <summary>
        /// Obtiene el ID del usuario actual autenticado
        /// </summary>
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        /// <summary>
        /// Obtiene el rol del usuario actual
        /// </summary>
        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        /// <summary>
        /// Verifica si el usuario actual es Admin o Moderator
        /// </summary>
        private static bool IsAdminOrModerator(string role)
        {
            return role is "Admin" or "Moderator";
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

                // CAMPOS NORMALIZADOS - IDs y nombres
                RoleId = user.RoleId,
                RoleName = user.RoleName,
                RoleDisplayName = user.RoleDisplayName,
                AuthProviderId = user.AuthProviderId,
                AuthProviderName = user.AuthProviderName,

                // OAUTH DATA
                ExternalId = user.ExternalId,

                // RESTO IGUAL
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }

        #endregion
    }
}