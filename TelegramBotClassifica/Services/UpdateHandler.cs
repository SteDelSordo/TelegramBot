using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBotClassifica.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text;

namespace TelegramBotClassifica.Services
{
    public interface IUpdateHandler
    {
        Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken = default);
    }

    public class UpdateHandler : IUpdateHandler
    {
        private readonly IDataService _dataService;
        private readonly ILogger<UpdateHandler> _logger;

        public UpdateHandler(IDataService dataService, ILogger<UpdateHandler> logger)
        {
            _dataService = dataService;
            _logger = logger;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken = default)
        {
            Message? message = update.Message;

            if (message is not { } msg)
                return;

            if (msg.From == null)
            {
                _logger.LogWarning("Received message with no sender. Ignoring.");
                return;
            }

            await _dataService.UpdateOrCreateUserAsync(msg.From.Id, msg.From.Username, msg.From.FirstName);

            bool shouldSendResponse = false;
            var msgText = "";

            if (msg.Chat.Type != ChatType.Private && msg.Text?.StartsWith("/") != true)
            {
                return;
            }

            _logger.LogInformation("Received message '{text}' from {chatId}.", msg.Text, msg.Chat.Id);

            string command = string.Empty;
            string arguments = string.Empty;

            if (msg.Text?.StartsWith("/") == true)
            {
                var parts = msg.Text.Split(' ', 2);
                command = parts[0].ToLowerInvariant().TrimStart('/');
                if (parts.Length > 1)
                {
                    arguments = parts[1];
                }

                switch (command)
                {
                    case "start":
                        if (msg.Chat.Type == ChatType.Private)
                        {
                            msgText = "Ciao! Sono il bot della classifica. Usa /ap <ID_utente_o_username> <coin> e /classifica per vedere i risultati. Tutti i comandi funzionano solo qui in privato.";
                            shouldSendResponse = true;
                        }
                        break;

                    case "ap":
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
                                    _logger.LogInformation("Comando /ap ricevuto: targetUser='{Target}', coins={Coins}", targetUserIdentifier, coins);

                                    long targetUserID = 0;
                                    if (long.TryParse(targetUserIdentifier, out long parsedID))
                                    {
                                        targetUserID = parsedID;
                                    }
                                    else
                                    {
                                        targetUserID = await _dataService.GetUserIdByUsernameAsync(targetUserIdentifier);
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
                                            var existingUser = await _dataService.GetUserPointsAsync(targetUserID);
                                            string? preservedUsername = existingUser?.Username;
                                            string? preservedFirstName = existingUser?.FirstName;

                                            if (!long.TryParse(targetUserIdentifier, out _))
                                            {
                                                preservedUsername = targetUserIdentifier.ToLowerInvariant().TrimStart('@');
                                            }

                                            await _dataService.AddOrUpdateUserPointsAsync(targetUserID, preservedUsername, preservedFirstName, coins);
                                            string displayName = !string.IsNullOrWhiteSpace(preservedUsername) ? $"@{preservedUsername}" : targetUserID.ToString();

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
                        if (msg.Chat.Type == ChatType.Private)
                        {
                            var leaderboard = await _dataService.GetLeaderboardAsync();
                            if (!leaderboard.Any())
                            {
                                msgText = "La classifica è vuota o nessuno ha ancora coin! Inizia ad aggiungerli.";
                            }
                            else
                            {
                                var sb = new StringBuilder();
                                sb.AppendLine($"🏆 Classifica Attuale ({DateTime.Now:dd/MM/yyyy HH:mm:ss})\n");

                                int rank = 1;
                                foreach (var entry in leaderboard)
                                {
                                    string displayName;
                                    if (!string.IsNullOrWhiteSpace(entry.Username))
                                    {
                                        displayName = "@" + entry.Username;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(entry.FirstName))
                                    {
                                        displayName = entry.FirstName;
                                    }
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
                        if (msg.Chat.Type == ChatType.Private && IsUserAuthorized(msg.From.Id))
                        {
                            try
                            {
                                await _dataService.ResetLeaderboardAsync();
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
                                var users = await _dataService.ExportKnownUsersAsync();
                                var json = Newtonsoft.Json.JsonConvert.SerializeObject(users, Newtonsoft.Json.Formatting.Indented);

                                _logger.LogInformation("Esportazione utenti JSON: {JsonData}", json);
                                msgText = "Utenti conosciuti esportati. Controlla i log della console per il JSON completo.";
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
                        if (msg.Chat.Type == ChatType.Private)
                        {
                            msgText = "Comando sconosciuto.";
                            shouldSendResponse = true;
                        }
                        break;
                }
            }

            if (shouldSendResponse && !string.IsNullOrEmpty(msgText))
            {
                await botClient.SendTextMessageAsync(
                    chatId: msg.Chat.Id,
                    text: msgText,
                    cancellationToken: cancellationToken);
            }
        }

        private bool IsUserAuthorized(long userId)
        {
            return BotConfig.AuthorizedAdminIds.Contains(userId);
        }
    }
}