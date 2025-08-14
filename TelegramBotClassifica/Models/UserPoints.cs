using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace TelegramBotClassifica.Models
{
    public class UserPoints
    {
        [Key] // Dice a EF Core che questo è l'ID primario autoincrementante
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("firstName")]
        public string? FirstName { get; set; }

        [JsonProperty("points")]
        public int Points { get; set; }
    }
}