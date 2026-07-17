using System.IO;
using KanbanForOne.Models;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

public sealed class DatabaseService
{
    private const int CurrentSchemaVersion = 4;

    private static readonly string[] RequiredTables = ["Tasks", "Notes", "Attachments", "ArchiveSections", "AppSettings", "WorkHourEntries"];

    private static readonly string[] CreateTableCommands =
    [
        """
        CREATE TABLE IF NOT EXISTS Tasks (
            Id TEXT PRIMARY KEY,
            Title TEXT NOT NULL,
            Description TEXT NOT NULL DEFAULT '',
            Status TEXT NOT NULL,
            Priority TEXT NOT NULL,
            TagsJson TEXT NOT NULL DEFAULT '[]',
            StartDate TEXT NULL,
            EndDate TEXT NULL,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            CompletedAt TEXT NULL,
            IsArchived INTEGER NOT NULL DEFAULT 0,
            ArchiveSectionId TEXT NULL,
            ArchivedAt TEXT NULL,
            SortOrder INTEGER NOT NULL DEFAULT 0
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS Notes (
            Id TEXT PRIMARY KEY,
            Title TEXT NOT NULL,
            Content TEXT NOT NULL DEFAULT '',
            TagsJson TEXT NOT NULL DEFAULT '[]',
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            IsArchived INTEGER NOT NULL DEFAULT 0,
            ArchiveSectionId TEXT NULL,
            ArchivedAt TEXT NULL,
            SortOrder INTEGER NOT NULL DEFAULT 0
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS ArchiveSections (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL,
            SortOrder INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            IsDefault INTEGER NOT NULL DEFAULT 0
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS Attachments (
            Id TEXT PRIMARY KEY,
            OwnerType TEXT NOT NULL,
            OwnerId TEXT NOT NULL,
            OriginalFileName TEXT NOT NULL,
            StoredFileName TEXT NOT NULL,
            RelativePath TEXT NOT NULL,
            FileExtension TEXT NOT NULL,
            FileSizeBytes INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            SortOrder INTEGER NOT NULL DEFAULT 0
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS AppSettings (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS WorkHourEntries (
            Id TEXT PRIMARY KEY,
            WorkDate TEXT NOT NULL,
            ProjectNumber TEXT NOT NULL,
            Discipline TEXT NOT NULL,
            WorkActivity TEXT NOT NULL,
            HourUnits INTEGER NOT NULL CHECK (HourUnits > 0 AND HourUnits <= 2400),
            Remark TEXT NOT NULL DEFAULT '',
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )
        """
    ];

