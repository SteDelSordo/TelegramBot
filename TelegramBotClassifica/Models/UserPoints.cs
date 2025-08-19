using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace TelegramBotClassifica.Models
{
    public class UserPoints
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [JsonIgnore]
        public string? Id { get; set; }

        [JsonProperty("userId")]
        [BsonElement("userId")]
        public long UserId { get; set; }

        [JsonProperty("username")]
        [BsonElement("username")]
        public string? Username { get; set; }

        [JsonProperty("firstName")]
        [BsonElement("firstName")]
        public string? FirstName { get; set; }

        [JsonProperty("points")]
        [BsonElement("points")]
        public int Points { get; set; }
    }
}