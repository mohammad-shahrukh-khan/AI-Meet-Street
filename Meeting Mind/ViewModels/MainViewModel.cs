using MeetingMind.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using MeetingMind.Services;
using Microsoft.Win32;
using System.Windows;
using System.Linq;
using System.Threading;

namespace MeetingMind.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
    //private readonly AwsTranscribeService _transcribeService;
    private readonly BedrockService _bedrockService;
    private readonly WhisperTranscribeService _whisperService = new WhisperTranscribeService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-base.bin"));
    private readonly LiveTranscriptionService _liveTranscriptionService = new LiveTranscriptionService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-tiny.bin"));
        private readonly IConfiguration _config;
    private readonly PdfExportService _pdfExportService = new PdfExportService();
    private readonly AudioRecorderService _audioRecorderService = new AudioRecorderService();
    private string _liveTranscriptBuffer = string.Empty;
    private string _audioFilePath = Path.Combine(Path.GetTempPath(), $"meetingmind_{Guid.NewGuid()}.wav");

        public ObservableCollection<MeetingSession> Sessions { get; } = new();
        private MeetingSession? _currentSession;
        public MeetingSession? CurrentSession
        {
            get => _currentSession;
            set { _currentSession = value; OnPropertyChanged(); }
        }

        private string? _liveTranscript = string.Empty;
        public string? LiveTranscript
        {
            get => _liveTranscript;
            set { _liveTranscript = value; OnPropertyChanged(); }
        }

        private string? _summaryText = string.Empty;
        public string? SummaryText
        {
            get => _summaryText;
            set { _summaryText = value; OnPropertyChanged(); }
        }

        private string? _mainSummary = string.Empty;
        public string? MainSummary
        {
            get => _mainSummary;
            set { _mainSummary = value; OnPropertyChanged(); }
        }

        private string? _keyDecisions = string.Empty;
        public string? KeyDecisions
        {
            get => _keyDecisions;
            set { _keyDecisions = value; OnPropertyChanged(); }
        }

        private string? _actionItems = string.Empty;
        public string? ActionItems
        {
            get => _actionItems;
            set { _actionItems = value; OnPropertyChanged(); }
        }

        private string? _followUps = string.Empty;
        public string? FollowUps
        {
            get => _followUps;
            set { _followUps = value; OnPropertyChanged(); }
        }

        private string? _suggestedQuestions = string.Empty;
        public string? SuggestedQuestions
        {
            get => _suggestedQuestions;
            set { _suggestedQuestions = value; OnPropertyChanged(); }
        }

        private string? _meetingInsights = string.Empty;
        public string? MeetingInsights
        {
            get => _meetingInsights;
            set { _meetingInsights = value; OnPropertyChanged(); }
        }

        private bool _isRecording;
        public bool IsRecording
        {
            get => _isRecording;
            set {
                if (_isRecording != value)
                {
                _isRecording = value;
                OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotRecording));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsNotRecording => !IsRecording;

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set {
                _isProcessing = value;
                OnPropertyChanged();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ClearSessionCommand { get; }
        public ICommand TestCommand { get; }
        public ICommand TestChunkCommand { get; }

        public MainViewModel()
        {
            var builder = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.json");
            _config = builder.Build();
            //_transcribeService = new AwsTranscribeService(_config);
            _bedrockService = new BedrockService(_config);
            
            // Subscribe to chunk events for live transcription
            _audioRecorderService.ChunkReady += OnChunkReady;
            Console.WriteLine("Subscribed to ChunkReady event");
            
            StartRecordingCommand = new RelayCommand(async _ => await StartRecording(), _ => !IsRecording && !IsProcessing);
            StopRecordingCommand = new RelayCommand(async _ => await StopRecording(), _ => IsRecording);
            ExportPdfCommand = new RelayCommand(async _ => await ExportPdf(), _ => CurrentSession != null);
            ClearSessionCommand = new RelayCommand(_ => ClearSession(), _ => Sessions.Count > 0);
            TestCommand = new RelayCommand(_ => TestFunction(), _ => true);
            TestChunkCommand = new RelayCommand(_ => TestChunkFunction(), _ => IsRecording);
            
            // Pre-download the live transcription model
            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Pre-downloading live transcription model...");
                    await _liveTranscriptionService.EnsureModelExistsAsync();
                    Console.WriteLine("Live transcription model ready!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to pre-download live transcription model: {ex.Message}");
                }
            });
        }

    private Task StartRecording()
        {
            try
        {
            IsRecording = true;
            LiveTranscript = string.Empty;
                _liveTranscriptBuffer = string.Empty;
            CurrentSession = new MeetingSession { StartTime = DateTime.Now };
            _audioFilePath = Path.Combine(Path.GetTempPath(), $"meetingmind_{Guid.NewGuid()}.wav");
                
                LiveTranscript = $"🎤 Starting live recording...\n";
                Console.WriteLine($"Starting recording to: {_audioFilePath}");
                
            _audioRecorderService.StartRecording(_audioFilePath);
                LiveTranscript += "🔴 Recording... Speak now. Live transcription will appear below.\n";
                Console.WriteLine("Recording started successfully");
                
                // Check if recording actually started
                if (_audioRecorderService.IsRecording)
                {
                    LiveTranscript += "✅ Recording confirmed active - Live transcription enabled\n";
                    
                    // Start periodic live transcription
                    _ = Task.Run(async () => await PeriodicLiveTranscriptionAsync());
                }
                else
                {
                    LiveTranscript += "❌ Recording failed to start\n";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting recording: {ex.Message}");
                LiveTranscript = $"ERROR starting recording: {ex.Message}\nStack: {ex.StackTrace}";
                IsRecording = false;
            }
            return Task.CompletedTask;
        }



        private async Task StopRecording()
        {
            try
            {
                LiveTranscript += "\nDEBUG: StopRecording method called!\n";
            IsRecording = false;
            IsProcessing = true;
                LiveTranscript += "DEBUG: Stopping recording...\n";
                
                Console.WriteLine("Stopping recording...");
            _audioRecorderService.StopRecording();
                
                // Wait for recording to fully stop
                await Task.Delay(1000);
                LiveTranscript += "DEBUG: Recording stopped, checking file...\n";
                
                // Check if file exists and is accessible
                if (!File.Exists(_audioFilePath))
                {
                    LiveTranscript += "ERROR: Audio file was not created. Please check microphone permissions.\n";
                    IsProcessing = false;
                    return;
                }

                var fileInfo = new FileInfo(_audioFilePath);
                LiveTranscript += $"DEBUG: Audio file found: {_audioFilePath}\n";
                LiveTranscript += $"DEBUG: File size: {fileInfo.Length} bytes\n";
                Console.WriteLine($"Audio file created: {_audioFilePath}, Size: {fileInfo.Length} bytes");
                
                if (fileInfo.Length == 0)
                {
                    LiveTranscript += "ERROR: Audio file is empty. Please check your microphone and try again.\n";
                    IsProcessing = false;
                    return;
                }

                LiveTranscript += $"✅ Audio recorded successfully ({fileInfo.Length} bytes). Finalizing transcription...\n";
                
            string transcript = string.Empty;
            try
            {
                    // Use live transcript if available, otherwise do full transcription
                    if (!string.IsNullOrWhiteSpace(_liveTranscriptBuffer))
                    {
                        transcript = _liveTranscriptBuffer.Trim();
                        LiveTranscript = $"📝 Final Transcript:\n\n{transcript}\n\nGenerating AI summary...\n";
                        Console.WriteLine($"Using live transcript: {transcript}");
                    }
                    else
                    {
                        LiveTranscript += "🔄 No live transcript available, doing full transcription...\n";
                        Console.WriteLine("Starting full Whisper transcription...");
                transcript = await _whisperService.TranscribeAsync(_audioFilePath);
                        LiveTranscript = $"📝 Final Transcript:\n\n{transcript}\n\nGenerating AI summary...\n";
                        Console.WriteLine($"Full transcription result: '{transcript}'");
                    }
                    
                    // Generate AI suggestions for the final transcript
                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        _ = Task.Run(async () => await GenerateFinalSuggestionsAsync(transcript));
                    }
                    
                if (string.IsNullOrWhiteSpace(transcript))
                {
                        LiveTranscript += "❌ No speech detected. Please check your microphone and try speaking louder.\n";
                }
                else
                {
                    if (CurrentSession != null)
                    {
                        CurrentSession.Transcript = transcript;
                            try
                            {
                                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                                {
                                    var summaryJson = await _bedrockService.SummarizeTranscriptAsync(transcript).WaitAsync(cts.Token);
                                    CurrentSession.Summary = new MeetingSummary { BulletedSummary = new() { summaryJson } };
                                    SummaryText = summaryJson;
                                    
                                    // Parse the summary into different sections
                                    ParseSummarySections(summaryJson);
                                    
                                    LiveTranscript += "✅ Summary generated successfully\n";
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                Console.WriteLine("AI summary timed out");
                                SummaryText = "AI summary generation timed out. Transcript saved successfully.";
                                LiveTranscript += "⏰ AI summary timed out - transcript saved\n";
                                
                                // Set basic summary
                                MainSummary = "AI summary generation timed out. Full transcript available above.";
                                KeyDecisions = "See transcript for details";
                                ActionItems = "See transcript for details";
                                FollowUps = "See transcript for details";
                            }
                            catch (Exception summaryEx)
                            {
                                Console.WriteLine($"Error generating summary: {summaryEx.Message}");
                                SummaryText = "Error generating summary. Transcript saved successfully.";
                                LiveTranscript += $"⚠ Summary error: {summaryEx.Message}\n";
                                
                                // Clear sections on error
                                MainSummary = "Error generating summary";
                                KeyDecisions = "";
                                ActionItems = "";
                                FollowUps = "";
                            }
                        Sessions.Add(CurrentSession);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Transcription error: {ex.Message}");
                    LiveTranscript += $"ERROR: Transcription failed: {ex.Message}\n\nPlease check:\n1. Microphone permissions\n2. Internet connection (for model download)\n3. Try speaking louder\n";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StopRecording: {ex.Message}");
                LiveTranscript += $"ERROR in StopRecording: {ex.Message}\n";
            }
            finally
            {
            IsProcessing = false;
            }
        }

        private async Task ExportPdf()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Meeting_{CurrentSession?.StartTime:yyyyMMdd_HHmm}.pdf"
            };
            if (dialog.ShowDialog() == true && CurrentSession != null)
            {
                _pdfExportService.ExportMeetingToPdf(CurrentSession, dialog.FileName);
            }
            await Task.CompletedTask;
        }

        private async void OnChunkReady(object? sender, string chunkPath)
        {
            try
            {
                if (!IsRecording) 
                {
                    Console.WriteLine("Not recording, ignoring chunk");
                    return;
                }

                Console.WriteLine($"Processing live chunk: {chunkPath}");
                
                // Update UI to show processing
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LiveTranscript = $"🔴 Recording... Processing chunk {_liveTranscriptBuffer.Split(' ').Length}...\n\n{_liveTranscriptBuffer.Trim()}";
                });
                
                // Try REAL live transcription using the same method as PeriodicLiveTranscriptionAsync
                Console.WriteLine($"Processing live chunk: {chunkPath}");
                
                string chunkTranscript = "";
                bool transcriptionSuccess = false;
                
                try
                {
                    // Create a copy of the file to avoid lock issues
                    var tempFilePath = Path.Combine(Path.GetTempPath(), $"live_chunk_{Guid.NewGuid()}.wav");
                    File.Copy(chunkPath, tempFilePath, true);
                    
                    try
                    {
                        var tinyModelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-tiny.bin");
                        var tinyWhisperService = new WhisperTranscribeService(tinyModelPath);
                        
                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                        {
                            Console.WriteLine($"Starting live chunk transcription (timeout: 8s)...");
                            chunkTranscript = await tinyWhisperService.TranscribeAsync(tempFilePath).WaitAsync(cts.Token);
                            Console.WriteLine($"Live chunk transcription result: '{chunkTranscript}'");
                            
                            if (!string.IsNullOrWhiteSpace(chunkTranscript) && chunkTranscript.Trim().Length > 3)
                            {
                                transcriptionSuccess = true;
                                Console.WriteLine($"LIVE CHUNK TRANSCRIPT SUCCESS: {chunkTranscript}");
                            }
                        }
                    }
                    finally
                    {
                        // Clean up temporary file
                        try { File.Delete(tempFilePath); } catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Live chunk transcription timed out after 8 seconds");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Live chunk transcription failed: {ex.Message}");
                }
                
                Console.WriteLine($"Chunk transcript result: '{chunkTranscript}'");
                
                if (transcriptionSuccess)
                {
                    // Update live transcript buffer
                    _liveTranscriptBuffer += chunkTranscript + " ";
                    
                    // Update UI on main thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        LiveTranscript = $"🔴 Recording... Live transcription:\n\n{_liveTranscriptBuffer.Trim()}";
                    });
                    
                    // Generate AI suggestions based on live transcript
                    _ = Task.Run(async () => await GenerateLiveSuggestionsAsync());
                    
                    Console.WriteLine($"Live transcript updated: {chunkTranscript}");
                }
                else
                {
                    Console.WriteLine("No transcript from chunk");
                    // Update UI to show no speech detected
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        LiveTranscript = $"🔴 Recording... No speech detected in chunk\n\n{_liveTranscriptBuffer.Trim()}";
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing live chunk: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LiveTranscript = $"🔴 Recording... Error processing chunk: {ex.Message}\n\n{_liveTranscriptBuffer.Trim()}";
                });
            }
        }

        private void ClearSession()
        {
            Sessions.Clear();
            LiveTranscript = string.Empty;
            _liveTranscriptBuffer = string.Empty;
            SummaryText = string.Empty;
            MainSummary = string.Empty;
            KeyDecisions = string.Empty;
            ActionItems = string.Empty;
            FollowUps = string.Empty;
            SuggestedQuestions = string.Empty;
            MeetingInsights = string.Empty;
            CurrentSession = null;
        }

        private void ParseSummarySections(string summaryJson)
        {
            try
            {
                // Initialize all sections
                MainSummary = "";
                KeyDecisions = "";
                ActionItems = "";
                FollowUps = "";

                if (string.IsNullOrWhiteSpace(summaryJson))
                {
                    MainSummary = "No summary available";
                    return;
                }

                // Split the summary by common section headers
                var lines = summaryJson.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var currentSection = "";
                var sectionContent = new List<string>();

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    // Check for section headers (case insensitive)
                    if (IsSectionHeader(trimmedLine, "summary", "overview", "main"))
                    {
                        FlushCurrentSection();
                        currentSection = "summary";
                        sectionContent.Clear();
                    }
                    else if (IsSectionHeader(trimmedLine, "decision", "decisions", "key decision"))
                    {
                        FlushCurrentSection();
                        currentSection = "decisions";
                        sectionContent.Clear();
                    }
                    else if (IsSectionHeader(trimmedLine, "action", "action item", "action items", "task", "tasks"))
                    {
                        FlushCurrentSection();
                        currentSection = "actions";
                        sectionContent.Clear();
                    }
                    else if (IsSectionHeader(trimmedLine, "follow", "follow up", "follow-up", "followups", "next step"))
                    {
                        FlushCurrentSection();
                        currentSection = "followups";
                        sectionContent.Clear();
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        sectionContent.Add(trimmedLine);
                    }
                }

                // Flush the last section
                FlushCurrentSection();

                // If no sections were found, put everything in main summary
                if (string.IsNullOrEmpty(MainSummary) && string.IsNullOrEmpty(KeyDecisions) && 
                    string.IsNullOrEmpty(ActionItems) && string.IsNullOrEmpty(FollowUps))
                {
                    MainSummary = summaryJson;
                }

                void FlushCurrentSection()
                {
                    if (sectionContent.Count > 0)
                    {
                        var content = string.Join("\n", sectionContent);
                        switch (currentSection)
                        {
                            case "summary":
                                MainSummary = content;
                                break;
                            case "decisions":
                                KeyDecisions = content;
                                break;
                            case "actions":
                                ActionItems = content;
                                break;
                            case "followups":
                                FollowUps = content;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing summary sections: {ex.Message}");
                MainSummary = summaryJson; // Fallback to raw summary
            }
        }

        private bool IsSectionHeader(string line, params string[] keywords)
        {
            var lowerLine = line.ToLowerInvariant();
            return keywords.Any(keyword => lowerLine.Contains(keyword) && 
                (lowerLine.StartsWith(keyword) || lowerLine.Contains(":") || lowerLine.Contains("-")));
        }

        private void TestFunction()
        {
            LiveTranscript = "TEST: UI is working! Button clicked successfully.\n";
            LiveTranscript += $"Current time: {DateTime.Now}\n";
            LiveTranscript += $"IsRecording: {IsRecording}\n";
            LiveTranscript += $"IsProcessing: {IsProcessing}\n";
            LiveTranscript += $"Sessions count: {Sessions.Count}\n";
            LiveTranscript += $"StopRecordingCommand CanExecute: {StopRecordingCommand.CanExecute(null)}\n";
            LiveTranscript += $"StartRecordingCommand CanExecute: {StartRecordingCommand.CanExecute(null)}\n";
            
            // Test model file paths
            var baseModelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-base.bin");
            var tinyModelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-tiny.bin");
            
            LiveTranscript += $"Base model exists: {File.Exists(baseModelPath)}\n";
            LiveTranscript += $"Tiny model exists: {File.Exists(tinyModelPath)}\n";
            
            if (File.Exists(baseModelPath))
            {
                var baseModelInfo = new FileInfo(baseModelPath);
                LiveTranscript += $"Base model size: {baseModelInfo.Length} bytes\n";
            }
            
            Console.WriteLine("Test button clicked - UI is working!");
        }

        private async void TestChunkFunction()
        {
            try
            {
                LiveTranscript += "\n🧪 TEST CHUNK: Manually testing chunk processing...\n";
                LiveTranscript += "📝 Step 1: Function started\n";
                
                if (File.Exists(_audioFilePath))
                {
                    var fileInfo = new FileInfo(_audioFilePath);
                    LiveTranscript += $"📁 Audio file exists: {fileInfo.Length} bytes\n";
                    LiveTranscript += "📝 Step 2: File check passed\n";
                    
                    if (fileInfo.Length > 1024)
                    {
                        LiveTranscript += "📝 Step 3: File size check passed\n";
                        LiveTranscript += "🔄 Processing chunk manually...\n";
                        LiveTranscript += "📝 Step 4: About to test service\n";
                        
                        // Test if the service is accessible first
                        if (_whisperService == null)
                        {
                            LiveTranscript += "❌ WhisperService is null!\n";
                            return;
                        }
                        
                        LiveTranscript += "📝 Step 5: Service is not null\n";
                        
                        // Check model file before calling the service
                        var modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-base.bin");
                        LiveTranscript += $"🔍 Checking model file at: {modelPath}\n";
                        
                        if (File.Exists(modelPath))
                        {
                            var modelInfo = new FileInfo(modelPath);
                            LiveTranscript += $"✅ Model file exists: {modelInfo.Length} bytes\n";
                        }
                        else
                        {
                            LiveTranscript += "❌ Model file does not exist - this is likely the problem!\n";
                            LiveTranscript += "🔄 The service will try to download it, which might be hanging...\n";
                        }
                        
                        // Check audio file properties
                        LiveTranscript += $"🔊 Audio file analysis:\n";
                        LiveTranscript += $"   - Size: {fileInfo.Length} bytes\n";
                        LiveTranscript += $"   - Duration estimate: {fileInfo.Length / 32000:F1} seconds\n";
                        LiveTranscript += $"   - Expected segments: {(fileInfo.Length / 32000) / 3:F0}\n";
                        
                        LiveTranscript += "🧪 Testing service call...\n";
                        LiveTranscript += "📝 Step 6: About to call async method\n";
                        
                        // Create a copy of the file to avoid file lock issues
                        var tempFilePath = Path.Combine(Path.GetTempPath(), $"test_chunk_{Guid.NewGuid()}.wav");
                        LiveTranscript += $"📋 Creating copy of audio file to avoid lock issues...\n";
                        
                        try
                        {
                            File.Copy(_audioFilePath, tempFilePath, true);
                            LiveTranscript += $"✅ File copied to: {tempFilePath}\n";
                            
                            // Now let's actually await it with a timeout
                            LiveTranscript += "⏳ Calling async method with 8-second timeout...\n";
                            
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                            try
                            {
                                LiveTranscript += "🔄 Starting transcription process...\n";
                                var result = await _whisperService.TranscribeAsync(tempFilePath).WaitAsync(cts.Token);
                                LiveTranscript += $"✅ Method completed! Result: '{result}'\n";
                                
                                if (string.IsNullOrWhiteSpace(result))
                                {
                                    LiveTranscript += "⚠️ Empty result - no speech detected\n";
                                    LiveTranscript += "💡 Try speaking louder and more clearly\n";
                                }
                                else
                                {
                                    LiveTranscript += $"🎉 Success! Found {result.Length} characters of text\n";
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                LiveTranscript += "⏰ Method timed out after 10 seconds - it's hanging!\n";
                            }
                            catch (Exception ex)
                            {
                                LiveTranscript += $"❌ Method failed with error: {ex.Message}\n";
                            }
                        }
                        finally
                        {
                            // Clean up the temporary file
                            if (File.Exists(tempFilePath))
                            {
                                try
                                {
                                    File.Delete(tempFilePath);
                                    LiveTranscript += "🧹 Cleaned up temporary file\n";
                                }
                                catch
                                {
                                    // Ignore cleanup errors
                                }
                            }
                        }
                    }
                    else
                    {
                        LiveTranscript += "❌ Audio file too small for processing\n";
                    }
                }
                else
                {
                    LiveTranscript += "❌ Audio file does not exist\n";
                }
            }
            catch (Exception ex)
            {
                LiveTranscript += $"❌ Error in manual chunk test: {ex.Message}\n";
            }
        }

        private async Task GenerateLiveSuggestionsAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_liveTranscriptBuffer) || _liveTranscriptBuffer.Length < 50)
                {
                    Console.WriteLine("Live transcript too short for AI suggestions");
                    return;
                }

                Console.WriteLine($"🤖 Generating live AI suggestions for transcript length: {_liveTranscriptBuffer.Length}");
                Console.WriteLine($"📝 Transcript content: {_liveTranscriptBuffer.Substring(0, Math.Min(100, _liveTranscriptBuffer.Length))}...");

                var prompt = $@"You are an AI meeting assistant. Based on this live meeting transcript, analyze the conversation and provide smart suggestions:

SUGGESTED QUESTIONS: Generate 2-3 specific, actionable questions that participants should ask for clarification, deeper understanding, or to move the discussion forward. Focus on:
- Unclear points that need clarification
- Missing details that should be discussed
- Follow-up questions on important topics
- Questions to ensure everyone is aligned

MEETING INSIGHTS: Provide 1-2 key observations about:
- Important decisions being made
- Key topics being discussed
- Potential issues or concerns
- Action items that might be emerging

Transcript: {_liveTranscriptBuffer.Trim()}";

                Console.WriteLine("🔄 Calling Bedrock service for AI suggestions...");
                
                // Add timeout for AI suggestions
                string suggestions = "";
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    suggestions = await _bedrockService.GenerateSuggestionsAsync(prompt).WaitAsync(cts.Token);
                    Console.WriteLine($"✅ AI suggestions received: {suggestions?.Length ?? 0} characters");
                }
                
                Console.WriteLine($"📄 Suggestions content: {suggestions?.Substring(0, Math.Min(200, suggestions?.Length ?? 0))}...");

                if (!string.IsNullOrWhiteSpace(suggestions))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ParseLiveSuggestions(suggestions);
                    });
                    Console.WriteLine("✅ Live suggestions updated in UI");
                }
                else
                {
                    Console.WriteLine("❌ No suggestions generated by AI");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error generating live suggestions: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SuggestedQuestions = "Error generating suggestions";
                    MeetingInsights = "Error generating insights";
                });
            }
        }

        private async Task GenerateFinalSuggestionsAsync(string transcript)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transcript) || transcript.Length < 50)
                    return;

                Console.WriteLine($"Generating final AI suggestions for transcript length: {transcript.Length}");

                var prompt = $@"You are an AI meeting assistant. Based on this meeting transcript, analyze the conversation and provide smart suggestions:

