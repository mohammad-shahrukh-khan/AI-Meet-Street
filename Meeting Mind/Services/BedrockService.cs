using Amazon;
using Amazon.Runtime;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeetingMind.Services
{
    public class BedrockService
    {
        private readonly AmazonBedrockRuntimeClient _client;
        private readonly string? _modelId;

        public BedrockService(IConfiguration config)
        {
            var awsSection = config.GetSection("AWS");
            var credentials = new BasicAWSCredentials(awsSection["AccessKey"], awsSection["SecretKey"]);
            var region = RegionEndpoint.GetBySystemName(config["Bedrock:Region"]);
            _client = new AmazonBedrockRuntimeClient(credentials, region);
            _modelId = config["Bedrock:ModelId"];
            if (string.IsNullOrWhiteSpace(_modelId))
                throw new ArgumentException("Bedrock:ModelId is not configured in appsettings.json");
        }

        public async Task<string> SummarizeTranscriptAsync(string transcript)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_modelId))
                    throw new InvalidOperationException("Bedrock model ID is not set.");

                var prompt = $@"Please analyze the following meeting transcript and provide a structured summary with the following sections:

SUMMARY:
Provide a 3-5 sentence overview of the main topics discussed.

KEY DECISIONS:
List all important decisions made during the meeting, including who made them and any context.

ACTION ITEMS:
List all tasks and action items with:
- What needs to be done
- Who is responsible (if mentioned)
- When it's due (if mentioned)

FOLLOW-UPS:
List any follow-up meetings, calls, or next steps mentioned.

Transcript:
{transcript}";

                // Use Claude 3 format
                var requestBody = new
                {
                    anthropic_version = "bedrock-2023-05-31",
                    max_tokens = 1000,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    }
                };

                var request = new InvokeModelRequest
                {
                    ModelId = _modelId,
                    ContentType = "application/json",
                    Body = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestBody)))
                };

                var response = await _client.InvokeModelAsync(request);
                using var reader = new System.IO.StreamReader(response.Body);
                var responseText = await reader.ReadToEndAsync();
                
                // Parse Claude 3 response format
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseText);
                if (jsonResponse.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
                {
                    return content[0].GetProperty("text").GetString() ?? "No summary generated";
                }
                
                return responseText;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in BedrockService: {ex.Message}");
                return $"Error generating summary: {ex.Message}";
            }
        }
    }
}
