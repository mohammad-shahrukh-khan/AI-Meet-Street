using NAudio.Wave;
using System;
using System.IO;

namespace MeetingMind.Services
{
    public class AudioRecorderService
    {
        private WaveInEvent? waveIn;
        private WaveFileWriter? writer;
        private string? outputFilePath;
        private bool _isRecording = false;

        public bool IsRecording => _isRecording;

        public void StartRecording(string filePath)
        {
            try
            {
                outputFilePath = filePath;
                
                // Ensure directory exists
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
                
                Console.WriteLine($"Started recording to: {filePath}");
                Console.WriteLine($"Wave format: {waveIn.WaveFormat}");
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

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            try
            {
                writer?.Dispose();
                writer = null;
                waveIn?.Dispose();
                waveIn = null;
                _isRecording = false;
                
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
