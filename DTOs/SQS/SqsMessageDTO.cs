using System.Text.Json;

namespace ProjectManagementService.DTOs
{
    public class SqsMessageDto
    {
        public required string MessageType { get; set; }
        public required object Payload { get; set; }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public static SqsMessageDto FromJson(string json)
        {
            return JsonSerializer.Deserialize<SqsMessageDto>(json) 
                ?? throw new InvalidOperationException("Failed to deserialize SQS message");
        }
    }
}