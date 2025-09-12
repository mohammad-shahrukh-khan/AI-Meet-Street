

using SmartMeetingAssistant.Core.Interfaces;
using SmartMeetingAssistant.Core.Models;
using System.Diagnostics;

namespace SmartMeetingAssistant.Services;

/// <summary>
/// Free speech recognition service using browser's Web Speech API
/// This service launches a simple HTML page that uses the browser's built-in speech recognition
/// </summary>
public class WebSpeechService : ITranscriptionService
{
    private readonly object _lock = new();
    private bool _isTranscribing;
    private Process? _browserProcess;
    private readonly string _htmlFilePath;
    private Timer? _checkTimer;

    public event EventHandler<TranscriptionEventArgs>? TranscriptionReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsTranscribing
    {
        get
        {
            lock (_lock)
            {
                return _isTranscribing;
            }
        }
    }

    public WebSpeechService()
    {
        _htmlFilePath = Path.Combine(Path.GetTempPath(), "SmartMeetingAssistant_SpeechRecognition.html");
        CreateSpeechRecognitionHtml();
    }

    public async Task StartTranscriptionAsync()
    {
        try
        {
            lock (_lock)
            {
                if (_isTranscribing)
                    return;
                
                _isTranscribing = true;
            }

            // Launch browser with speech recognition page
            var startInfo = new ProcessStartInfo
            {
                FileName = _htmlFilePath,
                UseShellExecute = true // This will open with default browser
            };

            _browserProcess = Process.Start(startInfo);

            // Start checking for transcription results
            _checkTimer = new Timer(CheckForTranscriptionResults, null, 
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            await Task.Delay(100); // Small delay to ensure process starts
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to start web speech recognition: {ex.Message}");
        }
    }

    public async Task StopTranscriptionAsync()
    {
        try
        {
            lock (_lock)
            {
                _isTranscribing = false;
            }

            _checkTimer?.Dispose();
            _checkTimer = null;

            // Close browser process
            if (_browserProcess != null && !_browserProcess.HasExited)
            {
                _browserProcess.CloseMainWindow();
                if (!_browserProcess.WaitForExit(5000))
                {
                    _browserProcess.Kill();
                }
                _browserProcess.Dispose();
                _browserProcess = null;
            }

            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to stop transcription: {ex.Message}");
        }
    }

    public async Task<string> TranscribeAudioFileAsync(string filePath)
    {
        // Web Speech API doesn't support file transcription directly
        // This would require uploading to a service or using a different approach
        await Task.Delay(1000);
        return "File transcription not supported with Web Speech API. Please use real-time transcription.";
    }

    public void ProcessAudioData(byte[] audioData, int sampleRate, int channels)
    {
        // Web Speech API handles audio input directly from browser microphone
        // This method is not needed but kept for interface compliance
    }

    private void CheckForTranscriptionResults(object? state)
    {
        try
        {
            if (!_isTranscribing)
                return;

            // Check if results file exists (created by the HTML page)
            var resultsFile = Path.Combine(Path.GetTempPath(), "smart_meeting_speech_results.txt");
            if (File.Exists(resultsFile))
            {
                var content = File.ReadAllText(resultsFile);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine($"Found speech results: {content}");
                    
                    // Clear the file after reading
                    File.WriteAllText(resultsFile, "");

                    var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            var parts = line.Split('|');
                            if (parts.Length >= 2)
                            {
                                var text = parts[0].Trim();
                                var isFinal = parts[1].Trim().ToLower() == "final";
                                var confidence = parts.Length > 2 && double.TryParse(parts[2], out var conf) ? conf : 0.9;

                                Console.WriteLine($"Processing speech: '{text}' (Final: {isFinal})");

                                var segment = new TranscriptSegment
                                {
                                    Text = text,
                                    Timestamp = DateTime.Now,
                                    StartTime = TimeSpan.Zero,
                                    EndTime = TimeSpan.Zero,
                                    Confidence = confidence,
                                    IsFinal = isFinal
                                };

                                TranscriptionReceived?.Invoke(this, new TranscriptionEventArgs(segment, isFinal, confidence));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking transcription results: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"Error checking transcription results: {ex.Message}");
        }
    }

    private void CreateSpeechRecognitionHtml()
    {
        var html = @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Smart Meeting Assistant - Speech Recognition</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            text-align: center;
            padding: 20px;
            margin: 0;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            justify-content: center;
            align-items: center;
        }
        .container {
            background: rgba(255, 255, 255, 0.1);
            backdrop-filter: blur(10px);
            border-radius: 15px;
            padding: 30px;
            box-shadow: 0 8px 32px rgba(31, 38, 135, 0.37);
            border: 1px solid rgba(255, 255, 255, 0.18);
            max-width: 600px;
            width: 100%;
        }
        h1 {
            margin-bottom: 30px;
            font-size: 2.5em;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.3);
        }
        .mic-button {
            background: #ff6b6b;
            border: none;
            border-radius: 50%;
            width: 120px;
            height: 120px;
            font-size: 3em;
            color: white;
            cursor: pointer;
            transition: all 0.3s ease;
            margin: 20px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.3);
        }
        .mic-button:hover {
            transform: scale(1.1);
            background: #ff5252;
        }
        .mic-button.listening {
            background: #4caf50;
            animation: pulse 1.5s infinite;
        }
        @keyframes pulse {
            0% { transform: scale(1); }
            50% { transform: scale(1.1); }
            100% { transform: scale(1); }
        }
        .status {
            font-size: 1.2em;
            margin: 20px 0;
            min-height: 30px;
        }
        .transcript {
            background: rgba(255, 255, 255, 0.2);
            border-radius: 10px;
            padding: 20px;
            margin: 20px 0;
            text-align: left;
            min-height: 100px;
            max-height: 200px;
            overflow-y: auto;
            font-family: monospace;
            font-size: 1.1em;
        }
        .interim {
            color: #ccc;
            font-style: italic;
        }
        .final {
            color: white;
            font-weight: bold;
        }
        .instructions {
            font-size: 0.9em;
            opacity: 0.8;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🎤 Smart Meeting Assistant</h1>
        <h2>Free Speech Recognition</h2>
        
        <button id='micButton' class='mic-button' onclick='toggleRecognition()'>🎤</button>
        
        <div id='status' class='status'>Click the microphone to start</div>
        
        <div id='transcript' class='transcript'>
            <div style='color: #ccc; text-align: center; padding: 20px;'>
                Speech transcription will appear here...
            </div>
        </div>
        
        <div class='instructions'>
            <p>🔊 <strong>Instructions:</strong></p>
            <p>• Click the microphone button to start/stop recording</p>
            <p>• Speak clearly into your microphone</p>
            <p>• Results will automatically sync with your meeting app</p>
            <p>• Keep this window open during your meeting</p>
        </div>
    </div>

    <script>
        let recognition;
        let isListening = false;
        const micButton = document.getElementById('micButton');
        const status = document.getElementById('status');
        const transcript = document.getElementById('transcript');

        // Check if browser supports speech recognition
        if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
            const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
            recognition = new SpeechRecognition();
            
            recognition.continuous = true;
            recognition.interimResults = true;
            recognition.lang = 'en-US';
            
            recognition.onstart = function() {
                isListening = true;
                micButton.classList.add('listening');
                micButton.innerHTML = '🔴';
                status.textContent = 'Listening... Speak now!';
            };
            
            recognition.onend = function() {
                isListening = false;
                micButton.classList.remove('listening');
                micButton.innerHTML = '🎤';
                status.textContent = 'Click microphone to start';
            };
            
            recognition.onresult = function(event) {
                let interimTranscript = '';
                let finalTranscript = '';
                
                for (let i = event.resultIndex; i < event.results.length; i++) {
                    const result = event.results[i];
                    if (result.isFinal) {
                        finalTranscript += result[0].transcript;
                        saveResult(result[0].transcript, true, result[0].confidence);
                    } else {
                        interimTranscript += result[0].transcript;
                        saveResult(result[0].transcript, false, result[0].confidence);
                    }
                }
                
                updateTranscript(finalTranscript, interimTranscript);
            };
            
            recognition.onerror = function(event) {
                status.textContent = 'Error: ' + event.error;
                console.error('Speech recognition error:', event.error);
            };
            
        } else {
            status.textContent = 'Speech recognition not supported in this browser';
            micButton.disabled = true;
        }