SUGGESTED QUESTIONS: Generate 3-5 specific, actionable questions that participants should ask for clarification, deeper understanding, or to move the discussion forward. Focus on:
- Unclear points that need clarification
- Missing details that should be discussed
- Follow-up questions on important topics
- Questions to ensure everyone is aligned
- Questions about implementation details
- Questions about timelines and priorities

MEETING INSIGHTS: Provide 2-3 key observations about:
- Important decisions being made
- Key topics being discussed
- Potential issues or concerns
- Action items that might be emerging
- Risks or challenges identified
- Next steps that should be taken

Transcript: {transcript.Trim()}";

                var suggestions = await _bedrockService.GenerateSuggestionsAsync(prompt);
                Console.WriteLine($"Final AI suggestions received: {suggestions?.Length ?? 0} characters");

                if (!string.IsNullOrWhiteSpace(suggestions))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ParseLiveSuggestions(suggestions);
                    });
                }
                else
                {
                    Console.WriteLine("No final suggestions generated by AI");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating final suggestions: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SuggestedQuestions = "Error generating suggestions";
                    MeetingInsights = "Error generating insights";
                });
            }
        }

        private void ParseLiveSuggestions(string suggestions)
        {
            try
            {
                var lines = suggestions.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var questions = new List<string>();
                var insights = new List<string>();

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("SUGGESTED QUESTIONS:", StringComparison.OrdinalIgnoreCase) ||
                        trimmedLine.StartsWith("QUESTIONS:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract questions
                        var questionText = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(questionText))
                            questions.Add(questionText);
                    }
                    else if (trimmedLine.StartsWith("MEETING INSIGHTS:", StringComparison.OrdinalIgnoreCase) ||
                             trimmedLine.StartsWith("INSIGHTS:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract insights
                        var insightText = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(insightText))
                            insights.Add(insightText);
                    }
                    else if (trimmedLine.StartsWith("CLARIFICATIONS:", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add clarifications to questions
                        var clarificationText = trimmedLine.Substring(trimmedLine.IndexOf(':') + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(clarificationText))
                            questions.Add(clarificationText);
                    }
                    else if (trimmedLine.Length > 10 && (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("•") || trimmedLine.StartsWith("*")))
                    {
                        // Handle bullet points
                        var cleanText = trimmedLine.Substring(1).Trim();
                        if (cleanText.Length > 10)
                        {
                            if (cleanText.ToLower().Contains("question") || cleanText.ToLower().Contains("ask"))
                                questions.Add(cleanText);
                            else
                                insights.Add(cleanText);
                        }
                    }
                }

                // Update UI
                SuggestedQuestions = questions.Count > 0 ? string.Join("\n", questions) : "No specific questions suggested yet. Keep the discussion going!";
                MeetingInsights = insights.Count > 0 ? string.Join("\n", insights) : "Listening for insights...";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing live suggestions: {ex.Message}");
                SuggestedQuestions = "Error generating suggestions";
                MeetingInsights = "Error generating insights";
            }
        }

        private async Task PeriodicLiveTranscriptionAsync()
        {
            var counter = 0;
            while (IsRecording)
            {
                try
                {
                    await Task.Delay(8000); // Wait 8 seconds for more audio
                    counter++;
                    
                    if (IsRecording && File.Exists(_audioFilePath))
                    {
                        var fileInfo = new FileInfo(_audioFilePath);
                        var duration = fileInfo.Length / 16000; // Rough duration in seconds
                        
                        Console.WriteLine($"Live transcription update #{counter} - File size: {fileInfo.Length} bytes");
                        
                        // Only try transcription if we have enough audio (at least 5 seconds)
                        if (duration >= 5)
                        {
                            // Try REAL transcription using the SAME method as Test Chunk
                            string realTranscript = "";
                            bool transcriptionSuccess = false;
                            var timeoutSeconds = Math.Min(10, Math.Max(3, duration / 10)); // 3-10 seconds based on duration
                            
                            try
                            {
                                // Create a copy of the file to avoid lock issues (same as Test Chunk)
                                var tempFilePath = Path.Combine(Path.GetTempPath(), $"live_chunk_{Guid.NewGuid()}.wav");
                                File.Copy(_audioFilePath, tempFilePath, true);
                                
                                try
                                {
                                    var tinyModelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-tiny.bin");
                                    var tinyWhisperService = new WhisperTranscribeService(tinyModelPath);
                                    
                                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
                                    {
                                        Console.WriteLine($"Starting live transcription (timeout: {timeoutSeconds}s)...");
                                        realTranscript = await tinyWhisperService.TranscribeAsync(tempFilePath).WaitAsync(cts.Token);
                                        Console.WriteLine($"Live transcription result: '{realTranscript}'");
                                        
                                        if (!string.IsNullOrWhiteSpace(realTranscript) && realTranscript.Trim().Length > 3)
                                        {
                                            transcriptionSuccess = true;
                                            Console.WriteLine($"LIVE TRANSCRIPT SUCCESS: {realTranscript}");
                                        }
                                    }
                                }
                                finally
                                {
                                    // Clean up temporary file
                                    try { File.Delete(tempFilePath); } catch { }
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                Console.WriteLine($"Live transcription timed out after {timeoutSeconds} seconds");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Live transcription failed: {ex.Message}");
                            }
                            
                            // Show result - ONLY real transcription
                            var liveText = "";
                            if (transcriptionSuccess)
                            {
                                // Show REAL transcript
                                liveText = $"🔴 Recording...\n\n{realTranscript}";
                                _liveTranscriptBuffer = realTranscript;
                                
                                // Generate AI suggestions based on live transcript
                                _ = Task.Run(async () => await GenerateLiveSuggestionsAsync());
                            }
                            else
                            {
                                // Show only recording status
                                liveText = $"🔴 Recording...\n\nProcessing your speech... (Duration: ~{duration}s)";
                            }
                            
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                LiveTranscript = liveText;
                            });
                            
                            Console.WriteLine($"Live transcript updated: Update #{counter} - Success: {transcriptionSuccess}");
                        }
                        else
                        {
                            // Not enough audio yet
                            var liveText = $"🔴 Recording...\n\nRecording... (Duration: ~{duration}s)";
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                LiveTranscript = liveText;
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in periodic live transcription: {ex.Message}");
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
        }

    public class RelayCommand : ICommand
    {
        private readonly Func<object?, Task>? _executeAsync;
        private readonly Predicate<object?>? _canExecute;
        private readonly Action<object?>? _executeSync;

        public RelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _executeAsync = execute;
            _canExecute = canExecute;
        }
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _executeSync = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public async void Execute(object? parameter)
        {
            if (_executeAsync != null) await _executeAsync(parameter);
            else _executeSync?.Invoke(parameter);
        }
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
    }

