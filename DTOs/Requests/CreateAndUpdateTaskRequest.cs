namespace ProjectManagementService.DTOs.Requests
{
    public class CreateAndUpdateTaskRequest
    {

        public required string Title { get; set; }
        public string? Description { get; set; }
        public required string AssignedTo { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Status { get; set; }
    }
}