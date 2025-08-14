using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramBotClassifica.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO; // Aggiunto per le operazioni sui file
using Newtonsoft.Json; // Aggiunto per deserializzare il JSON

namespace TelegramBotClassifica.Services
{
    // Questa classe implementa l'interfaccia IDataService usando Entity Framework Core e SQLite.
    public class SqliteDbService : IDataService
    {
        private readonly BotDbContext _context;
        private readonly ILogger<SqliteDbService> _logger;

        public SqliteDbService(BotDbContext context, ILogger<SqliteDbService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            // Questo comando si assicura che il database esista.
            await _context.Database.EnsureCreatedAsync();
            _logger.LogInformation("Database SQLite 'classifica.db' inizializzato e pronto.");

            // --- NUOVA LOGICA DI IMPORTAZIONE (SEEDING) ---
            // Controlliamo se la tabella degli utenti è già popolata.
            if (!await _context.UserPoints.AnyAsync())
            {
                _logger.LogInformation("Il database è vuoto. Tentativo di importare i dati da 'users_backup.json'...");

                string backupFilePath = "users_backup.json";
                if (File.Exists(backupFilePath))
                {
                    try
                    {
                        // Definiamo un modello temporaneo che rispecchia la struttura del JSON di Cosmos DB
                        // per evitare conflitti con il nostro nuovo modello UserPoints per SQLite.
                        var cosmosUserDefinition = new[] { new
                        {
                            id = string.Empty,
                            userId = 0L,
                            username = (string?)null,
                            firstName = (string?)null,
                            points = 0
                        }};

                        string json = await File.ReadAllTextAsync(backupFilePath);
                        var cosmosUsers = JsonConvert.DeserializeAnonymousType(json, cosmosUserDefinition);

                        if (cosmosUsers != null && cosmosUsers.Any())
                        {
                            // Trasformiamo i dati dal formato Cosmos al formato SQLite
                            var usersToImport = cosmosUsers.Select(c => new UserPoints
                            {
                                // L'ID primario non viene mappato, EF Core lo genererà automaticamente
                                UserId = c.userId,
                                Username = c.username,
                                FirstName = c.firstName,
                                Points = c.points
                            });

                            // Aggiungiamo tutti gli utenti al contesto in un colpo solo
                            await _context.UserPoints.AddRangeAsync(usersToImport);
                            // Salviamo le modifiche nel database
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("✅ IMPORTAZIONE RIUSCITA: {Count} utenti importati dal file di backup.", usersToImport.Count());
                        }
                        else
                        {
                            _logger.LogWarning("Il file di backup 'users_backup.json' è vuoto o malformato.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Errore critico durante l'importazione dei dati dal file di backup.");
                    }
                }
                else
                {
                    _logger.LogWarning("File di backup 'users_backup.json' non trovato. Il database partirà vuoto.");
                }
            }
            else
            {
                _logger.LogInformation("Il database contiene già dati. L'importazione non è necessaria.");
            }
        }

        public async Task<UserPoints?> GetUserPointsAsync(long userId)
        {
            return await _context.UserPoints.FirstOrDefaultAsync(u => u.UserId == userId);
        }

        public async Task AddOrUpdateUserPointsAsync(long userId, string? username, string? firstName, int pointsToAdd)
        {
            var user = await GetUserPointsAsync(userId);
            string? normalizedUsername = username?.ToLowerInvariant();

            if (user == null)
            {
                // L'utente non esiste, lo creiamo.
                user = new UserPoints
                {
                    UserId = userId,
                    Username = normalizedUsername,
                    FirstName = firstName,
                    Points = pointsToAdd // I punti iniziali sono quelli da aggiungere
                };
                _context.UserPoints.Add(user);
                _logger.LogInformation("Nuovo utente {UserId} creato con {PointsToAdd} coin.", userId, pointsToAdd);
            }
            else
            {
                // L'utente esiste, aggiorniamo i suoi dati.
                user.Points += pointsToAdd;
                if (!string.IsNullOrWhiteSpace(normalizedUsername)) user.Username = normalizedUsername;
                if (!string.IsNullOrWhiteSpace(firstName)) user.FirstName = firstName;
                _context.UserPoints.Update(user);
                _logger.LogInformation("Punti utente {UserId} aggiornati a {NewPoints}.", userId, user.Points);
            }
            // Questo comando salva tutte le modifiche (Add, Update, Delete) nel database.
            await _context.SaveChangesAsync();
        }

        public async Task<long> GetUserIdByUsernameAsync(string username)
        {
            var cleanUsername = username.ToLowerInvariant().TrimStart('@');
            var user = await _context.UserPoints.FirstOrDefaultAsync(u => u.Username == cleanUsername);
            return user?.UserId ?? 0; // Ritorna 0 se non trovato
        }

        public async Task UpdateOrCreateUserAsync(long userId, string? username, string? firstName)
        {
            var user = await GetUserPointsAsync(userId);
            string? normalizedUsername = username?.ToLowerInvariant();

            if (user == null)
            {
                user = new UserPoints { UserId = userId, Username = normalizedUsername, FirstName = firstName, Points = 0 };
                _context.UserPoints.Add(user);
            }
            else
            {
                bool needsUpdate = false;
                if (user.Username != normalizedUsername) { user.Username = normalizedUsername; needsUpdate = true; }
                if (user.FirstName != firstName) { user.FirstName = firstName; needsUpdate = true; }

                if (needsUpdate)
                {
                    _context.UserPoints.Update(user);
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<UserPoints>> GetLeaderboardAsync()
        {
            return await _context.UserPoints
                .Where(u => u.Points > 0)
                .OrderByDescending(u => u.Points)
                .ToListAsync();
        }

        public async Task ResetLeaderboardAsync()
        {
            // Con EF Core, cancellare tutti i record è un singolo comando.
            // NOTA: Questo comando è potente, usarlo con cautela!
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM UserPoints");
            _logger.LogInformation("Classifica resettata: tutti i record eliminati dalla tabella UserPoints.");
        }

        public async Task<IEnumerable<UserPoints>> ExportKnownUsersAsync()
        {
            return await _context.UserPoints.AsNoTracking().ToListAsync();
        }
    }
}
