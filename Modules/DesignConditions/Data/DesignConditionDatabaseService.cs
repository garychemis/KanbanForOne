using Microsoft.Data.Sqlite;

namespace KanbanForOne.Modules.DesignConditions.Data;

public sealed class DesignConditionDatabaseService
{
    private const int CurrentSchemaVersion = 3;
    private readonly DesignConditionStorageOptions _paths;

    public DesignConditionDatabaseService(DesignConditionStorageOptions paths)
    {
        _paths = paths;
    }

    public string DatabasePath => _paths.DatabasePath;

    public void ClearPool()
    {
        using var connection = CreateConnection();
        SqliteConnection.ClearPool(connection);
    }

    public SqliteConnection CreateConnection()
    {
        _paths.EnsureDirectories();
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        };
        return new SqliteConnection(builder.ToString());
    }

    public async Task InitializeAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_keys = ON";
            await foreignKeys.ExecuteNonQueryAsync();
        }

        var version = await GetVersionAsync(connection);
        if (version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"设计条件数据库版本 {version} 高于当前支持版本 {CurrentSchemaVersion}。");
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        await ExecuteAsync(connection,
            """
            CREATE TABLE IF NOT EXISTS DesignConditions (
                Id TEXT PRIMARY KEY,
                ProjectNumber TEXT NOT NULL,
                IssuingDiscipline TEXT NOT NULL,
                ReceivingDiscipline TEXT NOT NULL,
                Receiver TEXT NOT NULL,
                IssuedDate TEXT NOT NULL,
                ConditionName TEXT NOT NULL,
                Revision TEXT NOT NULL DEFAULT '',
                DrawingSize TEXT NOT NULL DEFAULT '',
                DrawingCounts TEXT NOT NULL DEFAULT '',
                DrawingCount INTEGER NOT NULL DEFAULT 0 CHECK (DrawingCount >= 0),
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )
            """, transaction);
        await ExecuteAsync(connection,
            """
            CREATE TABLE IF NOT EXISTS DesignConditionAttachments (
                Id TEXT PRIMARY KEY,
                DesignConditionId TEXT NOT NULL,
                OriginalFileName TEXT NOT NULL,
                StoredFileName TEXT NOT NULL,
                RelativePath TEXT NOT NULL,
                FileExtension TEXT NOT NULL DEFAULT '',
                FileSizeBytes INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (DesignConditionId) REFERENCES DesignConditions(Id) ON DELETE CASCADE
            )
            """, transaction);
        await ExecuteAsync(connection,
            """
            CREATE TABLE IF NOT EXISTS DesignConditionOptions (
                Id TEXT PRIMARY KEY,
                OptionType TEXT NOT NULL,
                Value TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            )
            """, transaction);

        string[] indexes =
        [
            "CREATE INDEX IF NOT EXISTS IX_DesignConditions_IssuedDate ON DesignConditions(IssuedDate)",
            "CREATE INDEX IF NOT EXISTS IX_DesignConditions_ProjectDate ON DesignConditions(ProjectNumber COLLATE NOCASE, IssuedDate)",
            "CREATE INDEX IF NOT EXISTS IX_DesignConditions_Grouping ON DesignConditions(ProjectNumber COLLATE NOCASE, IssuingDiscipline COLLATE NOCASE, ReceivingDiscipline COLLATE NOCASE, ConditionName COLLATE NOCASE)",
            "CREATE INDEX IF NOT EXISTS IX_DesignConditionAttachments_Owner ON DesignConditionAttachments(DesignConditionId, SortOrder)",
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_DesignConditionOptions_TypeValue ON DesignConditionOptions(OptionType, Value COLLATE NOCASE)"
        ];
        if (version < 2 && await ColumnExistsAsync(connection, "DesignConditions", "RecordDate", transaction))
        {
            await DropIndexesReferencingColumnAsync(connection, "DesignConditions", "RecordDate", transaction);
            await ExecuteAsync(connection, "ALTER TABLE DesignConditions DROP COLUMN RecordDate", transaction);
        }
        if (version < 3 && !await ColumnExistsAsync(connection, "DesignConditions", "DrawingCounts", transaction))
        {
            await ExecuteAsync(connection, "ALTER TABLE DesignConditions ADD COLUMN DrawingCounts TEXT NOT NULL DEFAULT ''", transaction);
            // 旧库允许 DrawingCount=0，但 V3 分图幅数量要求大于 0 的整数：
            // 有图幅的 0 数量记录迁移为默认 1 张，无图幅记录保持无规格状态（由编辑弹窗兜底引导补全）。
            await ExecuteAsync(connection,
                """
                UPDATE DesignConditions SET
                    DrawingCounts = CASE
                        WHEN DrawingCount > 0 THEN CAST(DrawingCount AS TEXT)
                        WHEN DrawingSize <> '' THEN '1'
                        ELSE '' END,
                    DrawingCount = CASE
                        WHEN DrawingCount > 0 THEN DrawingCount
                        WHEN DrawingSize <> '' THEN 1
                        ELSE 0 END
                """, transaction);
        }

        foreach (var index in indexes)
        {
            await ExecuteAsync(connection, index, transaction);
        }

        await SeedOptionsAsync(connection, transaction);
        await ValidateAsync(connection, transaction);
        await SetVersionAsync(connection, CurrentSchemaVersion, transaction);
        await transaction.CommitAsync();
        await ExecuteAsync(connection, "PRAGMA journal_mode = WAL");
    }

    public async Task CheckpointAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) == 0)
            {
                return;
            }
            await Task.Delay(50);
        }
        throw new InvalidOperationException("设计条件数据库 checkpoint 失败，备份已中止。");
    }

    private static async Task SeedOptionsAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        for (var index = 0; index < 5; index++)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT OR IGNORE INTO DesignConditionOptions (Id, OptionType, Value, SortOrder, CreatedAt) VALUES ($id, 'DrawingSize', $value, $sortOrder, $createdAt)";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$value", $"A{index}");
            command.Parameters.AddWithValue("$sortOrder", index);
            command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task ValidateAsync(SqliteConnection connection, SqliteTransaction transaction)
    {
        string[] tables = ["DesignConditions", "DesignConditionAttachments", "DesignConditionOptions"];
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            command.Parameters.AddWithValue("$name", table);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            {
                throw new InvalidOperationException($"设计条件数据库初始化失败：缺少 {table} 表。");
            }
        }
        if (!await ColumnExistsAsync(connection, "DesignConditions", "DrawingCounts", transaction))
        {
            throw new InvalidOperationException("设计条件数据库初始化失败：缺少分图幅数量字段。");
        }
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table, string column, SqliteTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static async Task DropIndexesReferencingColumnAsync(SqliteConnection connection, string table, string column, SqliteTransaction transaction)
    {
        var indexNames = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type = 'index' AND tbl_name = $table AND sql IS NOT NULL AND instr(lower(sql), lower($column)) > 0";
            command.Transaction = transaction;
            command.Parameters.AddWithValue("$table", table);
            command.Parameters.AddWithValue("$column", column);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) indexNames.Add(reader.GetString(0));
        }

        foreach (var indexName in indexNames)
        {
            var quotedName = indexName.Replace("\"", "\"\"");
            await ExecuteAsync(connection, $"DROP INDEX IF EXISTS \"{quotedName}\"", transaction);
        }
    }

    private static async Task<int> GetVersionAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task SetVersionAsync(SqliteConnection connection, int version, SqliteTransaction transaction)
    {
        await ExecuteAsync(connection, $"PRAGMA user_version = {version}", transaction);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
