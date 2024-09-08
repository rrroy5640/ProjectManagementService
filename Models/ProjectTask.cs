using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ProjectManagementService.Models
{
    public class ProjectTask
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public required string Title { get; set; }
        public string? Description { get; set; }
        public required string AssignedTo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.NotStarted;
    }

    public enum ProjectTaskStatus{
        NotStarted,
        InProgress,
        Completed
    }
}