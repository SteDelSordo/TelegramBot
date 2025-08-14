using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramBotClassifica.Services;
using TelegramBotClassifica.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder; // aggiunto
using Microsoft.AspNetCore.Hosting; // aggiunto

namespace TelegramBotClassifica
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Avvia il worker del bot Telegram in parallelo al web server
            var hostTask = CreateHostBuilder(args).Build().RunAsync();

            // Avvia un web server minimale su una porta (per Render)
            var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.MapGet("/", () => "Telegram Bot is running!");
            var webTask = app.RunAsync($"http://0.0.0.0:{port}");

            await Task.WhenAll(hostTask, webTask);
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
