using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotClassifica.Configuration;
using TelegramBotClassifica.Models; // Importa i modelli
using System;
using System.Collections.Generic; // Aggiungi per List
using System.Linq; // Aggiungi per Linq
using System.Threading;
using System.Threading.Tasks;
using System.Text; // Per StringBuilder

namespace TelegramBotClassifica.Services
{
    public class BotService : BackgroundService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<BotService> _logger;
        private readonly BotConfiguration _botConfiguration;
        private readonly ICosmosDbService _cosmosDbService; // Aggiungi questa riga

        public BotService(ITelegramBotClient botClient, ILogger<BotService> logger, BotConfiguration botConfiguration, ICosmosDbService cosmosDbService)
        {
            _botClient = botClient;
            _logger = logger;
            _botConfiguration = botConfiguration;
            _cosmosDbService = cosmosDbService; // Inietta il servizio Cosmos DB
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BotService is starting.");

            // Inizializza il database Cosmos DB all'avvio del bot
            await _cosmosDbService.InitializeAsync(); // Chiama l'inizializzazione del DB

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation("BotService started receiving updates.");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            Message? message = update.Message;

            if (message is not { } msg)
                return;

            if (msg.From == null)
            {
                _logger.LogWarning("Received message with no sender. Ignoring.");
                return;
            }

            await _cosmosDbService.UpdateOrCreateUserAsync(msg.From.Id, msg.From.Username, msg.From.FirstName);

            // NUOVO: Variabile per controllare se dobbiamo inviare un messaggio di risposta
            bool shouldSendResponse = false;
            var msgText = ""; // Inizializziamo vuota

            // NON risponde ai messaggi nei gruppi che non sono comandi.
            // Se il messaggio NON è privato O NON inizia con '/', allora ignoriamo il comando.
            if (msg.Chat.Type != ChatType.Private && msg.Text?.StartsWith("/") != true)
            {
                // È un messaggio normale in un gruppo, lo logghiamo ma non rispondiamo.
                _logger.LogInformation("Received non-command message '{text}' from group {chatId}.", msg.Text, msg.Chat.Id);
                return; // Ignora del tutto i messaggi non-comando nei gruppi
            }


            _logger.LogInformation("Received message '{text}' from {chatId}.", msg.Text, msg.Chat.Id);

            string command = string.Empty;
            string arguments = string.Empty;

            // Se è un comando (quindi inizia con '/')
            if (msg.Text?.StartsWith("/") == true)
            {
                var parts = msg.Text.Split(' ', 2);
                command = parts[0].ToLowerInvariant().TrimStart('/');
                if (parts.Length > 1)
                {
                    arguments = parts[1];
                }

                // Qui gestiamo i comandi. Tutte le risposte verranno messe in msgText.
                switch (command)
                {
                    case "start":
                        // Questo comando dovrebbe funzionare solo in privato
                        if (msg.Chat.Type == ChatType.Private)
                        {
                            msgText = "Ciao! Sono il bot della classifica. Usa /ap <ID_utente_o_username> <coin> e /classifica per vedere i risultati. Tutti i comandi funzionano solo qui in privato.";
                            shouldSendResponse = true;
                        }
                        break;

                    case "ap":
                        // Questo comando funziona solo in privato e solo per admin
                        if (msg.Chat.Type == ChatType.Private && IsUserAuthorized(msg.From.Id))
                        {
                            var args = arguments.Split(' ', 2);
                            if (args.Length != 2)
                            {
                                msgText = "Uso corretto: /ap <ID_utente_o_username> <coin>";
                                shouldSendResponse = true;
                            }
                            else
                            {
                                string targetUserIdentifier = args[0];
                                if (!int.TryParse(args[1], out int coins))
                                {
                                    msgText = "Valore di coin non valido. Deve essere un numero intero (può essere negativo per rimuovere coin).";
                                    shouldSendResponse = true;
                                }
                                else
                                {
                                    // Log per debugging
                                    _logger.LogInformation("Comando /ap ricevuto: targetUser='{Target}', coins={Coins}", targetUserIdentifier, coins);

                                    long targetUserID = 0;
                                    if (long.TryParse(targetUserIdentifier, out long parsedID))
                                    {
                                        targetUserID = parsedID;
                                    }
                                    else
                                    {
                                        targetUserID = await _cosmosDbService.GetUserIdByUsernameAsync(targetUserIdentifier);
                                        if (targetUserID == 0)
                                        {
                                            msgText = $"❌ Username '{targetUserIdentifier}' non trovato nel database. Assicurati che l'utente abbia già interagito con il bot o usa l'ID numerico.";
                                            shouldSendResponse = true;
                                            _logger.LogWarning("Tentativo di aggiungere punti a username non esistente: '{Username}'", targetUserIdentifier);
                                        }
                                    }

                                    if (targetUserID != 0)
                                    {
                                        try
                                        {
                                            // Recupera i dati esistenti dell'utente per preservare username e firstName
                                            var existingUser = await _cosmosDbService.GetUserPointsAsync(targetUserID);

                                            string? preservedUsername = existingUser?.Username;
                                            string? preservedFirstName = existingUser?.FirstName;

                                            // Se l'utente è stato identificato tramite username, preservalo
                                            if (!long.TryParse(targetUserIdentifier, out _))
                                            {
                                                // L'identificatore era un username, assicuriamoci di preservarlo
                                                preservedUsername = targetUserIdentifier.ToLowerInvariant().TrimStart('@');
                                            }

                                            await _cosmosDbService.AddOrUpdateUserPointsAsync(targetUserID, preservedUsername, preservedFirstName, coins);
                                            string displayName = !string.IsNullOrWhiteSpace(preservedUsername) ? $"@{preservedUsername}" : targetUserID.ToString();

                                            // Messaggio più descrittivo per aggiunte/rimozioni
                                            if (coins > 0)
                                            {
                                                msgText = $"✅ Aggiunti {coins} coin all'utente {displayName}. Totale coin aggiornato.";
                                            }
                                            else if (coins < 0)
                                            {
                                                msgText = $"➖ Rimossi {Math.Abs(coins)} coin dall'utente {displayName}. Totale coin aggiornato.";
                                            }
                                            else
                                            {
                                                msgText = $"ℹ️ Nessuna modifica ai coin per l'utente {displayName} (valore 0).";
                                            }
                                            shouldSendResponse = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            msgText = $"Errore nell'aggiunta/rimozione dei coin: {ex.Message}";
                                            _logger.LogError(ex, msgText);
                                            shouldSendResponse = true;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            msgText = "Mi dispiace, questo comando può essere usato solo dagli amministratori autorizzati e solo in chat privata.";
                            shouldSendResponse = true;
                        }
                        break;

                    case "classifica":
                        // Questo comando dovrebbe funzionare solo in privato
                        if (msg.Chat.Type == ChatType.Private)
                        {
                            var leaderboard = await _cosmosDbService.GetLeaderboardAsync();
                            if (!leaderboard.Any())
                            {
                                msgText = "La classifica è vuota o nessuno ha ancora coin! Inizia ad aggiungerli.";
                            }
                            else
                            {
                                var sb = new StringBuilder();
                                // AGGIUNTA QUI: Data e ora
                                sb.AppendLine($"🏆 Classifica Attuale ({DateTime.Now:dd/MM/yyyy HH:mm:ss})\n");

                                int rank = 1;
                                foreach (var entry in leaderboard)
                                {
                                    string displayName;

                                    _logger.LogInformation("Processing leaderboard entry: UserId={UserId}, Username='{Username}', FirstName='{FirstName}', Points={Points}, FinalDisplayName='{FinalDisplayName}'",
                                        entry.UserId, entry.Username ?? "NULL", entry.FirstName ?? "NULL", entry.Points, entry.Username ?? entry.FirstName ?? entry.UserId.ToString()); // Updated log to reflect final display name logic.

                                    // Priorità 1: Username (se non è vuoto o null)
                                    if (!string.IsNullOrWhiteSpace(entry.Username))
                                    {
                                        displayName = "@" + entry.Username;
                                    }
                                    // Priorità 2: FirstName (solo se Username non c'è)
                                    else if (!string.IsNullOrWhiteSpace(entry.FirstName))
                                    {
                                        displayName = entry.FirstName;
                                    }
                                    // Priorità 3: UserId (se Username e FirstName non ci sono)
                                    else
                                    {
                                        displayName = $"ID: {entry.UserId}";
                                    }

                                    sb.AppendLine($"{rank}. {displayName} - 🪙 {entry.Points}");
                                    rank++;
                                }
                                msgText = sb.ToString();
                            }
                            shouldSendResponse = true;
                        }
                        break;

                    case "resetclassifica":
                        // Questo comando funziona solo in privato e solo per admin
                        if (msg.Chat.Type == ChatType.Private && IsUserAuthorized(msg.From.Id))
                        {
                            try
                            {
                                await _cosmosDbService.ResetLeaderboardAsync();
                                msgText = "Classifica resettata con successo!";
                            }
                            catch (Exception ex)
                            {
                                msgText = $"Errore durante il reset della classifica: {ex.Message}";
                                _logger.LogError(ex, msgText);
                            }
                            shouldSendResponse = true;
                        }
                        else
                        {
                            msgText = "Mi dispiace, questo comando può essere usato solo dagli amministratori autorizzati e solo in chat privata.";
                            shouldSendResponse = true;
                        }
                        break;

                    case "exportusers":
                        if (msg.Chat.Type == ChatType.Private && IsUserAuthorized(msg.From.Id))
                        {
                            try
                            {
                                var users = await _cosmosDbService.ExportKnownUsersAsync();
                                var json = Newtonsoft.Json.JsonConvert.SerializeObject(users, Newtonsoft.Json.Formatting.Indented);

                                // MODIFICA QUI: Rimuoviamo la formattazione Markdown diretta e la logica di invio JSON
                                // Per i test, logghiamo il JSON e inviamo un messaggio di conferma semplice.
                                _logger.LogInformation("Esportazione utenti JSON: {JsonData}", json);
                                msgText = "Utenti conosciuti esportati. Controlla i log della console per il JSON completo.";

                                // Se il JSON è troppo lungo, il log sarà comunque completo,
                                // ma eviteremo problemi di parsing o troncamento su Telegram.
                                shouldSendResponse = true;
                            }
                            catch (Exception ex)
                            {
                                msgText = $"Errore durante l'esportazione degli utenti: {ex.Message}";
                                _logger.LogError(ex, msgText);
                                shouldSendResponse = true;
                            }
                        }
                        else
                        {
                            msgText = "Mi dispiace, questo comando può essere usato solo dagli amministratori autorizzati e solo in chat privata.";
                            shouldSendResponse = true;
                        }
                        break;

                    default:
                        // Se è un comando sconosciuto, risponde solo in privato
                        if (msg.Chat.Type == ChatType.Private)
                        {
                            msgText = "Comando sconosciuto.";
                            shouldSendResponse = true;
                        }
                        break;
                }
            }

            // Invia il messaggio solo se shouldSendResponse è true e msgText non è vuoto
            if (shouldSendResponse && !string.IsNullOrEmpty(msgText))
            {
                await botClient.SendTextMessageAsync(
                    chatId: msg.Chat.Id,
                    text: msgText,
                    // MODIFICA QUI: Rimuovi o commenta la riga parseMode: ParseMode.Markdown
                    // parseMode: ParseMode.Markdown, // <--- Commenta o rimuovi questa riga
                    cancellationToken: cancellationToken);
            }
        }

        private bool IsUserAuthorized(long userId)
        {
            return _botConfiguration.AuthorizedAdminIds.Contains(userId);
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, System.Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Polling error: {ErrorMessage}", exception.Message);
            return Task.CompletedTask;
        }
    }
}