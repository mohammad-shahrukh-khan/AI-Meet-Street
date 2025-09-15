using NAudio.Wave;
using System;
using System.IO;
using System.Timers;

namespace MeetingMind.Services
{
    public class AudioRecorderService
    {
        private WaveInEvent? waveIn;
        private WaveFileWriter? writer;
        private string? outputFilePath;
        private bool _isRecording = false;
        private System.Timers.Timer? _chunkTimer;
        private int _chunkCounter = 0;
        private string? _tempDirectory;
        private readonly object _lockObject = new object();
        private WaveFileWriter? _chunkWriter;
        private long _chunkStartPosition = 0;

        public bool IsRecording => _isRecording;
        public event EventHandler<string>? ChunkReady;

        public void StartRecording(string filePath)
        {
            try
        {
            outputFilePath = filePath;
                _chunkCounter = 0;
                _chunkStartPosition = 0;
                
                // Create temp directory for chunks
                _tempDirectory = Path.Combine(Path.GetTempPath(), $"meetingmind_chunks_{Guid.NewGuid()}");
                Directory.CreateDirectory(_tempDirectory);
                
                // Ensure main file directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Delete existing file if it exists
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

            waveIn = new WaveInEvent();
                // Use 16kHz mono, 16-bit PCM - optimal for Whisper
                waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
                
            writer = new WaveFileWriter(filePath, waveIn.WaveFormat);
            waveIn.StartRecording();
                _isRecording = true;
                
                // Start chunk timer for live transcription (every 5 seconds)
                _chunkTimer = new System.Timers.Timer(5000);
                _chunkTimer.Elapsed += OnChunkTimerElapsed;
                _chunkTimer.AutoReset = true; // Make sure it repeats
                _chunkTimer.Start();
                Console.WriteLine("Chunk timer started - will fire every 5 seconds");
                
                Console.WriteLine($"Started recording to: {filePath}");
                Console.WriteLine($"Wave format: {waveIn.WaveFormat}");
                Console.WriteLine($"Sample rate: {waveIn.WaveFormat.SampleRate} Hz");
                Console.WriteLine($"Channels: {waveIn.WaveFormat.Channels}");
                Console.WriteLine($"Bits per sample: {waveIn.WaveFormat.BitsPerSample}");
                Console.WriteLine($"Chunk directory: {_tempDirectory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting recording: {ex.Message}");
                throw;
            }
        }

        public void StopRecording()
        {
            try
            {
                if (waveIn != null && _isRecording)
                {
                    // Stop chunk timer
                    _chunkTimer?.Stop();
                    _chunkTimer?.Dispose();
                    _chunkTimer = null;
                    
                    waveIn.StopRecording();
                    _isRecording = false;
                    Console.WriteLine("Recording stopped");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping recording: {ex.Message}");
                throw;
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                if (writer != null && e.BytesRecorded > 0)
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                    writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing audio data: {ex.Message}");
            }
        }

        private void OnChunkTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                lock (_lockObject)
                {
                    Console.WriteLine($"Chunk timer elapsed. Recording: {_isRecording}, TempDir: {_tempDirectory}");

                    if (_isRecording && !string.IsNullOrEmpty(_tempDirectory))
                    {
                        // Check if main file exists and has content
                        if (File.Exists(outputFilePath))
                        {
                            var fileInfo = new FileInfo(outputFilePath);
                            Console.WriteLine($"Main file size: {fileInfo.Length} bytes");

                            // Only process if file has grown significantly since last chunk
                            if (fileInfo.Length > _chunkStartPosition + 20000) // Only if at least 20KB new data
                            {
                                _chunkCounter++;
                                var chunkPath = Path.Combine(_tempDirectory, $"chunk_{_chunkCounter:D3}.wav");

                                Console.WriteLine($"Creating chunk {_chunkCounter} at: {chunkPath} (new data: {fileInfo.Length - _chunkStartPosition} bytes)");

                                // Copy only the new portion of the file
                                using (var sourceStream = File.OpenRead(outputFilePath))
                                using (var chunkStream = File.Create(chunkPath))
                                {
                                    sourceStream.Position = _chunkStartPosition;
                                    sourceStream.CopyTo(chunkStream);
                                }

                                Console.WriteLine($"Chunk {_chunkCounter} created successfully (new data only)");
                                ChunkReady?.Invoke(this, chunkPath);
                                
                                // Update the start position for next chunk
                                _chunkStartPosition = fileInfo.Length;
                            }
                            else
                            {
                                Console.WriteLine($"Not enough new data ({fileInfo.Length - _chunkStartPosition} bytes), skipping chunk");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Main file does not exist: {outputFilePath}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Not recording or temp directory not set");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in chunk timer: {ex.Message}");
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            try
            {
                // Stop chunk timer
                _chunkTimer?.Stop();
                _chunkTimer?.Dispose();
                _chunkTimer = null;
                
            writer?.Dispose();
            writer = null;
            waveIn?.Dispose();
            waveIn = null;
                _isRecording = false;
                
                // Clean up temp directory
                if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
                {
                    try
                    {
                        Directory.Delete(_tempDirectory, true);
                        Console.WriteLine($"Cleaned up temp directory: {_tempDirectory}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error cleaning up temp directory: {ex.Message}");
                    }
                }
                
                if (e.Exception != null)
                {
                    Console.WriteLine($"Recording stopped with error: {e.Exception.Message}");
                }
                else
                {
                    Console.WriteLine("Recording stopped successfully");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnRecordingStopped: {ex.Message}");
            }
        }
    }
}
