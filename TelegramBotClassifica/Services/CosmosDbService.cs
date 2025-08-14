using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TelegramBotClassifica.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TelegramBotClassifica.Services
{
    public class CosmosDbService : ICosmosDbService
    {
        // Soluzione: Li rendiamo nullable con '?' perché verranno inizializzati in InitializeAsync()
        // o in un costruttore se decidessimo di inizializzare tutto lì.
        // E per _cosmosClient, lo inizializziamo direttamente nel costruttore.
        private CosmosClient _cosmosClient; // Inizializzato nel costruttore
        private Database? _database; // Reso nullable
        private Container? _container; // Reso nullable

        private readonly ILogger<CosmosDbService> _logger;
        private readonly string _databaseId;
        private readonly string _containerId;
        private readonly string _partitionKeyPath;

        public CosmosDbService(IConfiguration configuration, ILogger<CosmosDbService> logger)
        {
            _logger = logger;
            // Recupera le configurazioni da appsettings.json o variabili d'ambiente
            var connectionString = configuration.GetValue<string>("CosmosDb:ConnectionString");
            _databaseId = configuration.GetValue<string>("CosmosDb:DatabaseId") ?? "TelegramClassificaDb";
            _containerId = configuration.GetValue<string>("CosmosDb:ContainerId") ?? "UsersPoints";
            _partitionKeyPath = configuration.GetValue<string>("CosmosDb:PartitionKeyPath") ?? "/partitionKey";

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("CosmosDb:ConnectionString non configurata.");
                // Per un'applicazione reale, potresti voler gestire questo errore in modo più elegante,
                // ma per ora un'eccezione è ok per indicare un problema di configurazione critico.
                throw new ArgumentNullException("CosmosDb:ConnectionString", "La stringa di connessione a Cosmos DB non può essere vuota o null.");
            }

            // Inizializzazione di _cosmosClient nel costruttore
            _cosmosClient = new CosmosClient(connectionString);
        }

        // Questo metodo si occupa di creare il database e il container se non esistono
        public async Task InitializeAsync()
        {
            _database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
            _container = await _database.CreateContainerIfNotExistsAsync(_containerId, _partitionKeyPath);
            _logger.LogInformation("Cosmos DB database e container inizializzati.");
        }

        // Recupera i punti di un utente
        public async Task<UserPoints?> GetUserPointsAsync(long userId)
        {
            try
            {
                // Query per trovare l'utente tramite userId
                var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId")
                    .WithParameter("@userId", userId);

                using (FeedIterator<UserPoints> feed = _container!.GetItemQueryIterator<UserPoints>(query))
                {
                    while (feed.HasMoreResults)
                    {
                        var response = await feed.ReadNextAsync();
                        return response.FirstOrDefault();
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero punti utente {UserId}.", userId);
                throw;
            }
        }

        // Aggiunge o aggiorna i punti di un utente
        public async Task AddOrUpdateUserPointsAsync(long userId, string? username, string? firstName, int pointsToAdd)
        {
            string normalizedUsername = username?.ToLowerInvariant() ?? string.Empty;

            // Ottieni l'utente esistente tramite userId
            var existingUser = await GetUserPointsAsync(userId);
            UserPoints user;

            if (existingUser == null)
            {
                // Se l'utente non esiste, crea un nuovo documento con un nuovo ID
                user = new UserPoints
                {
                    UserId = userId,
                    Username = normalizedUsername,
                    FirstName = firstName,
                    Points = pointsToAdd, // Inizializza con i punti da aggiungere
                    PartitionKey = userId.ToString()
                };
                await _container!.CreateItemAsync(user, new PartitionKey(user.PartitionKey));
                _logger.LogInformation("Nuovo utente {UserId} inserito con {PointsToAdd} coin.", userId, pointsToAdd);
            }
            else
            {
                // Se l'utente esiste, AGGIORNA il documento ESISTENTE
                // NON creiamo un nuovo oggetto, ma modifichiamo quello esistente.
                user = existingUser;
                user.Points += pointsToAdd;

                // Aggiorna anche username/firstName se sono cambiati
                if (!string.IsNullOrWhiteSpace(normalizedUsername))
                {
                    user.Username = normalizedUsername;
                }
                if (!string.IsNullOrWhiteSpace(firstName))
                {
                    user.FirstName = firstName;
                }

                // Chiamiamo Upsert per aggiornare l'elemento esistente
                await _container!.UpsertItemAsync(user, new PartitionKey(user.PartitionKey));
                _logger.LogInformation("Punti utente {UserId} aggiornati a {NewPoints} (aggiunti {PointsToAdd}).", userId, user.Points, pointsToAdd);
            }
        }

        // Recupera l'ID utente tramite username
        public async Task<long> GetUserIdByUsernameAsync(string username)
        {
            string cleanUsername = username.ToLowerInvariant().TrimStart('@');
            // Nota: Le query su campi non-partition key possono essere meno efficienti per grandi dataset
            // Considera di salvare l'username anche come 'id' del documento se è univoco e usato per lookup frequenti.
            var query = new QueryDefinition("SELECT * FROM c WHERE LOWER(c.username) = @username")
                .WithParameter("@username", cleanUsername);

            using (FeedIterator<UserPoints> feed = _container!.GetItemQueryIterator<UserPoints>(query))
            {
                while (feed.HasMoreResults)
                {
                    FeedResponse<UserPoints> response = await feed.ReadNextAsync();
                    if (response.Any())
                    {
                        return response.First().UserId;
                    }
                }
            }
            return 0; // Se non trovato
        }

        // Aggiorna o crea un utente (solo dettagli, non punti)
        public async Task UpdateOrCreateUserAsync(long userId, string? username, string? firstName)
        {
            string normalizedUsername = username?.ToLowerInvariant() ?? string.Empty;
            var existingUser = await GetUserPointsAsync(userId);

            if (existingUser == null)
            {
                // SOLO se l'utente non esiste affatto, crea un nuovo record
                // Ma NON dovremmo mai sovrascrivere i punti esistenti
                var newUser = new UserPoints
                {
                    UserId = userId,
                    Username = normalizedUsername,
                    FirstName = firstName,
                    Points = 0, // Nuovo utente inizia con 0 punti
                    PartitionKey = userId.ToString()
                };
                await _container!.CreateItemAsync(newUser, new PartitionKey(newUser.PartitionKey));
                _logger.LogInformation("Nuovo utente {UserId} inserito (da updateOrCreateUser) con username '{Username}'.", userId, normalizedUsername);
            }
            else
            {
                // Aggiorna solo username/firstName, MA PRESERVA I PUNTI ESISTENTI
                bool needsUpdate = false;

                if (existingUser.Username != normalizedUsername)
                {
                    _logger.LogInformation("Aggiornando username per utente {UserId}: '{OldUsername}' -> '{NewUsername}'",
                        userId, existingUser.Username ?? "NULL", normalizedUsername);
                    existingUser.Username = normalizedUsername;
                    needsUpdate = true;
                }

                if (existingUser.FirstName != firstName)
                {
                    _logger.LogInformation("Aggiornando firstName per utente {UserId}: '{OldFirstName}' -> '{NewFirstName}'",
                        userId, existingUser.FirstName ?? "NULL", firstName ?? "NULL");
                    existingUser.FirstName = firstName;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    await _container!.UpsertItemAsync(existingUser, new PartitionKey(existingUser.PartitionKey));
                    _logger.LogInformation("Dati utente {UserId} aggiornati (da updateOrCreateUser). Punti preservati: {Points}.", userId, existingUser.Points);
                }
                else
                {
                    _logger.LogDebug("Nessun aggiornamento necessario per utente {UserId}. Punti attuali: {Points}.", userId, existingUser.Points);
                }
            }
        }

        // Recupera la classifica
        public async Task<IEnumerable<UserPoints>> GetLeaderboardAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.points > 0 ORDER BY c.points DESC");
            var results = new List<UserPoints>();

            using (FeedIterator<UserPoints> feed = _container!.GetItemQueryIterator<UserPoints>(query))
            {
                while (feed.HasMoreResults)
                {
                    FeedResponse<UserPoints> response = await feed.ReadNextAsync();
                    results.AddRange(response.Resource);
                }
            }
            return results;
        }

        // Resetta la classifica
        public async Task ResetLeaderboardAsync()
        {
            // Questo è un po' più complesso in Cosmos DB perché non c'è un "TRUNCATE TABLE" diretto.
            // Dobbiamo leggere tutti i documenti e cancellarli uno per uno.
            // PER AMBIENTI DI PRODUZIONE CON MOLTI DATI, questa operazione può essere costosa e lenta.
            // Considerare soluzioni alternative (es. cancellare e ricreare il container, o usare Change Feed).
            var query = new QueryDefinition("SELECT c.id, c.partitionKey FROM c");
            var itemsToDelete = new List<(string id, string partitionKey)>();

            using (FeedIterator<dynamic> feed = _container!.GetItemQueryIterator<dynamic>(query))
            {
                while (feed.HasMoreResults)
                {
                    FeedResponse<dynamic> response = await feed.ReadNextAsync();
                    foreach (var item in response.Resource)
                    {
                        itemsToDelete.Add((item.id.ToString(), item.partitionKey.ToString()));
                    }
                }
            }

            foreach (var item in itemsToDelete)
            {
                await _container.DeleteItemAsync<UserPoints>(item.id, new PartitionKey(item.partitionKey));
            }
            _logger.LogInformation("Classifica resettata: tutti i documenti eliminati.");
        }

        // Esporta gli utenti conosciuti
        public async Task<IEnumerable<UserPoints>> ExportKnownUsersAsync()
        {
            var query = new QueryDefinition("SELECT * FROM c");
            var results = new List<UserPoints>();

            using (FeedIterator<UserPoints> feed = _container!.GetItemQueryIterator<UserPoints>(query))
            {
                while (feed.HasMoreResults)
                {
                    FeedResponse<UserPoints> response = await feed.ReadNextAsync();
                    results.AddRange(response.Resource);
                }
            }
            return results;
        }
    }
}