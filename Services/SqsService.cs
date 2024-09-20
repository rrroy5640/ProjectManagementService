using Amazon.SQS;
using Amazon.SQS.Model;

namespace ProjectManagementService.Services
{
    public class SqsService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly string _sqsUrl;

        public SqsService(IAmazonSQS sqsClient, string sqsUrl)
        {
            _sqsClient = sqsClient;
            _sqsUrl = sqsUrl;
        }

        public async Task SendMessageAsync(string messageBody)
        {
            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = _sqsUrl,
                MessageBody = messageBody
            };

            await _sqsClient.SendMessageAsync(sendMessageRequest);
        }
    }
}