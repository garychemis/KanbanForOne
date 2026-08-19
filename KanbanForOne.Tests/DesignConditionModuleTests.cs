using System.IO;
using System.IO.Compression;
using ClosedXML.Excel;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Tests;

public sealed class DesignConditionModuleTests
{
    [Fact]
    public async Task Independent_database_initializes_with_wal_and_only_module_tables()
    {
        var root = CreateRoot();
        try
        {
            var options = new DesignConditionStorageOptions(root);
            var database = new DesignConditionDatabaseService(options);
            await database.InitializeAsync();

            await using var connection = database.CreateConnection();
            await connection.OpenAsync(Xunit.TestContext.Current.CancellationToken);
            Assert.Equal("wal", await ScalarAsync(connection, "PRAGMA journal_mode"));
            Assert.Equal("1", await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='DesignConditions'"));
            Assert.Equal("0", await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Tasks'"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Version_one_database_migrates_to_the_single_issued_date_without_losing_records()
    {
        var root = CreateRoot();
        try
        {
            var options = new DesignConditionStorageOptions(root);
            options.EnsureDirectories();
            await using (var connection = new SqliteConnection($"Data Source={options.DatabasePath}"))
            {
                await connection.OpenAsync(Xunit.TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE DesignConditions (
                        Id TEXT PRIMARY KEY NOT NULL,
                        ProjectNumber TEXT NOT NULL,
                        IssuingDiscipline TEXT NOT NULL,
                        ReceivingDiscipline TEXT NOT NULL,
                        Receiver TEXT NOT NULL,
                        RecordDate TEXT NOT NULL,
                        IssuedDate TEXT NOT NULL,
                        ConditionName TEXT NOT NULL,
                        Revision TEXT NOT NULL,
                        DrawingSize TEXT NOT NULL,
                        DrawingCount INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    INSERT INTO DesignConditions VALUES (
                        'legacy', 'P100', '工艺', '设备', '张三',
                        '2026-08-21T00:00:00.0000000', '2026-08-19T00:00:00.0000000',
                        '旧版条件', 'A', 'A1', 3,
                        '2026-08-19T08:00:00.0000000', '2026-08-19T08:00:00.0000000'
                    );
                    CREATE INDEX IX_DesignConditions_RecordDate ON DesignConditions(RecordDate);
                    PRAGMA user_version = 1;
                    """;
                await command.ExecuteNonQueryAsync(Xunit.TestContext.Current.CancellationToken);
            }

            var database = new DesignConditionDatabaseService(options);
            await database.InitializeAsync();
            await using var migrated = database.CreateConnection();
            await migrated.OpenAsync(Xunit.TestContext.Current.CancellationToken);
            Assert.Equal("2", await ScalarAsync(migrated, "PRAGMA user_version"));
            Assert.Equal("0", await ScalarAsync(migrated, "SELECT COUNT(*) FROM pragma_table_info('DesignConditions') WHERE name='RecordDate'"));
            Assert.Equal("0", await ScalarAsync(migrated, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_DesignConditions_RecordDate'"));
            Assert.Equal("2026-08-19T00:00:00.0000000", await ScalarAsync(migrated, "SELECT IssuedDate FROM DesignConditions WHERE Id='legacy'"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Repository_queries_by_the_single_issued_date()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var entry = CreateEntry(new DateTime(2026, 8, 19));
            await context.Repository.UpsertAsync(entry);

            Assert.Single(await context.Repository.GetByIssuedDateAsync(new DateTime(2026, 8, 19)));
            Assert.Empty(await context.Repository.GetByIssuedDateAsync(new DateTime(2026, 8, 21)));
            var loaded = Assert.Single(await context.Repository.GetAllAsync());
            Assert.Equal(new DateTime(2026, 8, 19), loaded.IssuedDate);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Attachments_are_stored_in_module_and_loaded_with_entry()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var entry = CreateEntry(DateTime.Today);
            await context.Repository.UpsertAsync(entry);
            var source = Path.Combine(root, "source.dwg");
            await File.WriteAllTextAsync(source, "drawing", Xunit.TestContext.Current.CancellationToken);
            var attachments = await context.Storage.CopyFilesAsync(entry.Id, [source]);
            await context.Attachments.AddRangeAsync(attachments);

            var loaded = Assert.Single(await context.Repository.GetAllAsync());
            var attachment = Assert.Single(loaded.Attachments);
            Assert.Equal("source.dwg", attachment.OriginalFileName);
            Assert.True(File.Exists(context.Storage.GetAbsolutePath(attachment)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Excel_contains_hierarchical_summary_and_nonduplicated_detail()
    {
        var root = CreateRoot();
        try
        {
            var entry = CreateEntry(DateTime.Today);
            entry.Attachments.Add(new DesignConditionAttachment { DesignConditionId = entry.Id, OriginalFileName = "a.dwg" });
            entry.Attachments.Add(new DesignConditionAttachment { DesignConditionId = entry.Id, OriginalFileName = "b.pdf" });
            var output = Path.Combine(root, "conditions.xlsx");
            await new DesignConditionExportService().ExportAsync(output, [entry]);

            using var workbook = new XLWorkbook(output);
            var summary = workbook.Worksheet("设计条件汇总");
            var detail = workbook.Worksheet("设计条件明细");
            Assert.Contains("项目", summary.Column(1).CellsUsed().Select(cell => cell.GetString()));
            Assert.Equal(entry.DrawingCount, detail.Cell(2, 9).GetValue<int>());
            Assert.Contains("a.dwg", detail.Cell(2, 11).GetString());
            Assert.Contains("b.pdf", detail.Cell(2, 11).GetString());
            Assert.Equal(2, detail.Cell(2, 10).GetValue<int>());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Backup_checkpoints_and_contains_database_and_attachments()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            await context.Repository.UpsertAsync(CreateEntry(DateTime.Today));
            var result = await new DesignConditionBackupService(context.Database, context.Options, new DesignConditionOperationCoordinator()).CreateBackupAsync();
            Assert.True(File.Exists(result.BackupPath));
            using var archive = System.IO.Compression.ZipFile.OpenRead(result.BackupPath);
            Assert.Contains(archive.Entries, entry => entry.FullName == "DesignConditions.db");
            Assert.Contains(archive.Entries, entry => entry.FullName == "manifest.json");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Aggregate_save_rolls_back_entry_and_attachments_when_one_insert_fails()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var original = CreateEntry(DateTime.Today);
            await context.Repository.UpsertAsync(original);
            var updated = CreateEntry(DateTime.Today);
            updated.Id = original.Id;
            updated.ConditionName = "不应提交";
            var duplicateId = Guid.NewGuid();
            DesignConditionAttachment Attachment(string name) => new()
            {
                Id = duplicateId,
                DesignConditionId = original.Id,
                OriginalFileName = name,
                StoredFileName = name,
                RelativePath = $"attachments/{name}",
                FileExtension = ".dwg",
                FileSizeBytes = 1
            };

            await Assert.ThrowsAnyAsync<Exception>(() => context.Repository.SaveAggregateAsync(updated, [], [Attachment("a.dwg"), Attachment("b.dwg")]));
            var loaded = Assert.Single(await context.Repository.GetAllAsync());
            Assert.Equal(original.ConditionName, loaded.ConditionName);
            Assert.Empty(loaded.Attachments);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Backup_restore_round_trip_keeps_data_and_creates_protective_backup()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var entry = CreateEntry(DateTime.Today);
            await context.Repository.UpsertAsync(entry);
            var backup = new DesignConditionBackupService(context.Database, context.Options, new DesignConditionOperationCoordinator());
            var created = await backup.CreateBackupAsync();
            await context.Repository.DeleteAsync(entry.Id);

            var restored = await backup.RestoreAsync(created.BackupPath);

            Assert.True(File.Exists(restored.ProtectiveBackupPath));
            Assert.Equal(entry.Id, Assert.Single(await context.Repository.GetAllAsync()).Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Restore_rejects_wrong_manifest_without_replacing_current_database()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var entry = CreateEntry(DateTime.Today);
            await context.Repository.UpsertAsync(entry);
            await context.Database.CheckpointAsync();
            context.Database.ClearPool();
            var invalid = Path.Combine(root, "invalid.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(invalid, System.IO.Compression.ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(context.Options.DatabasePath, "DesignConditions.db");
                var manifest = archive.CreateEntry("manifest.json");
                await using var writer = new StreamWriter(manifest.Open());
                await writer.WriteAsync("{\"module\":\"OtherModule\",\"version\":2}");
            }
            var backup = new DesignConditionBackupService(context.Database, context.Options, new DesignConditionOperationCoordinator());

            await Assert.ThrowsAsync<InvalidDataException>(() => backup.RestoreAsync(invalid));

            Assert.Equal(entry.Id, Assert.Single(await context.Repository.GetAllAsync()).Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Executable_attachment_cannot_be_launched_directly()
    {
        var root = CreateRoot();
        try
        {
            var options = new DesignConditionStorageOptions(root);
            options.EnsureDirectories();
            var path = Path.Combine(options.AttachmentRoot, "unsafe.cmd");
            File.WriteAllText(path, "echo unsafe");
            var attachment = new DesignConditionAttachment
            {
                OriginalFileName = "unsafe.cmd",
                RelativePath = Path.GetRelativePath(options.ModuleRoot, path),
                FileExtension = ".cmd"
            };

            Assert.Throws<InvalidOperationException>(() => new DesignConditionAttachmentStorageService(options).Open(attachment));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static DesignConditionEntry CreateEntry(DateTime issuedDate) => new()
    {
        ProjectNumber = "P100",
        IssuingDiscipline = "工艺",
        ReceivingDiscipline = "设备",
        Receiver = "张三",
        IssuedDate = issuedDate,
        ConditionName = "设备布置条件",
        Revision = "A",
        DrawingSize = "A1",
        DrawingCount = 3,
        CreatedAt = DateTime.Now,
        UpdatedAt = DateTime.Now
    };

    private static async Task<TestContext> CreateContextAsync(string root)
    {
        var options = new DesignConditionStorageOptions(root);
        var database = new DesignConditionDatabaseService(options);
        await database.InitializeAsync();
        var attachments = new DesignConditionAttachmentRepository(database);
        var repository = new DesignConditionRepository(database, attachments);
        return new TestContext(options, database, attachments, repository, new DesignConditionAttachmentStorageService(options));
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "KanbanForOne.DesignCondition.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    private sealed record TestContext(
        DesignConditionStorageOptions Options,
        DesignConditionDatabaseService Database,
        DesignConditionAttachmentRepository Attachments,
        DesignConditionRepository Repository,
        DesignConditionAttachmentStorageService Storage);
}
