// Program.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramBotClassifica.Services;
using TelegramBotClassifica.Configuration;
using TelegramBotClassifica.Models;

namespace TelegramBotClassifica
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            await CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    // Carica le configurazioni da appsettings.json e variabili d'ambiente
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Legge la configurazione del bot (token, admin IDs, group ID)
                    var botConfiguration = hostContext.Configuration.GetSection("BotConfiguration").Get<BotConfiguration>();
                    if (botConfiguration == null)
                    {
                        throw new System.Exception("BotConfiguration section not found or invalid in appsettings.json or environment variables.");
                    }

                    // Registra l'istanza di ITelegramBotClient come Singleton
                    services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botConfiguration.BotToken));

                    // Registra il servizio del bot come Hosted Service
                    // Hosted Service è un servizio che gira in background per tutta la vita dell'applicazione
                    services.AddHostedService<BotService>();

                    // Registra la configurazione del bot come Singleton
                    services.AddSingleton(botConfiguration);

                    services.AddSingleton<ICosmosDbService, CosmosDbService>();



                    // Qui registreremo il servizio per il database Cosmos DB
                    // Per ora lo lasciamo vuoto, lo aggiungeremo nel prossimo step
                    // services.AddSingleton<ICosmosDbService, CosmosDbService>();
                });
    }
}