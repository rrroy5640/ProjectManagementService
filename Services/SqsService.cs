using Amazon.SQS;
using Amazon.SQS.Model;
using ProjectManagementService.DTOs;
using ProjectManagementService.Models;
namespace ProjectManagementService.Services
{
    public class SqsService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly string _sqsUrl;

        public SqsService(IAmazonSQS sqsClient, ISQSSettings sqsSettings)
        {
            _sqsClient = sqsClient;
            _sqsUrl = sqsSettings.Url;
        }

        public async Task SendMessageAsync(SqsMessageDto messageBody)
        {
            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = _sqsUrl,
                MessageBody = messageBody.ToJson()
            };

            await _sqsClient.SendMessageAsync(sendMessageRequest);
        }
    }
}