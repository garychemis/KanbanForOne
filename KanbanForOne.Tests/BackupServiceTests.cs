using System.IO;
using KanbanForOne.Models;
using KanbanForOne.Services;
using Microsoft.Data.Sqlite;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Tests;

/// <summary>
/// WAL 模式与备份一致性测试：验证数据库以 WAL 模式运行、checkpoint 后主文件自包含、旧库自动升级。
/// </summary>
public sealed class BackupServiceTests
{
    [Fact]
    public async Task Initialize_creates_wal_journal_mode_database()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();

            Assert.Equal("wal", await GetJournalModeAsync(database));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Checkpoint_makes_database_file_self_contained()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var databasePath = Path.Combine(testRoot, "Kanban41.db");
            var database = new DatabaseService(databasePath);
            await database.InitializeAsync();

            var repository = new TaskRepository(database);
            await repository.UpsertAsync(new TaskItem
            {
                Title = "WAL任务",
                Status = TaskStatus.Todo,
                Priority = TaskPriority.Medium
            });

            await database.CheckpointAsync();

            // 复制主数据库文件（不带 -wal 边车），副本应自包含全部数据
            var copyPath = Path.Combine(testRoot, "copy.db");
            File.Copy(databasePath, copyPath);
            SqliteConnection.ClearAllPools();

            await using var copyConnection = new SqliteConnection($"Data Source={copyPath};Mode=ReadOnly");
            await copyConnection.OpenAsync();
            await using var command = copyConnection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Tasks WHERE Title = $title";
            command.Parameters.AddWithValue("$title", "WAL任务");
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Wal_connection_opens_legacy_database_and_preserves_data()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var databasePath = Path.Combine(testRoot, "Kanban41.db");

            // 模拟旧版：无 WAL 连接串建库并写入数据
            var legacyBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate
            };
            await using (var legacy = new SqliteConnection(legacyBuilder.ToString()))
            {
                await legacy.OpenAsync();
                await using var createCommand = legacy.CreateCommand();
                createCommand.CommandText = "CREATE TABLE LegacyTest (Id INTEGER PRIMARY KEY, Value TEXT)";
                await createCommand.ExecuteNonQueryAsync();

                await using var insertCommand = legacy.CreateCommand();
                insertCommand.CommandText = "INSERT INTO LegacyTest (Value) VALUES ('legacy-data')";
                await insertCommand.ExecuteNonQueryAsync();
            }

            // 新版 DatabaseService 打开旧库：应自动切为 WAL 且数据保留
            var database = new DatabaseService(databasePath);
            await database.InitializeAsync();
            Assert.Equal("wal", await GetJournalModeAsync(database));

            await using var connection = database.CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM LegacyTest WHERE Value = 'legacy-data'";
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static async Task<string> GetJournalModeAsync(DatabaseService database)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";
        return (await command.ExecuteScalarAsync())?.ToString() ?? string.Empty;
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "KanbanForOne.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
