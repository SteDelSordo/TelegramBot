using MongoDB.Driver;
using TelegramBotClassifica.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace TelegramBotClassifica.Services
{
    public class MongoDbService : IDataService
    {
        private readonly IMongoCollection<UserPoints> _collection;

        public MongoDbService(string mongoUri)
        {
            var mongoUrl = new MongoUrl(mongoUri);
            var client = new MongoClient(mongoUrl);
            // Forza il database con il nome corretto
            var database = client.GetDatabase("ClassificaBotTelegram");
            _collection = database.GetCollection<UserPoints>("UserPoints");
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public async Task<UserPoints?> GetUserPointsAsync(long userId)
        {
            return await _collection.Find(x => x.UserId == userId).FirstOrDefaultAsync();
        }

        public async Task AddOrUpdateUserPointsAsync(long userId, string? username, string? firstName, int pointsToAdd)
        {
            var cleanUsername = username?.ToLowerInvariant();
            var filter = Builders<UserPoints>.Filter.Eq(u => u.UserId, userId);
            var update = Builders<UserPoints>.Update
                .SetOnInsert(u => u.UserId, userId)
                .Set(u => u.Username, cleanUsername)
                .Set(u => u.FirstName, firstName)
                .Inc(u => u.Points, pointsToAdd);

            await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task<long> GetUserIdByUsernameAsync(string username)
        {
            var cleanUsername = username.ToLowerInvariant().TrimStart('@');
            var user = await _collection.Find(u => u.Username == cleanUsername).FirstOrDefaultAsync();
            return user?.UserId ?? 0;
        }

        public async Task UpdateOrCreateUserAsync(long userId, string? username, string? firstName)
        {
            var cleanUsername = username?.ToLowerInvariant();
            var filter = Builders<UserPoints>.Filter.Eq(u => u.UserId, userId);
            var update = Builders<UserPoints>.Update
                .SetOnInsert(u => u.UserId, userId)
                .Set(u => u.Username, cleanUsername)
                .Set(u => u.FirstName, firstName)
                .SetOnInsert(u => u.Points, 0);

            await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        public async Task<List<UserPoints>> GetLeaderboardAsync()
        {
            return await _collection.Find(_ => true)
                .SortByDescending(u => u.Points)
                .ToListAsync();
        }

        public async Task ResetLeaderboardAsync()
        {
            await _collection.DeleteManyAsync(_ => true);
        }

        public async Task<List<UserPoints>> ExportKnownUsersAsync()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }

        // Seed iniziale da JSON SOLO se la collection è vuota
        public async Task SeedFromJsonIfEmptyAsync(string jsonFilePath)
        {
            var count = await _collection.CountDocumentsAsync(_ => true);
            if (count > 0) return;

            if (!File.Exists(jsonFilePath)) return;

            var json = await File.ReadAllTextAsync(jsonFilePath);
            var users = JsonConvert.DeserializeObject<List<UserPoints>>(json);
            if (users == null || users.Count == 0) return;

            await _collection.InsertManyAsync(users);
        }
    }
}