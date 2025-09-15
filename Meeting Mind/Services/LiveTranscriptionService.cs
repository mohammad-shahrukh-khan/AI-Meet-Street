using System;
using System.IO;
using System.Threading.Tasks;
using Whisper.net;
using System.Net.Http;

namespace MeetingMind.Services
{
    public class LiveTranscriptionService
    {
        private readonly string _modelPath;
        private readonly string _modelDirectory;
        
        public LiveTranscriptionService(string modelPath)
        {
            _modelPath = modelPath;
            _modelDirectory = Path.GetDirectoryName(_modelPath) ?? "";
        }

        public async Task EnsureModelExistsAsync()
        {
            try
            {
                Console.WriteLine($"Ensuring model exists at: {_modelPath}");
                
                // Ensure model directory exists
                if (!Directory.Exists(_modelDirectory))
                {
                    Directory.CreateDirectory(_modelDirectory);
                    Console.WriteLine($"Created model directory: {_modelDirectory}");
                }

                // Check if model file exists
                if (!File.Exists(_modelPath))
                {
                    Console.WriteLine("Model file not found, downloading...");
                    await DownloadTinyModelAsync();
                }
                else
                {
                    var modelInfo = new FileInfo(_modelPath);
                    Console.WriteLine($"Model file exists: {modelInfo.Length} bytes");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring model exists: {ex.Message}");
                throw;
            }
        }

        public async Task<string> TranscribeChunkAsync(string audioFilePath)
        {
            try
            {
                Console.WriteLine($"=== LIVE TRANSCRIPTION START === {audioFilePath}");
                Console.WriteLine($"Model path: {_modelPath}");
                Console.WriteLine($"Model directory: {_modelDirectory}");
                
                // Also write to a log file for debugging
                var logPath = Path.Combine(Path.GetTempPath(), "whisper_debug.log");
                var logContent = new System.Text.StringBuilder();
                logContent.AppendLine($"=== LIVE TRANSCRIPTION START === {audioFilePath}");
                logContent.AppendLine($"Model path: {_modelPath}");
                logContent.AppendLine($"Model directory: {_modelDirectory}");
                
                // Ensure model directory exists
                Console.WriteLine("Step 1: Checking model directory...");
                if (!Directory.Exists(_modelDirectory))
                {
                    Console.WriteLine("Creating model directory...");
                    Directory.CreateDirectory(_modelDirectory);
                    Console.WriteLine($"Created model directory: {_modelDirectory}");
                }
                else
                {
                    Console.WriteLine($"Model directory exists: {_modelDirectory}");
                }

                // Download model if not present (use tiny model for faster live transcription)
                Console.WriteLine("Step 2: Checking model file...");
                if (!File.Exists(_modelPath))
                {
                    Console.WriteLine($"Live model not found at {_modelPath}. Downloading...");
                    Console.WriteLine("Starting model download...");
                    await DownloadTinyModelAsync();
                    Console.WriteLine("Model download completed");
                }
                else
                {
                    Console.WriteLine($"Live model found at: {_modelPath}");
                    var modelInfo = new FileInfo(_modelPath);
                    Console.WriteLine($"Model file size: {modelInfo.Length} bytes");
                }

                // Verify audio file exists and has content
                Console.WriteLine("Step 3: Verifying audio file...");
                if (!File.Exists(audioFilePath))
                {
                    Console.WriteLine($"Audio file does not exist: {audioFilePath}");
                    return "";
                }

                var fileInfo = new FileInfo(audioFilePath);
                Console.WriteLine($"Audio file size: {fileInfo.Length} bytes");
                
                if (fileInfo.Length == 0)
                {
                    Console.WriteLine("Audio file is empty");
                    return "";
                }

                // Only process if file has reasonable size (at least 1KB)
                if (fileInfo.Length < 1024)
                {
                    Console.WriteLine($"Audio file too small: {fileInfo.Length} bytes");
                    return "";
                }

                // Check if it's a valid WAV file
                try
                {
                    using var testStream = File.OpenRead(audioFilePath);
                    var header = new byte[12];
                    var bytesRead = testStream.Read(header, 0, 12);
                    if (bytesRead == 12)
                    {
                        var riff = System.Text.Encoding.ASCII.GetString(header, 0, 4);
                        var wave = System.Text.Encoding.ASCII.GetString(header, 8, 4);
                        Console.WriteLine($"Audio file format: {riff} - {wave}");
                        
                        if (riff != "RIFF" || wave != "WAVE")
                        {
                            Console.WriteLine("Warning: File may not be a valid WAV file");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Warning: Could not read WAV header");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking audio file format: {ex.Message}");
                }

                Console.WriteLine("Step 4: Creating Whisper factory...");
                Console.WriteLine($"About to call WhisperFactory.FromPath with: {_modelPath}");
                using var factory = WhisperFactory.FromPath(_modelPath);
                Console.WriteLine("Whisper factory created successfully");

                Console.WriteLine("Step 5: Creating Whisper processor...");
                Console.WriteLine("About to call factory.CreateBuilder()...");
                var builder = factory.CreateBuilder();
                Console.WriteLine("Builder created, setting language...");
                builder = builder.WithLanguage("en");
                Console.WriteLine("Language set, adding segment handler...");
                builder = builder.WithSegmentEventHandler(segment => 
                {
                    Console.WriteLine($"Live segment: {segment.Text}");
                });
                Console.WriteLine("Segment handler added, building processor...");
                using var processor = builder.Build();
                Console.WriteLine("Whisper processor created successfully");
                
                Console.WriteLine("Step 6: Opening audio file...");
                using var fileStream = File.OpenRead(audioFilePath);
                Console.WriteLine("Audio file opened successfully");
                
                var transcript = "";
                
                Console.WriteLine("Step 7: Processing audio segments...");
                int segmentCount = 0;
                await foreach (var segment in processor.ProcessAsync(fileStream))
                {
                    segmentCount++;
                    Console.WriteLine($"Processing segment {segmentCount}: '{segment.Text}' (Start: {segment.Start}, End: {segment.End})");
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        transcript += segment.Text + " ";
                        Console.WriteLine($"Added to transcript: '{segment.Text}'");
                    }
                    else
                    {
                        Console.WriteLine("Segment text is empty or whitespace");
                    }
                }
                
                Console.WriteLine($"Total segments processed: {segmentCount}");
                Console.WriteLine($"Transcript before trim: '{transcript}'");
                
                var result = transcript.Trim();
                Console.WriteLine($"=== LIVE TRANSCRIPTION RESULT: '{result}' ===");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LiveTranscriptionService: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return "";
            }
        }

        private async Task DownloadTinyModelAsync()
        {
            try
            {
                Console.WriteLine("Downloading Whisper tiny model for live transcription...");
                
                // Create a more robust HTTP client with better timeout handling
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10); // Increased timeout
                
                // Use multiple fallback URLs for better reliability
                var urls = new[]
                {
                    "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
                    "https://github.com/ggerganov/whisper.cpp/raw/main/models/ggml-tiny.bin",
                    "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin"
                };
                
                byte[]? data = null;
                string? successfulUrl = null;
                
                foreach (var url in urls)
                {
                    try
                    {
                        Console.WriteLine($"Trying to download from: {url}");
                        
                        // Add progress reporting
                        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        
                        var contentLength = response.Content.Headers.ContentLength;
                        Console.WriteLine($"Content length: {contentLength} bytes");
                        
                        data = await response.Content.ReadAsByteArrayAsync();
                        successfulUrl = url;
                        Console.WriteLine($"Successfully downloaded from: {url}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to download from {url}: {ex.Message}");
                        continue;
                    }
                }
                
                if (data == null)
                {
                    throw new InvalidOperationException("Failed to download model from any URL");
                }
                
                // Write the file with progress reporting
                Console.WriteLine($"Writing model file to: {_modelPath}");
                await File.WriteAllBytesAsync(_modelPath, data);
                
                // Verify the file was written correctly
                var fileInfo = new FileInfo(_modelPath);
                if (fileInfo.Length != data.Length)
                {
                    throw new InvalidOperationException($"File size mismatch: expected {data.Length}, got {fileInfo.Length}");
                }
                
                Console.WriteLine($"Tiny model downloaded successfully from {successfulUrl}. Size: {data.Length} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading tiny model: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw new InvalidOperationException($"Failed to download Whisper tiny model: {ex.Message}", ex);
            }
        }
    }
}
