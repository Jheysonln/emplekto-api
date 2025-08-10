using Dapper;
using EmplekTo.DTOs.Common;
using EmplekTo.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EmplekTo.Data.Repositories
{
    /// <summary>
    /// Implementación del repositorio de usuarios usando Dapper
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string not found");
            _logger = logger;
        }

        public async Task<User?> GetByIdAsync(int id)
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

        public async Task<User?> GetByEmailAsync(string email)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var user = await connection.QueryFirstOrDefaultAsync<User>(
                    "SELECT * FROM Users WHERE Email = @Email AND IsActive = 1",
                    new { Email = email }
                );

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email: {Email}", email);
                throw;
            }
        }

        public async Task<PaginatedResponse<User>> GetAllAsync(int page = 1, int pageSize = 10, string? roleName = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var offset = (page - 1) * pageSize;

                // WHERE clause actualizado para usar JOIN con Roles
                var whereClause = !string.IsNullOrEmpty(roleName) ? "AND r.Name = @RoleName" : "";

                // Consulta para obtener el total de registros - CON JOIN
                var totalQuery = $@"
            SELECT COUNT(*) 
            FROM Users u 
                INNER JOIN Roles r ON u.RoleId = r.Id 
            WHERE u.IsActive = 1 {whereClause}";

                var totalItems = await connection.QueryFirstOrDefaultAsync<int>(totalQuery, new { RoleName = roleName });

                // Consulta paginada - CON JOIN para obtener datos normalizados
                var dataQuery = $@"
            SELECT 
                u.Id,
                u.Email,
                u.PasswordHash,
                u.FirstName,
                u.LastName,
                u.PhoneNumber,
                u.ProfilePicture,
                u.RoleId,
                r.Name AS RoleName,
                r.DisplayName AS RoleDisplayName,
                u.AuthProviderId,
                ap.Name AS AuthProviderName,
                u.ExternalId,
                u.IsActive,
                u.EmailConfirmed,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt
            FROM Users u
                INNER JOIN Roles r ON u.RoleId = r.Id
                INNER JOIN AuthProviders ap ON u.AuthProviderId = ap.Id
            WHERE u.IsActive = 1 {whereClause}
            ORDER BY u.CreatedAt DESC 
            OFFSET @Offset ROWS 
            FETCH NEXT @PageSize ROWS ONLY";

                var users = await connection.QueryAsync<User>(dataQuery, new
                {
                    RoleName = roleName,
                    Offset = offset,
                    PageSize = pageSize
                });

                return new PaginatedResponse<User>
                {
                    Items = users.ToList(),
                    TotalItems = totalItems,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated users");
                throw;
            }
        }

        public async Task<bool> UpdateAsync(User user)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    UPDATE Users SET 
                        FirstName = @FirstName,
                        LastName = @LastName,
                        PhoneNumber = @PhoneNumber,
                        ProfilePicture = @ProfilePicture,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id AND IsActive = 1";

                var rowsAffected = await connection.ExecuteAsync(sql, user);
                var success = rowsAffected > 0;

                _logger.LogInformation("User updated {UserId}: {Success}", user.Id, success);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
                throw;
            }
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            return await ChangeActiveStatusAsync(id, false);
        }

        public async Task<bool> ActivateAsync(int id)
        {
            return await ChangeActiveStatusAsync(id, true);
        }

        public async Task<bool> ConfirmEmailAsync(int id)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    UPDATE Users SET 
                        EmailConfirmed = 1,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id";

                var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
                var success = rowsAffected > 0;

                _logger.LogInformation("Email confirmed for user {UserId}: {Success}", id, success);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming email for user: {UserId}", id);
                throw;
            }
        }

        private async Task<bool> ChangeActiveStatusAsync(int id, bool isActive)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    UPDATE Users SET 
                        IsActive = @IsActive,
                        UpdatedAt = GETUTCDATE()
                    WHERE Id = @Id";

                var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, IsActive = isActive });
                var success = rowsAffected > 0;

                _logger.LogInformation("User {UserId} {Action}: {Success}",
                    id, isActive ? "activated" : "deactivated", success);

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing active status for user: {UserId}", id);
                throw;
            }
        }
    }
}
