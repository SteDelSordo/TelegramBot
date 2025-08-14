
using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramBotClassifica.Models;

namespace TelegramBotClassifica.Services
{
    public interface ICosmosDbService
    {
        Task InitializeAsync();
        Task<UserPoints?> GetUserPointsAsync(long userId);
        Task AddOrUpdateUserPointsAsync(long userId, string? username, string? firstName, int pointsToAdd);
        Task<long> GetUserIdByUsernameAsync(string username);
        Task<IEnumerable<UserPoints>> GetLeaderboardAsync();
        Task ResetLeaderboardAsync();
        Task<IEnumerable<UserPoints>> ExportKnownUsersAsync();
        Task UpdateOrCreateUserAsync(long userId, string? username, string? firstName); // Aggiunto per mantenere aggiornati username/firstName
    }
}