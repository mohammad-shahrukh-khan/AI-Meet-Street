using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using SmartMeetingAssistant.Core.Interfaces;
using SmartMeetingAssistant.Core.Models;
using SmartMeetingAssistant.Core.Data;
using SmartMeetingAssistant.Core.Configuration;
using SmartMeetingAssistant.Services;

namespace SmartMeetingAssistant.Desktop;

public partial class MainWindow : Window
{
    private readonly IAudioService _audioService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly INLPService _nlpService;
    private readonly MeetingContext _meetingContext;
    private readonly AppSettings _appSettings;
    private Meeting? _currentMeeting;
    private readonly List<TranscriptSegment> _currentTranscript = new();

    public MainWindow()
    {
        InitializeComponent();
        
        // Load configuration from appsettings.json
        _appSettings = LoadConfiguration();
        
            // Initialize services with configuration
            _audioService = new AudioService();
            Console.WriteLine("🚀 Creating WhisperTranscriptionService...");
            _transcriptionService = new WhisperTranscriptionService(); // OpenAI Whisper local transcription
            Console.WriteLine("✅ WhisperTranscriptionService created successfully");
        _nlpService = new NLPService(
            _appSettings.AWS.AccessKey,
            _appSettings.AWS.SecretKey,
            _appSettings.AWS.Region,
            _appSettings.AWS.Bedrock.ModelId,
            _appSettings.AWS.Bedrock.MaxTokens,
            _appSettings.AWS.Bedrock.Temperature);
        
        // Test Bedrock connection on startup
        _ = TestBedrockConnectionAsync();
        _meetingContext = new MeetingContext(_appSettings.Database.ConnectionString);
        
        // Subscribe to events
        _audioService.AudioDataReceived += OnAudioDataReceived;
        _audioService.ErrorOccurred += OnAudioError;
        _transcriptionService.TranscriptionReceived += OnTranscriptionReceived;
        _transcriptionService.ErrorOccurred += OnTranscriptionError;
        
        // Set default values for demo
        MeetingTitleBox.Text = "Q4 Planning Meeting";
        ParticipantsBox.Text = "John, Sarah, Mike, Lisa";
        AgendaBox.Text = @"1. Review Q3 performance metrics
2. Discuss new feature prioritization
3. Assign Q4 development tasks
4. Budget allocation discussion";
    }

