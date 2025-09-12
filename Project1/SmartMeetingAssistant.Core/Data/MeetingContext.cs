using Microsoft.Data.Sqlite;
using SmartMeetingAssistant.Core.Models;
using System.Data;

namespace SmartMeetingAssistant.Core.Data;

public class MeetingContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public MeetingContext(string? connectionString = null)
    {
        _connectionString = connectionString ?? "Data Source=meetings.db";
        InitializeDatabaseAsync().Wait();
    }

    private async Task InitializeDatabaseAsync()
    {
        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        // Create tables if they don't exist
        await CreateTablesAsync();
    }

    private async Task CreateTablesAsync()
    {
        if (_connection == null) return;

        var createMeetingsTable = @"
            CREATE TABLE IF NOT EXISTS Meetings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Participants TEXT,
                Agenda TEXT,
                StartTime DATETIME NOT NULL,
                EndTime DATETIME,
                Status INTEGER NOT NULL DEFAULT 0,
                AudioFilePath TEXT,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            )";

        var createTranscriptSegmentsTable = @"
            CREATE TABLE IF NOT EXISTS TranscriptSegments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MeetingId INTEGER NOT NULL,
                Text TEXT NOT NULL,
                Speaker TEXT,
                Timestamp DATETIME NOT NULL,
                StartTime INTEGER NOT NULL,
                EndTime INTEGER,
                Confidence REAL NOT NULL DEFAULT 0.0,
                IsFinal INTEGER NOT NULL DEFAULT 0,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (MeetingId) REFERENCES Meetings(Id) ON DELETE CASCADE
            )";

        var createActionItemsTable = @"
            CREATE TABLE IF NOT EXISTS ActionItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MeetingId INTEGER NOT NULL,
                Description TEXT NOT NULL,
                AssignedTo TEXT,
                DueDate DATETIME,
                Priority INTEGER NOT NULL DEFAULT 1,
                Status INTEGER NOT NULL DEFAULT 0,
                Confidence REAL NOT NULL DEFAULT 0.0,
                SourceTranscriptSegmentId INTEGER,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (MeetingId) REFERENCES Meetings(Id) ON DELETE CASCADE,
                FOREIGN KEY (SourceTranscriptSegmentId) REFERENCES TranscriptSegments(Id)
            )";

        var createKeyPointsTable = @"
            CREATE TABLE IF NOT EXISTS KeyPoints (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MeetingId INTEGER NOT NULL,
                Summary TEXT NOT NULL,
                Details TEXT,
                Category INTEGER NOT NULL DEFAULT 0,
                Confidence REAL NOT NULL DEFAULT 0.0,
                SourceTranscriptSegmentId INTEGER,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (MeetingId) REFERENCES Meetings(Id) ON DELETE CASCADE,
                FOREIGN KEY (SourceTranscriptSegmentId) REFERENCES TranscriptSegments(Id)
            )";

        var createDecisionsTable = @"
            CREATE TABLE IF NOT EXISTS Decisions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MeetingId INTEGER NOT NULL,
                Summary TEXT NOT NULL,
                Details TEXT,
                DecisionMaker TEXT,
                Impact INTEGER NOT NULL DEFAULT 1,
                Confidence REAL NOT NULL DEFAULT 0.0,
                SourceTranscriptSegmentId INTEGER,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (MeetingId) REFERENCES Meetings(Id) ON DELETE CASCADE,
                FOREIGN KEY (SourceTranscriptSegmentId) REFERENCES TranscriptSegments(Id)
            )";

        var createQuestionsTable = @"
            CREATE TABLE IF NOT EXISTS Questions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MeetingId INTEGER NOT NULL,
                QuestionText TEXT NOT NULL,
                Context TEXT,
                Type INTEGER NOT NULL DEFAULT 0,
                Priority INTEGER NOT NULL DEFAULT 1,
                Confidence REAL NOT NULL DEFAULT 0.0,
                IsAnswered INTEGER NOT NULL DEFAULT 0,
                Answer TEXT,
                SourceTranscriptSegmentId INTEGER,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                AnsweredAt DATETIME,
                FOREIGN KEY (MeetingId) REFERENCES Meetings(Id) ON DELETE CASCADE,
                FOREIGN KEY (SourceTranscriptSegmentId) REFERENCES TranscriptSegments(Id)
            )";

        using var command = new SqliteCommand(createMeetingsTable, _connection);
        await command.ExecuteNonQueryAsync();

        command.CommandText = createTranscriptSegmentsTable;
        await command.ExecuteNonQueryAsync();

        command.CommandText = createActionItemsTable;
        await command.ExecuteNonQueryAsync();

        command.CommandText = createKeyPointsTable;
        await command.ExecuteNonQueryAsync();

        command.CommandText = createDecisionsTable;
        await command.ExecuteNonQueryAsync();

        command.CommandText = createQuestionsTable;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CreateMeetingAsync(Meeting meeting)
    {
        if (_connection == null) throw new InvalidOperationException("Database connection not initialized");

        var sql = @"
            INSERT INTO Meetings (Title, Participants, Agenda, StartTime, EndTime, Status, AudioFilePath, CreatedAt, UpdatedAt)
            VALUES (@Title, @Participants, @Agenda, @StartTime, @EndTime, @Status, @AudioFilePath, @CreatedAt, @UpdatedAt);
            SELECT last_insert_rowid();";

        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@Title", meeting.Title);
        command.Parameters.AddWithValue("@Participants", meeting.Participants ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Agenda", meeting.Agenda ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StartTime", meeting.StartTime);
        command.Parameters.AddWithValue("@EndTime", meeting.EndTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Status", (int)meeting.Status);
        command.Parameters.AddWithValue("@AudioFilePath", meeting.AudioFilePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", meeting.CreatedAt);
        command.Parameters.AddWithValue("@UpdatedAt", meeting.UpdatedAt);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<Meeting?> GetMeetingAsync(int id)
    {
        if (_connection == null) return null;

        var sql = "SELECT * FROM Meetings WHERE Id = @Id";
        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapMeetingFromReader(reader);
        }

        return null;
    }

    public async Task<List<Meeting>> GetAllMeetingsAsync()
    {
        if (_connection == null) return new List<Meeting>();

        var meetings = new List<Meeting>();
        var sql = "SELECT * FROM Meetings ORDER BY StartTime DESC";
        
        using var command = new SqliteCommand(sql, _connection);
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            meetings.Add(MapMeetingFromReader(reader));
        }

        return meetings;
    }

    public async Task UpdateMeetingAsync(Meeting meeting)
    {
        if (_connection == null) return;

        var sql = @"
            UPDATE Meetings 
            SET Title = @Title, Participants = @Participants, Agenda = @Agenda, 
                StartTime = @StartTime, EndTime = @EndTime, Status = @Status, 
                AudioFilePath = @AudioFilePath, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@Id", meeting.Id);
        command.Parameters.AddWithValue("@Title", meeting.Title);
        command.Parameters.AddWithValue("@Participants", meeting.Participants ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Agenda", meeting.Agenda ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@StartTime", meeting.StartTime);
        command.Parameters.AddWithValue("@EndTime", meeting.EndTime ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Status", (int)meeting.Status);
        command.Parameters.AddWithValue("@AudioFilePath", meeting.AudioFilePath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> AddTranscriptSegmentAsync(TranscriptSegment segment)
    {
        if (_connection == null) throw new InvalidOperationException("Database connection not initialized");

        var sql = @"
            INSERT INTO TranscriptSegments (MeetingId, Text, Speaker, Timestamp, StartTime, EndTime, Confidence, IsFinal, CreatedAt)
            VALUES (@MeetingId, @Text, @Speaker, @Timestamp, @StartTime, @EndTime, @Confidence, @IsFinal, @CreatedAt);
            SELECT last_insert_rowid();";

        using var command = new SqliteCommand(sql, _connection);
        command.Parameters.AddWithValue("@MeetingId", segment.MeetingId);
        command.Parameters.AddWithValue("@Text", segment.Text);
        command.Parameters.AddWithValue("@Speaker", segment.Speaker ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Timestamp", segment.Timestamp);
        command.Parameters.AddWithValue("@StartTime", segment.StartTime.Ticks);
        command.Parameters.AddWithValue("@EndTime", segment.EndTime?.Ticks ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Confidence", segment.Confidence);
        command.Parameters.AddWithValue("@IsFinal", segment.IsFinal ? 1 : 0);
        command.Parameters.AddWithValue("@CreatedAt", segment.CreatedAt);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private Meeting MapMeetingFromReader(SqliteDataReader reader)
    {
        return new Meeting
        {
            Id = reader.GetInt32("Id"),
            Title = reader.GetString("Title"),
            Participants = reader.IsDBNull("Participants") ? null : reader.GetString("Participants"),
            Agenda = reader.IsDBNull("Agenda") ? null : reader.GetString("Agenda"),
            StartTime = reader.GetDateTime("StartTime"),
            EndTime = reader.IsDBNull("EndTime") ? null : reader.GetDateTime("EndTime"),
            Status = (MeetingStatus)reader.GetInt32("Status"),
            AudioFilePath = reader.IsDBNull("AudioFilePath") ? null : reader.GetString("AudioFilePath"),
            CreatedAt = reader.GetDateTime("CreatedAt"),
            UpdatedAt = reader.GetDateTime("UpdatedAt")
        };
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
