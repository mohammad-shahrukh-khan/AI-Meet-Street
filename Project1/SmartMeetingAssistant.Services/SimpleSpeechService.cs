using SmartMeetingAssistant.Core.Interfaces;
using SmartMeetingAssistant.Core.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SmartMeetingAssistant.Services;

/// <summary>
/// Simple speech recognition service that creates a local web server
/// and serves an HTML page with Web Speech API for real speech recognition
/// </summary>
public class SimpleSpeechService : ITranscriptionService
{
    private readonly object _lock = new();
    private bool _isTranscribing;
    private HttpListener? _httpListener;
    private readonly int _port = 8765;
    private readonly Queue<string> _transcriptionQueue = new();
    private Timer? _processingTimer;

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

            // Start local HTTP server
            await StartHttpServer();

            // Start processing queue
            _processingTimer = new Timer(ProcessTranscriptionQueue, null, 
                TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

            // Open browser to the speech recognition page
            var url = $"http://localhost:{_port}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            Console.WriteLine($"Speech recognition started at {url}");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to start speech recognition: {ex.Message}");
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

            _processingTimer?.Dispose();
            _processingTimer = null;

            _httpListener?.Stop();
            _httpListener?.Close();
            _httpListener = null;

            await Task.Delay(100);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to stop transcription: {ex.Message}");
        }
    }

    public async Task<string> TranscribeAudioFileAsync(string filePath)
    {
        await Task.Delay(1000);
        return "File transcription not supported with Web Speech API. Please use real-time transcription.";
    }

    public void ProcessAudioData(byte[] audioData, int sampleRate, int channels)
    {
        // Web Speech API handles audio input directly from browser microphone
        // This method is not needed but kept for interface compliance
    }

    private async Task StartHttpServer()
    {
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_port}/");
            _httpListener.Start();

            Console.WriteLine($"HTTP server started on port {_port}");

            // Handle requests in background
            _ = Task.Run(async () =>
            {
                while (_httpListener?.IsListening == true)
                {
                    try
                    {
                        var context = await _httpListener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"HTTP server error: {ex.Message}");
                        break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start HTTP server: {ex.Message}");
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            if (request.HttpMethod == "GET" && request.Url?.AbsolutePath == "/")
            {
                // Serve the HTML page
                var html = GetSpeechRecognitionHtml();
                var buffer = Encoding.UTF8.GetBytes(html);
                
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else if (request.HttpMethod == "POST" && request.Url?.AbsolutePath == "/speech")
            {
                // Handle speech results
                using var reader = new StreamReader(request.InputStream);
                var json = reader.ReadToEnd();
                
                Console.WriteLine($"Received speech data: {json}");
                
                lock (_lock)
                {
                    _transcriptionQueue.Enqueue(json);
                }

                // Send OK response
                var okBuffer = Encoding.UTF8.GetBytes("OK");
                response.ContentType = "text/plain";
                response.ContentLength64 = okBuffer.Length;
                response.OutputStream.Write(okBuffer, 0, okBuffer.Length);
            }

            response.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling request: {ex.Message}");
        }
    }

    private void ProcessTranscriptionQueue(object? state)
    {
        try
        {
            if (!_isTranscribing)
                return;

            while (_transcriptionQueue.Count > 0)
            {
                string json;
                lock (_lock)
                {
                    if (_transcriptionQueue.Count == 0)
                        break;
                    json = _transcriptionQueue.Dequeue();
                }

                try
                {
                    var speechData = JsonSerializer.Deserialize<SpeechResult>(json);
                    if (speechData != null && !string.IsNullOrWhiteSpace(speechData.Text))
                    {
                        Console.WriteLine($"Processing speech: '{speechData.Text}' (Final: {speechData.IsFinal})");

                        var segment = new TranscriptSegment
                        {
                            Text = speechData.Text,
                            Timestamp = DateTime.Now,
                            StartTime = TimeSpan.Zero,
                            EndTime = TimeSpan.Zero,
                            Confidence = speechData.Confidence,
                            IsFinal = speechData.IsFinal
                        };

                        TranscriptionReceived?.Invoke(this, new TranscriptionEventArgs(segment, speechData.IsFinal, speechData.Confidence));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing speech data: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing transcription queue: {ex.Message}");
        }
    }

    private string GetSpeechRecognitionHtml()
    {
        return @"<!DOCTYPE html>
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
        <h2>Real Speech Recognition</h2>
        
        <button id='micButton' class='mic-button' onclick='toggleRecognition()'>🎤</button>
        
        <div id='status' class='status'>Click the microphone to start</div>
        
        <div id='transcript' class='transcript'>
            <div style='color: #ccc; text-align: center; padding: 20px;'>
                Your speech will appear here in real-time...
            </div>
        </div>
        
        <div class='instructions'>
            <p>🔊 <strong>Instructions:</strong></p>
            <p>• Click the microphone button to start/stop recording</p>
            <p>• Speak clearly into your microphone</p>
            <p>• Results automatically sync with your meeting app</p>
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
                        sendToApp(result[0].transcript, true, result[0].confidence);
                    } else {
                        interimTranscript += result[0].transcript;
                        sendToApp(result[0].transcript, false, result[0].confidence);
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

        function sendToApp(text, isFinal, confidence) {
            const data = {
                text: text,
                isFinal: isFinal,
                confidence: confidence || 0.9
            };
            
            // Send to C# application via HTTP POST
            fetch('/speech', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(data)
            }).catch(err => {
                console.log('Could not send to app:', err);
            });
        }

        // Auto-start recognition when page loads
        window.addEventListener('load', function() {
            setTimeout(() => {
                if (recognition && !isListening) {
                    status.textContent = 'Ready! Click the microphone to start speaking.';
                }
            }, 1000);
        });
    </script>
</body>
</html>";
    }

    private class SpeechResult
    {
        public string Text { get; set; } = string.Empty;
        public bool IsFinal { get; set; }
        public double Confidence { get; set; }
    }

    public void Dispose()
    {
        try
        {
            StopTranscriptionAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
