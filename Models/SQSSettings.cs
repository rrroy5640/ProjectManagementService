namespace ProjectManagementService.Models
{
    public class SQSSettings : ISQSSettings
    {
        public required string Url { get; set; }
    }

    public interface ISQSSettings
    {
        string Url { get; set; }
    }
}