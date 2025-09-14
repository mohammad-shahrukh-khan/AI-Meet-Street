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

namespace MeetingMind.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
    //private readonly AwsTranscribeService _transcribeService;
    private readonly BedrockService _bedrockService;
    private readonly WhisperTranscribeService _whisperService = new WhisperTranscribeService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetingMind", "ggml-base.bin"));
        private readonly IConfiguration _config;
    private readonly PdfExportService _pdfExportService = new PdfExportService();
    private readonly AudioRecorderService _audioRecorderService = new AudioRecorderService();
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

        public MainViewModel()
        {
            var builder = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.json");
            _config = builder.Build();
            //_transcribeService = new AwsTranscribeService(_config);
            _bedrockService = new BedrockService(_config);
            StartRecordingCommand = new RelayCommand(async _ => await StartRecording(), _ => !IsRecording && !IsProcessing);
            StopRecordingCommand = new RelayCommand(async _ => await StopRecording(), _ => IsRecording);
            ExportPdfCommand = new RelayCommand(async _ => await ExportPdf(), _ => CurrentSession != null);
            ClearSessionCommand = new RelayCommand(_ => ClearSession(), _ => Sessions.Count > 0);
            TestCommand = new RelayCommand(_ => TestFunction(), _ => true);
        }

    private Task StartRecording()
        {
            try
            {
                IsRecording = true;
                LiveTranscript = string.Empty;
                CurrentSession = new MeetingSession { StartTime = DateTime.Now };
                _audioFilePath = Path.Combine(Path.GetTempPath(), $"meetingmind_{Guid.NewGuid()}.wav");
                
                LiveTranscript = $"DEBUG: Starting recording to: {_audioFilePath}\n";
                Console.WriteLine($"Starting recording to: {_audioFilePath}");
                
                _audioRecorderService.StartRecording(_audioFilePath);
                LiveTranscript += "Recording... Speak now.\n";
                Console.WriteLine("Recording started successfully");
                
                // Check if recording actually started
                if (_audioRecorderService.IsRecording)
                {
                    LiveTranscript += "✓ Recording confirmed active\n";
                }
                else
                {
                    LiveTranscript += "✗ Recording failed to start\n";
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

                LiveTranscript += $"✓ Audio recorded successfully ({fileInfo.Length} bytes). Starting transcription...\n";
                
                string transcript = string.Empty;
                try
                {
                    LiveTranscript += "DEBUG: Starting Whisper transcription...\n";
                    Console.WriteLine("Starting Whisper transcription...");
                    transcript = await _whisperService.TranscribeAsync(_audioFilePath);
                    LiveTranscript += $"DEBUG: Transcription result: '{transcript}'\n";
                    Console.WriteLine($"Transcription result: '{transcript}'");
                    
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        LiveTranscript += "No speech detected. Please check your microphone and try speaking louder.\n";
                    }
                    else
                    {
                        LiveTranscript = transcript;
                        if (CurrentSession != null)
                        {
                            CurrentSession.Transcript = transcript;
                            try
                            {
                                LiveTranscript += "\nGenerating AI summary...\n";
                                var summaryJson = await _bedrockService.SummarizeTranscriptAsync(transcript);
                                CurrentSession.Summary = new MeetingSummary { BulletedSummary = new() { summaryJson } };
                                SummaryText = summaryJson;
                                
                                // Parse the summary into different sections
                                ParseSummarySections(summaryJson);
                                
                                LiveTranscript += "✓ Summary generated successfully\n";
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

        private void ClearSession()
        {
            Sessions.Clear();
            LiveTranscript = string.Empty;
            SummaryText = string.Empty;
            MainSummary = string.Empty;
            KeyDecisions = string.Empty;
            ActionItems = string.Empty;
            FollowUps = string.Empty;
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
            Console.WriteLine("Test button clicked - UI is working!");
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