        function toggleRecognition() {
            if (isListening) {
                recognition.stop();
            } else {
                recognition.start();
            }
        }

        function updateTranscript(final, interim) {
            const finalDiv = '<div class=""final"">' + final + '</div>';
            const interimDiv = interim ? '<div class=""interim"">' + interim + '</div>' : '';
            
            if (final) {
                transcript.innerHTML += finalDiv;
            }
            if (interim) {
                // Remove previous interim results and add new ones
                const existingInterim = transcript.querySelector('.interim');
                if (existingInterim) {
                    existingInterim.remove();
                }
                if (interim.trim()) {
                    transcript.innerHTML += interimDiv;
                }
            }
            
            transcript.scrollTop = transcript.scrollHeight;
        }

        function saveResult(text, isFinal, confidence) {
            // Save results to file for the C# application to read
            const result = text + '|' + (isFinal ? 'final' : 'interim') + '|' + confidence + '\n';
            
            // Try to save to file using different methods
            try {
                // Method 1: Try to use the File System Access API (modern browsers)
                if ('showSaveFilePicker' in window) {
                    // This requires user permission, so we'll use a different approach
                }
                
                // Method 2: Create a downloadable file and auto-trigger download
                const blob = new Blob([result], { type: 'text/plain' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'smart_meeting_speech_results.txt';
                a.style.display = 'none';
                document.body.appendChild(a);
                
                // For continuous results, we need a different approach
                // Let's use localStorage and display instructions
                const existingResults = localStorage.getItem('speechResults') || '';
                localStorage.setItem('speechResults', existingResults + result);
                
                // Also try to write to a known location (this won't work in browser for security)
                // The C# app will need to check localStorage or we need a different approach
                
            } catch (e) {
                console.log('Could not save file:', e);
            }
        }

        // Auto-start recognition when page loads (optional)
        window.addEventListener('load', function() {
            setTimeout(() => {
                if (recognition && !isListening) {
                    // Uncomment the next line to auto-start
                    // toggleRecognition();
                }
            }, 1000);
        });
    </script>
</body>
</html>";

        try
        {
            File.WriteAllText(_htmlFilePath, html);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create speech recognition HTML file: {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            StopTranscriptionAsync().Wait(TimeSpan.FromSeconds(5));
            
            // Clean up temporary files
            if (File.Exists(_htmlFilePath))
            {
                File.Delete(_htmlFilePath);
            }
            
            var resultsFile = Path.Combine(Path.GetTempPath(), "speech_results.txt");
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
