namespace ProjectManagementService.DTOs.Responses
{
    public class ProjectDTO
    {
        public string? Id { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public required List<string> ProjectMembers { get; set; }
        public required string ProjectOwner { get; set; }
        public List<string>? TaskIds { get; set; }
        public required string Status { get; set; }
    }
}