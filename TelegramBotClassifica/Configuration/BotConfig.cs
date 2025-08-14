using System;
using System.Collections.Generic;
using System.Linq;

namespace TelegramBotClassifica.Configuration
{
    public static class BotConfig
    {
        public static string BotToken =>
            Environment.GetEnvironmentVariable("BotConfiguration__BotToken") ?? string.Empty;

        public static long TargetGroupId =>
            long.TryParse(Environment.GetEnvironmentVariable("TARGET_GROUP_ID"), out var groupId)
                ? groupId
                : 0;

        public static List<long> AuthorizedAdminIds =>
            (Environment.GetEnvironmentVariable("AUTHORIZED_ADMIN_IDS") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => long.TryParse(id, out var n) ? n : 0)
                .Where(id => id != 0)
                .ToList();
    }
}