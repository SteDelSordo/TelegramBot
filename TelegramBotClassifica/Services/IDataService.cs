using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramBotClassifica.Models;

namespace TelegramBotClassifica.Services
{
    // Questa è l'interfaccia generica che il nostro bot userà.
    // Non sa se sotto c'è SQLite, CosmosDB o altro, e non gli interessa.
    public interface IDataService
    {
        Task InitializeAsync();
        Task<UserPoints?> GetUserPointsAsync(long userId);
        Task AddOrUpdateUserPointsAsync(long userId, string? username, string? firstName, int pointsToAdd);
        Task<long> GetUserIdByUsernameAsync(string username);
        Task<IEnumerable<UserPoints>> GetLeaderboardAsync();
        Task ResetLeaderboardAsync();
        Task<IEnumerable<UserPoints>> ExportKnownUsersAsync();
        Task UpdateOrCreateUserAsync(long userId, string? username, string? firstName);
    }
}
