// Program.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramBotClassifica.Services;
using TelegramBotClassifica.Configuration;
using TelegramBotClassifica.Models;
using Microsoft.EntityFrameworkCore; // <-- LA CORREZIONE È QUI! Questo using è fondamentale.

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
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var botConfiguration = hostContext.Configuration.GetSection("BotConfiguration").Get<BotConfiguration>();
                    if (botConfiguration == null)
                    {
                        throw new System.Exception("BotConfiguration section not found.");
                    }

                    // Registra la configurazione del bot
                    services.AddSingleton(botConfiguration);

                    // Registra il client di Telegram
                    services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botConfiguration.BotToken));

                    // Registra il DbContext per SQLite, usando la ConnectionString dal file appsettings.json
                    services.AddDbContext<BotDbContext>(options =>
                        options.UseSqlite(hostContext.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=classifica.db"));

                    // Registra il nostro nuovo servizio dati
                    services.AddScoped<IDataService, SqliteDbService>();

                    // Registra il servizio del bot come Hosted Service
                    services.AddHostedService<BotService>();
                });
    }
}
