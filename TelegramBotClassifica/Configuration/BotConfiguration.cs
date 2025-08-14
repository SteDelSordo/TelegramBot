// Configuration/BotConfiguration.cs
namespace TelegramBotClassifica.Configuration
{
    public class BotConfiguration
    {
        public string BotToken { get; set; } = string.Empty;
        public long TargetGroupId { get; set; }
        public long[] AuthorizedAdminIds { get; set; } = System.Array.Empty<long>();
    }
}