    private async void StartMeetingButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(MeetingTitleBox.Text))
            {
                MessageBox.Show("Please enter a meeting title.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create meeting record
            _currentMeeting = new Meeting
            {
                Title = MeetingTitleBox.Text,
                Participants = ParticipantsBox.Text,
                Agenda = AgendaBox.Text,
                StartTime = DateTime.Now,
                Status = MeetingStatus.InProgress
            };

            var meetingId = await _meetingContext.CreateMeetingAsync(_currentMeeting);
            _currentMeeting.Id = meetingId;

            // Start services
            Console.WriteLine("🎵 Starting audio service...");
            await _audioService.StartRecordingAsync();
            Console.WriteLine("🎤 Starting transcription service...");
            await _transcriptionService.StartTranscriptionAsync();
            Console.WriteLine("✅ All services started");

            // Update UI
            StartMeetingButton.IsEnabled = false;
            StopMeetingButton.IsEnabled = true;
            StatusText.Text = "Recording";
            StatusText.Parent.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.Red));
            FooterStatus.Text = "Meeting in progress - Recording audio and transcribing...";
            AudioIndicator.Fill = new SolidColorBrush(Colors.Green);
            AIIndicator.Fill = new SolidColorBrush(Colors.Orange);
            TranscriptText.Text = "Meeting started. Listening for speech...\n\n";

            // Clear previous insights
            ActionItemsPanel.Children.Clear();
            KeyPointsPanel.Children.Clear();
            DecisionsPanel.Children.Clear();
            QuestionsPanel.Children.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start meeting: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StopMeetingButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Stop services
            await _audioService.StopRecordingAsync();
            await _transcriptionService.StopTranscriptionAsync();

            // Update meeting record
            if (_currentMeeting != null)
            {
                _currentMeeting.EndTime = DateTime.Now;
                _currentMeeting.Status = MeetingStatus.Completed;
                // TODO: Update meeting in database
            }

            // Update UI
            StartMeetingButton.IsEnabled = true;
            StopMeetingButton.IsEnabled = false;
            ExportButton.IsEnabled = true;
            StatusText.Text = "Completed";
            StatusText.Parent.SetValue(Border.BackgroundProperty, new SolidColorBrush(Colors.Blue));
            FooterStatus.Text = "Meeting completed. Ready to export report.";
            AudioIndicator.Fill = new SolidColorBrush(Colors.Red);
            AIIndicator.Fill = new SolidColorBrush(Colors.Green);

            TranscriptText.Text += "\n\n--- Meeting Ended ---";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop meeting: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // TODO: Implement export functionality
            MessageBox.Show("Export functionality will be implemented in the next phase.", 
                "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnAudioDataReceived(object? sender, AudioDataEventArgs e)
    {
        // Send audio data to transcription service
        _transcriptionService.ProcessAudioData(e.Data, e.SampleRate, e.Channels);
        
        Dispatcher.Invoke(() =>
        {
            AudioIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
        });
    }

    private void OnAudioError(object? sender, string error)
    {
        Dispatcher.Invoke(() =>
        {
            FooterStatus.Text = $"Audio Error: {error}";
            AudioIndicator.Fill = new SolidColorBrush(Colors.Red);
        });
    }

    private void OnTranscriptionReceived(object? sender, TranscriptionEventArgs e)
    {
        Console.WriteLine($"🎤 TRANSCRIPTION RECEIVED: '{e.Segment.Text}' (Final: {e.IsFinal}, Confidence: {e.Confidence})");
        
        Dispatcher.Invoke(() =>
        {
            if (e.IsFinal && !string.IsNullOrWhiteSpace(e.Segment.Text))
            {
                Console.WriteLine($"📝 Adding to transcript: '{e.Segment.Text}'");
                
                // Add to transcript
                _currentTranscript.Add(e.Segment);
                
                // Update UI
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                TranscriptText.Text += $"[{timestamp}] {e.Segment.Text}\n";
                
                // Auto-scroll to bottom
                TranscriptScrollViewer.ScrollToBottom();
                
                // Process with NLP for real-time insights
                _ = ProcessTranscriptForInsightsAsync(e.Segment.Text);
            }
            else
            {
                Console.WriteLine($"⚠️ Skipping transcription: IsFinal={e.IsFinal}, IsEmpty={string.IsNullOrWhiteSpace(e.Segment.Text)}");
            }
        });
    }

    private void OnTranscriptionError(object? sender, string error)
    {
        Dispatcher.Invoke(() =>
        {
            FooterStatus.Text = $"Transcription Error: {error}";
            AIIndicator.Fill = new SolidColorBrush(Colors.Red);
        });
    }

    private async Task ProcessTranscriptForInsightsAsync(string text)
    {
        try
        {
            if (_currentMeeting == null) return;

            // Use NLP service to extract insights
            var actionItems = await _nlpService.ExtractActionItemsAsync(text, _currentMeeting.Id);
            var keyPoints = await _nlpService.ExtractKeyPointsAsync(text, _currentMeeting.Id);
            var decisions = await _nlpService.ExtractDecisionsAsync(text, _currentMeeting.Id);
            Console.WriteLine("🤔 Extracting questions from text...");
            var questions = await _nlpService.ExtractQuestionsAsync(text, _currentMeeting.Id);
            Console.WriteLine($"💡 Generated {questions.Count} questions");

            // Update UI on the main thread
            Dispatcher.Invoke(() =>
            {
                foreach (var actionItem in actionItems)
                {
                    AddActionItemToUI(actionItem.Description);
                }

                foreach (var keyPoint in keyPoints)
                {
                    AddKeyPointToUI(keyPoint.Summary);
                }

                foreach (var decision in decisions)
                {
                    AddDecisionToUI(decision.Summary);
                }

                foreach (var question in questions)
                {
                    Console.WriteLine($"❓ Adding question: '{question.QuestionText}' (Type: {question.Type}, Priority: {question.Priority})");
                    AddQuestionToUI(question.QuestionText, question.Type.ToString(), question.Priority.ToString());
                }
            });
        }
        catch (Exception ex)
        {
            // Log error but don't crash the app
            Console.WriteLine($"Error processing insights: {ex.Message}");
        }
    }

    private bool ContainsActionKeywords(string text)
    {
        var keywords = new[] { "will do", "action item", "follow up", "by next", "responsible for", "assign" };
        return keywords.Any(keyword => text.ToLower().Contains(keyword));
    }

    private bool ContainsDecisionKeywords(string text)
    {
        var keywords = new[] { "decided", "decision", "we'll go with", "approved", "agreed" };
        return keywords.Any(keyword => text.ToLower().Contains(keyword));
    }

    private bool ContainsKeyPointKeywords(string text)
    {
        var keywords = new[] { "important", "key point", "note that", "remember", "critical" };
        return keywords.Any(keyword => text.ToLower().Contains(keyword));
    }

    private void AddActionItemToUI(string text)
    {
        var actionItem = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 249, 196)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 5)
        };

        var textBlock = new TextBlock
        {
            Text = $"📋 {text.Trim()}",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };

        actionItem.Child = textBlock;
        ActionItemsPanel.Children.Insert(0, actionItem);

        // Remove placeholder text
        if (ActionItemsPanel.Children.Count > 1 && 
            ActionItemsPanel.Children[^1] is TextBlock placeholder && 
            placeholder.Text.Contains("No action items"))
        {
            ActionItemsPanel.Children.RemoveAt(ActionItemsPanel.Children.Count - 1);
        }
    }

    private void AddDecisionToUI(string text)
    {
        var decision = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(196, 255, 196)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 5)
        };

        var textBlock = new TextBlock
        {
            Text = $"✅ {text.Trim()}",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };

        decision.Child = textBlock;
        DecisionsPanel.Children.Insert(0, decision);

        // Remove placeholder text
        if (DecisionsPanel.Children.Count > 1 && 
            DecisionsPanel.Children[^1] is TextBlock placeholder && 
            placeholder.Text.Contains("No decisions"))
        {
            DecisionsPanel.Children.RemoveAt(DecisionsPanel.Children.Count - 1);
        }
    }

    private void AddKeyPointToUI(string text)
    {
        var keyPoint = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(196, 230, 255)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 5)
        };

        var textBlock = new TextBlock
        {
            Text = $"💡 {text.Trim()}",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };

        keyPoint.Child = textBlock;
        KeyPointsPanel.Children.Insert(0, keyPoint);

        // Remove placeholder text
        if (KeyPointsPanel.Children.Count > 1 && 
            KeyPointsPanel.Children[^1] is TextBlock placeholder && 
            placeholder.Text.Contains("No key points"))
        {
            KeyPointsPanel.Children.RemoveAt(KeyPointsPanel.Children.Count - 1);
        }
    }

    private void AddQuestionToUI(string questionText, string type, string priority)
    {
        var question = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)), // Light pink/red background
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 5)
        };

        var stackPanel = new StackPanel();

        // Question text
        var questionTextBlock = new TextBlock
        {
            Text = $"❓ {questionText}",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };

        // Type and priority info
        var infoTextBlock = new TextBlock
        {
            Text = $"Type: {type} | Priority: {priority}",
            FontSize = 10,
            Foreground = new SolidColorBrush(Colors.Gray),
            Margin = new Thickness(0, 3, 0, 0)
        };

        stackPanel.Children.Add(questionTextBlock);
        stackPanel.Children.Add(infoTextBlock);
        question.Child = stackPanel;

        QuestionsPanel.Children.Insert(0, question);

        // Remove placeholder text
        if (QuestionsPanel.Children.Count > 1 && 
            QuestionsPanel.Children[^1] is TextBlock placeholder && 
            placeholder.Text.Contains("No questions"))
        {
            QuestionsPanel.Children.RemoveAt(QuestionsPanel.Children.Count - 1);
        }
    }

    private AppSettings LoadConfiguration()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables() // Allow environment variables to override settings
                .Build();

            var appSettings = new AppSettings();
            configuration.Bind(appSettings);
            
            return appSettings;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load configuration: {ex.Message}. Using default settings.", 
                "Configuration Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            
            // Return default settings if configuration fails
            return new AppSettings();
        }
    }

    private async Task TestBedrockConnectionAsync()
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine("=== BEDROCK FUNCTIONALITY TEST ===");
            Console.WriteLine("🔍 Testing if Bedrock AI features work...");
            
            // Simple test - try to extract action items from a test string
            var testText = "We need to complete the project documentation by Friday and assign John to review the requirements.";
            Console.WriteLine($"📝 Test Input: \"{testText}\"");
            
            var actionItems = await _nlpService.ExtractActionItemsAsync(testText, 0);
            
            if (actionItems.Count > 0)
            {
                Console.WriteLine("🎉 BEDROCK AI FEATURES WORKING PERFECTLY!");
                Console.WriteLine($"✅ Successfully generated {actionItems.Count} action items:");
                foreach (var item in actionItems)
                {
                    Console.WriteLine($"   📋 {item.Description} (Priority: {item.Priority})");
                }
                
                Dispatcher.Invoke(() =>
                {
                    FooterStatus.Text = "AWS Bedrock: ✅ Connected & Working";
                });
            }
            else
            {
                Console.WriteLine("⚠️ NO AI FEATURES DETECTED - Using basic pattern matching");
                Console.WriteLine("🔄 This means you'll get simpler insights and questions");
                
                Dispatcher.Invoke(() =>
                {
                    FooterStatus.Text = "AWS Bedrock: ⚠️ Fallback Mode";
                });
            }
            
            Console.WriteLine("===================================");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ BEDROCK TEST COMPLETELY FAILED!");
            Console.WriteLine($"❌ Error Details: {ex.Message}");
            Console.WriteLine("🔄 Application will use basic pattern matching only");
            
            Dispatcher.Invoke(() =>
            {
                FooterStatus.Text = "AWS Bedrock: ❌ Connection Failed";
            });
            
            Console.WriteLine("===================================");
            Console.WriteLine();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Cleanup
        _audioService?.Dispose();
        _transcriptionService?.Dispose();
        _meetingContext?.Dispose();
        base.OnClosed(e);
    }
}