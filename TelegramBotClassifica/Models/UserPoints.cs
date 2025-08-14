using Newtonsoft.Json;
using System; // Aggiunto per Guid

namespace TelegramBotClassifica.Models
{
    public class UserPoints
    {
        // Questo è il campo ID richiesto da Cosmos DB.
        // Inizializziamo con un nuovo GUID per assicurare che non sia mai null.
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Soluzione: Inizializzato qui

        [JsonProperty("userId")]
        public long UserId { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; } // Reso nullable con '?'

        [JsonProperty("firstName")]
        public string? FirstName { get; set; } // Reso nullable con '?'

        [JsonProperty("points")]
        public int Points { get; set; }

        // La PartitionKey è cruciale. La inizializziamo a string.Empty per evitare il warning
        // e ci assicureremo che venga popolata correttamente prima di ogni salvataggio.
        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; } = string.Empty; // Soluzione: Inizializzato qui
    }
}