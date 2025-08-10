using EmplekTo.DTOs.Common;
using EmplekTo.Models;

namespace EmplekTo.Data.Repositories
{
    /// <summary>
    /// Interface para operaciones de usuarios
    /// </summary>
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<PaginatedResponse<User>> GetAllAsync(int page = 1, int pageSize = 10, string? roleName = null);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeactivateAsync(int id);
        Task<bool> ActivateAsync(int id);
        Task<bool> ConfirmEmailAsync(int id);
    }
}
