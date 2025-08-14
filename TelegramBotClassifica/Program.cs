using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramBotClassifica.Services;
using TelegramBotClassifica.Configuration;
using Microsoft.EntityFrameworkCore;

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
                    // Usa direttamente la configurazione dalle ENV variables tramite BotConfig
                    services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(BotConfig.BotToken));

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