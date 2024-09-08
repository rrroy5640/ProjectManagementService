namespace ProjectManagementService.DTOs.Requests
{
    public class UpdateProjectRequest
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public required List<string> ProjectMemberIds { get; set; }
        public required string ProjectOwner { get; set; }
    }
}