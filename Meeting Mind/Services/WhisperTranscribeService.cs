using System.IO;
using System.Threading.Tasks;
using Whisper.net;
using System;
using System.Linq;

namespace MeetingMind.Services
{
    public class WhisperTranscribeService
    {
        private readonly string _modelPath;
        private readonly string _modelDirectory;
        
        public WhisperTranscribeService(string modelPath)
        {
            _modelPath = modelPath;
            _modelDirectory = Path.GetDirectoryName(_modelPath) ?? "";
        }

        public async Task<string> TranscribeAsync(string audioFilePath)
        {
            try
            {
                // Ensure model directory exists
                if (!Directory.Exists(_modelDirectory))
                {
                    Directory.CreateDirectory(_modelDirectory);
                }

                // Download model if not present
                if (!File.Exists(_modelPath))
                {
                    Console.WriteLine($"Model not found at {_modelPath}. Downloading...");
                    await DownloadModelAsync();
                }

                // Verify audio file exists and has content
                if (!File.Exists(audioFilePath))
                {
                    throw new FileNotFoundException($"Audio file not found: {audioFilePath}");
                }

                var fileInfo = new FileInfo(audioFilePath);
                if (fileInfo.Length == 0)
                {
                    throw new InvalidOperationException("Audio file is empty");
                }

                Console.WriteLine($"Starting transcription of {audioFilePath} (Size: {fileInfo.Length} bytes)");

                using var factory = WhisperFactory.FromPath(_modelPath);
                using var processor = factory.CreateBuilder()
                    .WithLanguage("en")
                    .WithSegmentEventHandler(segment => 
                    {
                        Console.WriteLine($"Segment: {segment.Text}");
                    })
                    .Build();
                
                using var fileStream = File.OpenRead(audioFilePath);
                var transcript = "";
                
                await foreach (var segment in processor.ProcessAsync(fileStream))
                {
                    transcript += segment.Text + " ";
                    Console.WriteLine($"Processed segment: {segment.Text}");
                }
                
                var result = transcript.Trim();
                Console.WriteLine($"Transcription complete. Result length: {result.Length}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in WhisperTranscribeService: {ex.Message}");
                throw;
            }
        }

        private async Task DownloadModelAsync()
        {
            try
            {
                Console.WriteLine("Downloading Whisper model...");
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10); // Increase timeout for large download
                
                // Try multiple model URLs in case one fails
                var urls = new[]
                {
                    "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
                    "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin" // Smaller fallback
                };
                
                foreach (var url in urls)
                {
                    try
                    {
                        Console.WriteLine($"Trying to download from: {url}");
                        var data = await client.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(_modelPath, data);
                        Console.WriteLine($"Model downloaded successfully from {url}. Size: {data.Length} bytes");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to download from {url}: {ex.Message}");
                        if (url == urls.Last())
                            throw; // Re-throw if all URLs failed
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading model: {ex.Message}");
                throw new InvalidOperationException($"Failed to download Whisper model: {ex.Message}", ex);
            }
        }
    }
}
