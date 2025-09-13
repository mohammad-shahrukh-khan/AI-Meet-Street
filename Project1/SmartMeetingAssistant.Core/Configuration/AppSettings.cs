namespace SmartMeetingAssistant.Core.Configuration;

public class AppSettings
{
    public AWSSettings AWS { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
}

public class AWSSettings
{
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public BedrockSettings Bedrock { get; set; } = new();
}

public class BedrockSettings
{
    public string ModelId { get; set; } = "anthropic.claude-3-sonnet-20240229-v1:0";
    public int MaxTokens { get; set; } = 2000;
    public double Temperature { get; set; } = 0.7;
}


public class AudioSettings
{
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public int BufferSize { get; set; } = 1024;
    public string DefaultOutputPath { get; set; } = "recordings";
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = "Data Source=meetings.db";
}
