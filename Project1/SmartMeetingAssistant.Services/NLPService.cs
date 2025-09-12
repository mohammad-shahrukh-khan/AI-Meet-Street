using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon;
using SmartMeetingAssistant.Core.Interfaces;
using SmartMeetingAssistant.Core.Models;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace SmartMeetingAssistant.Services;

public class NLPService : INLPService
{
    private readonly AmazonBedrockRuntimeClient? _bedrockClient;
    private readonly string _modelId;
    private readonly int _maxTokens;
    private readonly double _temperature;
    private readonly bool _useBedrockFallback;

    public NLPService(string? accessKey = null, string? secretKey = null, string region = "us-east-1", string modelId = "anthropic.claude-3-sonnet-20240229-v1:0", int maxTokens = 2000, double temperature = 0.7)
    {
        _modelId = modelId;
        _maxTokens = maxTokens;
        _temperature = temperature;

        // Only initialize Bedrock client if credentials are provided
        Console.WriteLine("=== AWS BEDROCK CONNECTION TEST ===");
        
        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            Console.WriteLine("🔑 AWS Credentials: ✅ Found");
            Console.WriteLine($"📋 Access Key: {accessKey?.Substring(0, Math.Min(8, accessKey.Length))}...");
            Console.WriteLine($"🔐 Secret Key: {(string.IsNullOrEmpty(secretKey) ? "❌ Missing" : "✅ Configured")}");
            Console.WriteLine($"🌍 Region: {region}");
            Console.WriteLine($"🤖 Model: {modelId}");
            
            try
            {
                Console.WriteLine("🔧 Initializing AWS Bedrock client...");
                var config = new AmazonBedrockRuntimeConfig
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(region)
                };

                _bedrockClient = new AmazonBedrockRuntimeClient(accessKey, secretKey, config);
                _useBedrockFallback = false;
                
                Console.WriteLine("✅ AWS BEDROCK CLIENT INITIALIZED SUCCESSFULLY!");
                Console.WriteLine($"🎯 Max Tokens: {maxTokens}");
                Console.WriteLine($"🌡️ Temperature: {temperature}");
                Console.WriteLine("🚀 Ready to use AI-powered insights and questions!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ BEDROCK CONNECTION FAILED!");
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine("🔄 Falling back to pattern-based extraction");
                _useBedrockFallback = true;
            }
        }
        else
        {
            Console.WriteLine("❌ AWS CREDENTIALS NOT PROVIDED!");
            Console.WriteLine("📝 Access Key: " + (string.IsNullOrEmpty(accessKey) ? "❌ Missing" : "✅ Found"));
            Console.WriteLine("🔐 Secret Key: " + (string.IsNullOrEmpty(secretKey) ? "❌ Missing" : "✅ Found"));
            Console.WriteLine("🔄 Using pattern-based fallback mode only");
            _useBedrockFallback = true;
        }
        
        Console.WriteLine($"🎛️ Final Mode: {(_useBedrockFallback ? "❌ FALLBACK MODE" : "✅ BEDROCK AI MODE")}");
        Console.WriteLine("=======================================");
    }

    public async Task<List<ActionItem>> ExtractActionItemsAsync(string text, int meetingId)
    {
        if (!_useBedrockFallback && _bedrockClient != null)
        {
            Console.WriteLine("🤖 Using AWS Bedrock AI for action item extraction...");
            try
            {
                var prompt = $@"Human: Analyze the following meeting transcript and extract action items. 
Return ONLY a JSON array of action items with the following structure:
[
  {{
    ""description"": ""string"",
    ""assignedTo"": ""string or null"",
    ""priority"": ""Low"", ""Medium"", ""High"", or ""Critical"",
    ""confidence"": 0.0-1.0
  }}
]

Transcript:
{text}

Look for:
- Tasks mentioned to be done
- Follow-up actions
- Assignments to specific people
- Deadlines or time-sensitive items

Return only the JSON array, no other text.

Assistant: I'll analyze the transcript and extract action items.

[]";

                var response = await InvokeBedrockModelAsync(prompt);
                var jsonResponse = ExtractJsonFromResponse(response);

                var actionItemsData = JsonSerializer.Deserialize<List<ActionItemData>>(jsonResponse);
                
                return actionItemsData?.Select(item => new ActionItem
                {
                    MeetingId = meetingId,
                    Description = item.Description,
                    AssignedTo = item.AssignedTo,
                    Priority = Enum.Parse<ActionItemPriority>(item.Priority),
                    Confidence = item.Confidence,
                    Status = ActionItemStatus.Open,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList() ?? new List<ActionItem>();
            }
            catch (Exception)
            {
                // Fall through to pattern-based extraction
            }
        }

        // Fallback to pattern-based extraction
        Console.WriteLine("🔄 Using fallback pattern-based extraction for action items");
        return await FallbackExtractActionItemsAsync(text, meetingId);
    }

    public async Task<List<KeyPoint>> ExtractKeyPointsAsync(string text, int meetingId)
    {
        if (!_useBedrockFallback && _bedrockClient != null)
        {
            try
            {
                var prompt = $@"Human: Analyze the following meeting transcript and extract key points.
Return ONLY a JSON array of key points with the following structure:
[
  {{
    ""summary"": ""string"",
    ""details"": ""string or null"",
    ""category"": ""General"", ""Technical"", ""Business"", ""Strategic"", ""Risk"", or ""Opportunity"",
    ""confidence"": 0.0-1.0
  }}
]

Transcript:
{text}

Look for:
- Important decisions or conclusions
- Key insights or findings
- Critical information shared
- Strategic points discussed
- Technical details worth noting

Return only the JSON array, no other text.

Assistant: I'll analyze the transcript and extract key points.

[]";

                var response = await InvokeBedrockModelAsync(prompt);
                var jsonResponse = ExtractJsonFromResponse(response);

                var keyPointsData = JsonSerializer.Deserialize<List<KeyPointData>>(jsonResponse);
                
                return keyPointsData?.Select(item => new KeyPoint
                {
                    MeetingId = meetingId,
                    Summary = item.Summary,
                    Details = item.Details,
                    Category = Enum.Parse<KeyPointCategory>(item.Category),
                    Confidence = item.Confidence,
                    CreatedAt = DateTime.UtcNow
                }).ToList() ?? new List<KeyPoint>();
            }
            catch (Exception)
            {
                // Fall through to pattern-based extraction
            }
        }

        // Fallback to pattern-based extraction
        return await FallbackExtractKeyPointsAsync(text, meetingId);
    }

    public async Task<List<Decision>> ExtractDecisionsAsync(string text, int meetingId)
    {
        if (!_useBedrockFallback && _bedrockClient != null)
        {
            try
            {
                var prompt = $@"Human: Analyze the following meeting transcript and extract decisions.
Return ONLY a JSON array of decisions with the following structure:
[
  {{
    ""summary"": ""string"",
    ""details"": ""string or null"",
    ""decisionMaker"": ""string or null"",
    ""impact"": ""Low"", ""Medium"", ""High"", or ""Critical"",
    ""confidence"": 0.0-1.0
  }}
]

Transcript:
{text}

Look for:
- Explicit decisions made during the meeting
- Agreements reached
- Approvals given
- Choices between options
- Final determinations on topics

Return only the JSON array, no other text.

Assistant: I'll analyze the transcript and extract decisions.

[]";

                var response = await InvokeBedrockModelAsync(prompt);
                var jsonResponse = ExtractJsonFromResponse(response);

                var decisionsData = JsonSerializer.Deserialize<List<DecisionData>>(jsonResponse);
                
                return decisionsData?.Select(item => new Decision
                {
                    MeetingId = meetingId,
                    Summary = item.Summary,
                    Details = item.Details,
                    DecisionMaker = item.DecisionMaker,
                    Impact = Enum.Parse<DecisionImpact>(item.Impact),
                    Confidence = item.Confidence,
                    CreatedAt = DateTime.UtcNow
                }).ToList() ?? new List<Decision>();
            }
            catch (Exception)
            {
                // Fall through to pattern-based extraction
            }
        }

        // Fallback to pattern-based extraction
        return await FallbackExtractDecisionsAsync(text, meetingId);
    }

    public async Task<List<Question>> ExtractQuestionsAsync(string text, int meetingId)
    {
        if (!_useBedrockFallback && _bedrockClient != null)
        {
            try
            {
                var prompt = $@"Human: Analyze the following meeting transcript and suggest relevant questions or doubts that could be raised during the meeting to clarify points, address concerns, or improve the discussion.
Return ONLY a JSON array of questions with the following structure:
[
  {{
    ""questionText"": ""string"",
    ""context"": ""string or null"",
    ""type"": ""Clarification"", ""Concern"", ""Suggestion"", ""FollowUp"", ""Technical"", ""Process"", ""Timeline"", or ""Resource"",
    ""priority"": ""Low"", ""Medium"", ""High"", or ""Urgent"",
    ""confidence"": 0.0-1.0
  }}
]

Transcript:
{text}

Look for:
- Points that need clarification or more details
- Potential concerns or risks that should be addressed
- Missing information that should be discussed
- Follow-up questions based on what was said
- Technical aspects that might need explanation
- Process or timeline questions
- Resource or budget concerns

Generate helpful, relevant questions that would improve the meeting discussion.

Return only the JSON array, no other text.

Assistant: I'll analyze the transcript and suggest relevant questions.";

                Console.WriteLine($"🧠 Sending question extraction prompt to Bedrock...");
                var response = await InvokeBedrockModelAsync(prompt);
                Console.WriteLine($"🤖 Bedrock response: {response}");
                
                var jsonResponse = ExtractJsonFromResponse(response);
                Console.WriteLine($"📝 Extracted JSON: {jsonResponse}");

                var questionsData = JsonSerializer.Deserialize<List<QuestionData>>(jsonResponse);
                Console.WriteLine($"✅ Deserialized {questionsData?.Count ?? 0} questions");
                
                return questionsData?.Select(item => new Question
                {
                    MeetingId = meetingId,
                    QuestionText = item.QuestionText,
                    Context = item.Context,
                    Type = Enum.Parse<QuestionType>(item.Type),
                    Priority = Enum.Parse<QuestionPriority>(item.Priority),
                    Confidence = item.Confidence,
                    IsAnswered = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList() ?? new List<Question>();
            }
            catch (Exception)
            {
                // Fall through to pattern-based extraction
            }
        }

        // Fallback to pattern-based extraction
        return await FallbackExtractQuestionsAsync(text, meetingId);
    }

    public async Task<string> SummarizeTranscriptAsync(List<TranscriptSegment> segments)
    {
        if (!_useBedrockFallback && _bedrockClient != null)
        {
            try
            {
                var fullTranscript = string.Join("\n", segments.Select(s => s.Text));
                
                var prompt = $@"Human: Summarize the following meeting transcript into a concise, well-structured summary.
Include the main topics discussed, key outcomes, and important points.
Keep it professional and organized.

Transcript:
{fullTranscript}

Assistant: I'll provide a concise summary of the meeting.";

                var response = await InvokeBedrockModelAsync(prompt);
                return response;
            }
            catch (Exception)
            {
                // Fall through to pattern-based summarization
            }
        }

        // Fallback to pattern-based summarization
        return FallbackSummarizeTranscript(segments);
    }

    public async Task<List<string>> ExtractTopicsAsync(string text)
    {
        if (!_useBedrockFallback && _bedrockClient != null)
        {
            try
            {
                var prompt = $@"Human: Analyze the following text and extract the main topics discussed.
Return ONLY a JSON array of topic strings:
[""topic1"", ""topic2"", ""topic3""]

Text:
{text}

Return only the JSON array, no other text.

Assistant: I'll extract the main topics.

[]";

                var response = await InvokeBedrockModelAsync(prompt);
                var jsonResponse = ExtractJsonFromResponse(response);

                return JsonSerializer.Deserialize<List<string>>(jsonResponse) ?? new List<string>();
            }
            catch (Exception)
            {
                // Fall through to pattern-based extraction
            }
        }

        // Fallback to pattern-based extraction
        return FallbackExtractTopics(text);
    }

    public async Task<double> AnalyzeSentimentAsync(string text)
    {
        if (!_useBedrockFallback && _bedrockClient != null)
        {
            try
            {
                var prompt = $@"Human: Analyze the sentiment of the following text and return a single number between -1.0 (very negative) and 1.0 (very positive), where 0.0 is neutral.
Return ONLY the number, no other text.

Text:
{text}

Assistant: ";

                var response = await InvokeBedrockModelAsync(prompt);
                
                if (double.TryParse(response.Trim(), out double sentiment))
                {
                    return Math.Max(-1.0, Math.Min(1.0, sentiment));
                }
            }
            catch (Exception)
            {
                // Fall through to pattern-based analysis
            }
        }

        // Fallback to pattern-based sentiment analysis
        return FallbackAnalyzeSentiment(text);
    }

    // AWS Bedrock helper methods
    private async Task<string> InvokeBedrockModelAsync(string prompt)
    {
        if (_bedrockClient == null) throw new InvalidOperationException("Bedrock client not initialized");

        var request = new InvokeModelRequest
        {
            ModelId = _modelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = CreateClaudeRequestBody(prompt)
        };

        var response = await _bedrockClient.InvokeModelAsync(request);
        return ParseClaudeResponse(response.Body);
    }

    private MemoryStream CreateClaudeRequestBody(string prompt)
    {
        var requestBody = new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = _maxTokens,
            temperature = _temperature,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    private string ParseClaudeResponse(MemoryStream responseBody)
    {
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var json = reader.ReadToEnd();
        
        using var document = JsonDocument.Parse(json);
        var content = document.RootElement.GetProperty("content");
        if (content.ValueKind == JsonValueKind.Array && content.GetArrayLength() > 0)
        {
            return content[0].GetProperty("text").GetString() ?? string.Empty;
        }
        
        return string.Empty;
    }

    private string ExtractJsonFromResponse(string response)
    {
        // Extract JSON array from response
        var startIndex = response.IndexOf('[');
        var endIndex = response.LastIndexOf(']');
        
        if (startIndex >= 0 && endIndex > startIndex)
        {
            return response.Substring(startIndex, endIndex - startIndex + 1);
        }
        
        return "[]";
    }

    // Fallback methods using pattern matching (for when Bedrock is unavailable)
    private async Task<List<ActionItem>> FallbackExtractActionItemsAsync(string text, int meetingId)
    {
        var actionItems = new List<ActionItem>();
        
        var actionKeywords = new[]
        {
            @"(?i)(will|should|need to|must|action item|follow up|assign|responsible for|by next|due)",
            @"(?i)(todo|to do|task|action|assignment)"
        };

        foreach (var keyword in actionKeywords)
        {
            var matches = Regex.Matches(text, $@"[^.!?]*{keyword}[^.!?]*[.!?]");
            foreach (Match match in matches)
            {
                if (match.Success && match.Value.Length > 10)
                {
                    var description = match.Value.Trim().TrimEnd('.', '!', '?');
                    
                    // Extract potential assignee
                    var assigneeMatch = Regex.Match(description, @"(?i)(assign|give|for)\s+(\w+)", RegexOptions.IgnoreCase);
                    string? assignedTo = assigneeMatch.Success ? assigneeMatch.Groups[2].Value : null;

                    actionItems.Add(new ActionItem
                    {
                        MeetingId = meetingId,
                        Description = description,
                        AssignedTo = assignedTo,
                        Priority = ActionItemPriority.Medium,
                        Confidence = 0.7,
                        Status = ActionItemStatus.Open,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        return actionItems.Take(10).ToList(); // Limit to avoid spam
    }

    private async Task<List<KeyPoint>> FallbackExtractKeyPointsAsync(string text, int meetingId)
    {
        var keyPoints = new List<KeyPoint>();
        
        var keywordPatterns = new[]
        {
            @"(?i)(important|key point|note that|remember|critical|significant|main|primary)",
            @"(?i)(decision|conclusion|result|outcome|finding)"
        };

        foreach (var pattern in keywordPatterns)
        {
            var matches = Regex.Matches(text, $@"[^.!?]*{pattern}[^.!?]*[.!?]");
            foreach (Match match in matches)
            {
                if (match.Success && match.Value.Length > 15)
                {
                    var summary = match.Value.Trim().TrimEnd('.', '!', '?');
                    
                    keyPoints.Add(new KeyPoint
                    {
                        MeetingId = meetingId,
                        Summary = summary,
                        Category = KeyPointCategory.General,
                        Confidence = 0.6,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        return keyPoints.Take(10).ToList();
    }

    private async Task<List<Decision>> FallbackExtractDecisionsAsync(string text, int meetingId)
    {
        var decisions = new List<Decision>();
        
        var decisionPatterns = new[]
        {
            @"(?i)(decided|decision|we'll go with|approved|agreed|chosen|selected)",
            @"(?i)(final|conclude|determine|resolve)"
        };

        foreach (var pattern in decisionPatterns)
        {
            var matches = Regex.Matches(text, $@"[^.!?]*{pattern}[^.!?]*[.!?]");
            foreach (Match match in matches)
            {
                if (match.Success && match.Value.Length > 15)
                {
                    var summary = match.Value.Trim().TrimEnd('.', '!', '?');
                    
                    decisions.Add(new Decision
                    {
                        MeetingId = meetingId,
                        Summary = summary,
                        Impact = DecisionImpact.Medium,
                        Confidence = 0.6,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        return decisions.Take(10).ToList();
    }

    private async Task<List<Question>> FallbackExtractQuestionsAsync(string text, int meetingId)
    {
        Console.WriteLine("🔄 Using fallback question generation...");
        var questions = new List<Question>();
        
        // Enhanced pattern-based question generation
        var concernPatterns = new[]
        {
            @"(?i)(problem|issue|concern|risk|challenge|difficulty|obstacle|blocker)",
            @"(?i)(unclear|confusing|ambiguous|uncertain|vague|complicated)",
            @"(?i)(budget|cost|resource|timeline|deadline|delay)"
        };

        var clarificationNeeded = new[]
        {
            @"(?i)(maybe|perhaps|possibly|might|could be|not sure|think|assume)",
            @"(?i)(need to check|verify|confirm|validate|investigate|research)",
            @"(?i)(what if|how about|should we|do we need|can we|will we)"
        };

        var actionPatterns = new[]
        {
            @"(?i)(need to|should|must|have to|will|going to|plan to)",
            @"(?i)(assign|delegate|responsible|owner|who will|task)"
        };

        // Generate concern-based questions
        foreach (var pattern in concernPatterns)
        {
            var matches = Regex.Matches(text, $@"[^.!?]*{pattern}[^.!?]*[.!?]");
            foreach (Match match in matches)
            {
                if (match.Success && match.Value.Length > 15)
                {
                    var context = match.Value.Trim().TrimEnd('.', '!', '?');
                    questions.Add(new Question
                    {
                        MeetingId = meetingId,
                        QuestionText = $"Can we discuss the potential impact of: {context.ToLower()}?",
                        Context = context,
                        Type = QuestionType.Concern,
                        Priority = QuestionPriority.Medium,
                        Confidence = 0.6,
                        IsAnswered = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // Generate clarification questions
        foreach (var pattern in clarificationNeeded)
        {
            var matches = Regex.Matches(text, $@"[^.!?]*{pattern}[^.!?]*[.!?]");
            foreach (Match match in matches)
            {
                if (match.Success && match.Value.Length > 15)
                {
                    var context = match.Value.Trim().TrimEnd('.', '!', '?');
                    questions.Add(new Question
                    {
                        MeetingId = meetingId,
                        QuestionText = $"Could you clarify: {context.ToLower()}?",
                        Context = context,
                        Type = QuestionType.Clarification,
                        Priority = QuestionPriority.Medium,
                        Confidence = 0.5,
                        IsAnswered = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // Add smart contextual questions based on content analysis
        var words = text.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;

        if (wordCount > 20) // Only for substantial content
        {
            // Add relevant contextual questions
            if (text.Contains("project", StringComparison.OrdinalIgnoreCase))
            {
                questions.Add(new Question
                {
                    MeetingId = meetingId,
                    QuestionText = "What are the key milestones and deliverables for this project?",
                    Context = "Project discussion detected",
                    Type = QuestionType.Timeline,
                    Priority = QuestionPriority.High,
                    Confidence = 0.8,
                    IsAnswered = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (text.Contains("decision", StringComparison.OrdinalIgnoreCase) || 
                text.Contains("decide", StringComparison.OrdinalIgnoreCase))
            {
                questions.Add(new Question
                {
                    MeetingId = meetingId,
                    QuestionText = "Who has the authority to make this decision and by when?",
                    Context = "Decision-making discussion detected",
                    Type = QuestionType.Process,
                    Priority = QuestionPriority.High,
                    Confidence = 0.8,
                    IsAnswered = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (text.Contains("team", StringComparison.OrdinalIgnoreCase) || 
                text.Contains("resource", StringComparison.OrdinalIgnoreCase))
            {
                questions.Add(new Question
                {
                    MeetingId = meetingId,
                    QuestionText = "Do we have the right resources and team members for this initiative?",
                    Context = "Resource/team discussion detected",
                    Type = QuestionType.Resource,
                    Priority = QuestionPriority.Medium,
                    Confidence = 0.7,
                    IsAnswered = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (text.Contains("technical", StringComparison.OrdinalIgnoreCase) || 
                text.Contains("system", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("development", StringComparison.OrdinalIgnoreCase))
            {
                questions.Add(new Question
                {
                    MeetingId = meetingId,
                    QuestionText = "Are there any technical dependencies or risks we should consider?",
                    Context = "Technical discussion detected",
                    Type = QuestionType.Technical,
                    Priority = QuestionPriority.Medium,
                    Confidence = 0.7,
                    IsAnswered = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        Console.WriteLine($"🎯 Generated {questions.Count} fallback questions");
        
        // Limit to avoid too many questions and prioritize by relevance
        return questions.OrderByDescending(q => q.Confidence)
                      .ThenByDescending(q => q.Priority)
                      .Take(8)
                      .ToList();
    }

    private string FallbackSummarizeTranscript(List<TranscriptSegment> segments)
    {
        if (!segments.Any())
            return "No transcript available.";

        var fullText = string.Join(" ", segments.Select(s => s.Text));
        
        // Simple summarization - take first few sentences and key sentences
        var sentences = fullText.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .Where(s => s.Length > 10)
                                .ToList();

        var summary = new List<string>();
        
        // Add first few sentences
        summary.AddRange(sentences.Take(3));
        
        // Add sentences with key words
        var keyWords = new[] { "important", "decision", "action", "key", "main", "critical", "result" };
        var keySentences = sentences
            .Where(s => keyWords.Any(kw => s.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            .Take(3);
        
        summary.AddRange(keySentences);

        return string.Join(". ", summary.Distinct()) + ".";
    }

    private List<string> FallbackExtractTopics(string text)
    {
        var topics = new List<string>();
        
        // Simple topic extraction based on common meeting topics
        var topicPatterns = new Dictionary<string, string[]>
        {
            ["Budget"] = new[] { "budget", "cost", "expense", "money", "financial" },
            ["Planning"] = new[] { "plan", "strategy", "roadmap", "timeline", "schedule" },
            ["Development"] = new[] { "develop", "build", "create", "implement", "code" },
            ["Marketing"] = new[] { "marketing", "promotion", "campaign", "sales", "customer" },
            ["Operations"] = new[] { "operations", "process", "workflow", "efficiency", "productivity" },
            ["HR"] = new[] { "hiring", "employee", "staff", "team", "personnel" }
        };

        foreach (var topicPair in topicPatterns)
        {
            if (topicPair.Value.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                topics.Add(topicPair.Key);
            }
        }

        return topics;
    }

    private double FallbackAnalyzeSentiment(string text)
    {
        // Simple sentiment analysis based on positive/negative words
        var positiveWords = new[] { "good", "great", "excellent", "positive", "success", "achieve", "win", "happy", "pleased" };
        var negativeWords = new[] { "bad", "terrible", "negative", "fail", "problem", "issue", "concern", "worry", "difficult" };

        var words = text.ToLower().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        
        var positiveCount = words.Count(w => positiveWords.Contains(w));
        var negativeCount = words.Count(w => negativeWords.Contains(w));
        
        if (positiveCount + negativeCount == 0)
            return 0.0; // Neutral

        var sentiment = (double)(positiveCount - negativeCount) / (positiveCount + negativeCount);
        return Math.Max(-1.0, Math.Min(1.0, sentiment));
    }

    // Helper classes for JSON deserialization
    private class ActionItemData
    {
        public string Description { get; set; } = string.Empty;
        public string? AssignedTo { get; set; }
        public string Priority { get; set; } = "Medium";
        public double Confidence { get; set; }
    }

    private class KeyPointData
    {
        public string Summary { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string Category { get; set; } = "General";
        public double Confidence { get; set; }
    }

    private class DecisionData
    {
        public string Summary { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? DecisionMaker { get; set; }
        public string Impact { get; set; } = "Medium";
        public double Confidence { get; set; }
    }

    private class QuestionData
    {
        public string QuestionText { get; set; } = string.Empty;
        public string? Context { get; set; }
        public string Type { get; set; } = "Clarification";
        public string Priority { get; set; } = "Medium";
        public double Confidence { get; set; }
    }
}