    private static readonly string[] CreateIndexCommands =
    [
        "CREATE INDEX IF NOT EXISTS IX_Tasks_Status ON Tasks(Status)",
        "CREATE INDEX IF NOT EXISTS IX_Tasks_DateRange ON Tasks(StartDate, EndDate)",
        "CREATE INDEX IF NOT EXISTS IX_Tasks_IsArchived ON Tasks(IsArchived)",
        "CREATE INDEX IF NOT EXISTS IX_Tasks_ArchiveSection ON Tasks(IsArchived, ArchiveSectionId)",
        "CREATE INDEX IF NOT EXISTS IX_Notes_IsArchived ON Notes(IsArchived)",
        "CREATE INDEX IF NOT EXISTS IX_Notes_ArchiveSection ON Notes(IsArchived, ArchiveSectionId)",
        "CREATE INDEX IF NOT EXISTS IX_Attachments_Owner ON Attachments(OwnerType, OwnerId)",
        "CREATE UNIQUE INDEX IF NOT EXISTS UX_ArchiveSections_Name ON ArchiveSections(Name COLLATE NOCASE)",
        "CREATE INDEX IF NOT EXISTS IX_WorkHourEntries_Date ON WorkHourEntries(WorkDate)",
        "CREATE INDEX IF NOT EXISTS IX_WorkHourEntries_DateProject ON WorkHourEntries(WorkDate, ProjectNumber COLLATE NOCASE)",
        "CREATE INDEX IF NOT EXISTS IX_WorkHourEntries_DisciplineDate ON WorkHourEntries(Discipline, WorkDate)",
        "CREATE INDEX IF NOT EXISTS IX_WorkHourEntries_ActivityDate ON WorkHourEntries(WorkActivity, WorkDate)"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> RequiredColumns = new Dictionary<string, string[]>
    {
        ["Tasks"] =
        [
            "Id",
            "Title",
            "Description",
            "Status",
            "Priority",
            "TagsJson",
            "StartDate",
            "EndDate",
            "CreatedAt",
            "UpdatedAt",
            "CompletedAt",
            "IsArchived",
            "ArchiveSectionId",
            "ArchivedAt",
            "SortOrder"
        ],
        ["Notes"] =
        [
            "Id",
            "Title",
            "Content",
            "TagsJson",
            "CreatedAt",
            "UpdatedAt",
            "IsArchived",
            "ArchiveSectionId",
            "ArchivedAt",
            "SortOrder"
        ],
        ["Attachments"] =
        [
            "Id",
            "OwnerType",
            "OwnerId",
            "OriginalFileName",
            "StoredFileName",
            "RelativePath",
            "FileExtension",
            "FileSizeBytes",
            "CreatedAt",
            "SortOrder"
        ],
        ["ArchiveSections"] = ["Id", "Name", "SortOrder", "CreatedAt", "UpdatedAt", "IsDefault"],
        ["AppSettings"] = ["Key", "Value"],
        ["WorkHourEntries"] =
        [
            "Id",
            "WorkDate",
            "ProjectNumber",
            "Discipline",
            "WorkActivity",
            "HourUnits",
            "Remark",
            "CreatedAt",
            "UpdatedAt"
        ]
    };

    private readonly string? _databasePathOverride;

    public DatabaseService(string? databasePath = null)
    {
        _databasePathOverride = databasePath;
    }

    public string ProjectRoot => AppPaths.ProjectRoot;

    public string DataDirectory => AppPaths.DataRoot;

    public string AttachmentDirectory => AppPaths.AttachmentRoot;

    public string BackupDirectory => AppPaths.BackupRoot;

    public string DatabasePath => _databasePathOverride ?? AppPaths.DatabasePath;

    public SqliteConnection CreateConnection()
    {
        EnsureStorageDirectories();

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        return new SqliteConnection(builder.ToString());
    }

    public async Task InitializeAsync()
    {
        EnsureStorageDirectories();

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await InitializeSchemaAsync(connection);
    }

    private void EnsureStorageDirectories()
    {
        if (_databasePathOverride is null)
        {
            AppPaths.EnsureStorageLayout();
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
    }

    private static async Task InitializeSchemaAsync(SqliteConnection connection)
    {
        var schemaVersion = await GetSchemaVersionAsync(connection);

        if (schemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"数据库版本 {schemaVersion} 高于当前应用支持的版本 {CurrentSchemaVersion}。");
        }

        await CreateCurrentSchemaAsync(connection);
        await EnsureDefaultArchiveSectionAsync(connection);
        await ApplyMigrationsAsync(connection, schemaVersion);
        await CreateCurrentIndexesAsync(connection);
        await ValidateCurrentSchemaAsync(connection);

        if (schemaVersion != CurrentSchemaVersion)
        {
            await SetSchemaVersionAsync(connection, CurrentSchemaVersion);
        }
    }

    private static async Task<int> GetSchemaVersionAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SetSchemaVersionAsync(SqliteConnection connection, int version)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version}";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ApplyMigrationsAsync(SqliteConnection connection, int fromVersion)
    {
        _ = fromVersion;
        await EnsureColumnAsync(connection, "Tasks", "StartDate", "TEXT NULL");
        await EnsureColumnAsync(connection, "Tasks", "EndDate", "TEXT NULL");
        await EnsureColumnAsync(connection, "Tasks", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "Tasks", "ArchiveSectionId", "TEXT NULL");
        await EnsureColumnAsync(connection, "Tasks", "ArchivedAt", "TEXT NULL");
        await EnsureColumnAsync(connection, "Notes", "IsArchived", "INTEGER NOT NULL DEFAULT 0");
        await EnsureColumnAsync(connection, "Notes", "ArchiveSectionId", "TEXT NULL");
        await EnsureColumnAsync(connection, "Notes", "ArchivedAt", "TEXT NULL");

        if (await ColumnExistsAsync(connection, "Tasks", "DueDate"))
        {
            await using var migrateDatesCommand = connection.CreateCommand();
            migrateDatesCommand.CommandText =
                """
                UPDATE Tasks
                SET StartDate = COALESCE(StartDate, DueDate),
                    EndDate = COALESCE(EndDate, DueDate)
                WHERE DueDate IS NOT NULL
            """;
            await migrateDatesCommand.ExecuteNonQueryAsync();
        }

        var defaultSectionId = ArchiveSection.DefaultId.ToString();

        await using (var migrateTaskArchiveSectionCommand = connection.CreateCommand())
        {
            migrateTaskArchiveSectionCommand.CommandText =
                """
                UPDATE Tasks
                SET ArchiveSectionId = $defaultSectionId,
                    ArchivedAt = COALESCE(ArchivedAt, UpdatedAt, CreatedAt)
                WHERE IsArchived = 1
                  AND (ArchiveSectionId IS NULL OR ArchiveSectionId = '')
                """;
            migrateTaskArchiveSectionCommand.Parameters.AddWithValue("$defaultSectionId", defaultSectionId);
            await migrateTaskArchiveSectionCommand.ExecuteNonQueryAsync();
        }

        await using (var migrateNoteArchiveSectionCommand = connection.CreateCommand())
        {
            migrateNoteArchiveSectionCommand.CommandText =
                """
                UPDATE Notes
                SET ArchiveSectionId = $defaultSectionId,
                    ArchivedAt = COALESCE(ArchivedAt, UpdatedAt, CreatedAt)
                WHERE IsArchived = 1
                  AND (ArchiveSectionId IS NULL OR ArchiveSectionId = '')
                """;
            migrateNoteArchiveSectionCommand.Parameters.AddWithValue("$defaultSectionId", defaultSectionId);
            await migrateNoteArchiveSectionCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task CreateCurrentSchemaAsync(SqliteConnection connection)
    {
        foreach (var commandText in CreateTableCommands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task CreateCurrentIndexesAsync(SqliteConnection connection)
    {
        foreach (var commandText in CreateIndexCommands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureDefaultArchiveSectionAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR IGNORE INTO ArchiveSections (Id, Name, SortOrder, CreatedAt, UpdatedAt, IsDefault)
            VALUES ($id, $name, 0, $createdAt, $updatedAt, 1)
            """;
        var now = SqliteMapper.DbDate(DateTime.Now);
        command.Parameters.AddWithValue("$id", ArchiveSection.DefaultId.ToString());
        command.Parameters.AddWithValue("$name", "默认");
        command.Parameters.AddWithValue("$createdAt", now);
        command.Parameters.AddWithValue("$updatedAt", now);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ValidateCurrentSchemaAsync(SqliteConnection connection)
    {
        foreach (var tableName in RequiredTables)
        {
            if (!await TableExistsAsync(connection, tableName))
            {
                throw new InvalidOperationException($"数据库初始化失败：缺少 {tableName} 表。");
            }
        }

        foreach (var (tableName, columnNames) in RequiredColumns)
        {
            foreach (var columnName in columnNames)
            {
                if (!await ColumnExistsAsync(connection, tableName, columnName))
                {
                    throw new InvalidOperationException($"数据库初始化失败：{tableName} 表缺少 {columnName} 字段。");
                }
            }
        }
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        if (await ColumnExistsAsync(connection, tableName, columnName))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = $tableName
            """;
        command.Parameters.AddWithValue("$tableName", tableName);

        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
