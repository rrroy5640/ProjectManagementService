using ProjectManagementService.Models;

namespace ProjectManagementService.DTOs.Responses{
    public class CreateProjectResponse{
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public required List<string> ProjectMembers { get; set; }
        public List<ProjectTask>? Tasks { get; set; }
    }
}