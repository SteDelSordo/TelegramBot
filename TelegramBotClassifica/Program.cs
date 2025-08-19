using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramBotClassifica.Services;
using TelegramBotClassifica.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace TelegramBotClassifica
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";
            var builder = WebApplication.CreateBuilder(args);

            // Configurazione di servizi
            builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(BotConfig.BotToken));

            // MongoDB connection
            string? mongoUriConfig = builder.Configuration["MongoDb:Uri"];
            string? mongoUriEnv = Environment.GetEnvironmentVariable("MONGODB_URI");
            string mongoUri = mongoUriConfig ?? mongoUriEnv ?? throw new InvalidOperationException("MongoDB connection string is not set in configuration or environment variables.");
            var mongoDbService = new MongoDbService(mongoUri);

            // Inizializza e fai il seed dal backup solo se serve
            await mongoDbService.InitializeAsync();
            await mongoDbService.SeedFromJsonIfEmptyAsync("users_backup.json");

            builder.Services.AddSingleton<IDataService>(mongoDbService);
            builder.Services.AddScoped<IUpdateHandler, UpdateHandler>();

            var app = builder.Build();

            app.MapGet("/", () => "Telegram Bot is running!");

            app.MapPost("/webhook", async (HttpRequest request, ITelegramBotClient botClient, IUpdateHandler handler, ILogger<Program> logger) =>
            {
                string body = "";
                try
                {
                    using var reader = new StreamReader(request.Body);
                    body = await reader.ReadToEndAsync();
                    logger.LogInformation("Webhook ricevuto. Body: {Body}", body);

                    var update = JsonConvert.DeserializeObject<Telegram.Bot.Types.Update>(body);
                    if (update != null)
                    {
                        logger.LogInformation("Update deserializzato correttamente. Passo al handler.");
                        await handler.HandleUpdateAsync(botClient, update);
                    }
                    else
                    {
                        logger.LogWarning("Deserializzazione fallita: update nullo.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Errore durante la gestione della richiesta webhook. Body ricevuto: {Body}", body);
                }
                return Results.Ok();
            });

            await app.RunAsync($"http://0.0.0.0:{port}");
        }
    }
}