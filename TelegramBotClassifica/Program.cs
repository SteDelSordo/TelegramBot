using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramBotClassifica.Services;
using TelegramBotClassifica.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text.Json;

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
            builder.Services.AddDbContext<BotDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=classifica.db"));
            builder.Services.AddScoped<IDataService, SqliteDbService>();
            builder.Services.AddScoped<IUpdateHandler, UpdateHandler>(); // Vedi sotto

            var app = builder.Build();

            app.MapGet("/", () => "Telegram Bot is running!");

            // Endpoint webhook: riceve update da Telegram
            app.MapPost("/webhook", async (HttpRequest request, ITelegramBotClient botClient, IUpdateHandler handler) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                var update = JsonSerializer.Deserialize<Telegram.Bot.Types.Update>(body);
                if (update != null)
                {
                    await handler.HandleUpdateAsync(botClient, update);
                }
                return Results.Ok();
            });

            await app.RunAsync($"http://0.0.0.0:{port}");
        }
    }
}