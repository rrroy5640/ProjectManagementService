using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectManagementService.Models
{
    public class Project
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public required List<string> ProjectMembers { get; set; }
        public required string ProjectOwner { get; set; }

        public List<string>? TaskIds { get; set; }
        public ProjectStatus Status { get; set; }
    }

    public enum ProjectStatus
    {
        NotStarted,
        Active,
        Completed,
        PendingReview,
        Archived
    }
}