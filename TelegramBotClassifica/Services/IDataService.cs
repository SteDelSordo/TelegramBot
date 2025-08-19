using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramBotClassifica.Models;

namespace TelegramBotClassifica.Services
{
    public interface IDataService
    {
        Task InitializeAsync();
        Task<UserPoints?> GetUserPointsAsync(long userId);
        Task AddOrUpdateUserPointsAsync(long userId, string? username, string? firstName, int pointsToAdd);
        Task<long> GetUserIdByUsernameAsync(string username);
        Task<List<UserPoints>> GetLeaderboardAsync();
        Task ResetLeaderboardAsync();
        Task<List<UserPoints>> ExportKnownUsersAsync();
        Task UpdateOrCreateUserAsync(long userId, string? username, string? firstName);
        Task SeedFromJsonIfEmptyAsync(string jsonFilePath);
    }
}