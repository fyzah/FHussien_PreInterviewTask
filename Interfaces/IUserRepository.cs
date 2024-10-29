using FHussien_PreInterviewTask.Models;

namespace FHussien_PreInterviewTask.Interfaces
{
    public interface IUserRepository
    {
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<IEnumerable<Result>> GetAllUsersAsync();
        Task<Result> GetUserByIdAsync(int id);
        Task<int> AddUserAsync(Result user);
        Task<int> UpdateUserAsync(Request user);
        Task<int> DeleteUserAsync(int id);
    }
}
