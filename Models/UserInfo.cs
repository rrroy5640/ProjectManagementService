using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectManagementService.Models
{
    public class UserInfo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public required string Email { get; set; }
        public required string Name { get; set; }
    }
}