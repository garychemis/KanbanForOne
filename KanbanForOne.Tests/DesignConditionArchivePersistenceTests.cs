using System.IO;
using System.IO.Compression;
using System.Text.Json;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Tests;

public sealed class DesignConditionArchivePersistenceTests
{
    private static readonly Guid LegacyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task V3_migration_preserves_legacy_record_and_attachment_and_is_idempotent()
    {
        using var fixture = await Fixture.CreateAsync(initialize: false);
        await WriteLegacyDatabaseAsync(fixture.Options.DatabasePath);
        await fixture.Database.InitializeAsync();
        await fixture.Database.InitializeAsync();

        var entry = Assert.Single(await fixture.Repository.GetAllAsync());
        Assert.Equal(LegacyId, entry.Id);
        Assert.Equal(DesignConditionArchiveKind.Issued, entry.ArchiveKind);
        Assert.Null(entry.ReceivedDate);
        Assert.Equal(new DateTime(2026, 8, 19), entry.ArchiveDate);
        Assert.Equal("历史条件", entry.ConditionName);
        Assert.Equal("2|3", entry.DrawingCounts);
        Assert.Equal(5, entry.DrawingCount);
        Assert.Equal(new DateTime(2026, 8, 19, 8, 0, 0), entry.CreatedAt);
        Assert.Equal("attachments/legacy.dwg", Assert.Single(entry.Attachments).RelativePath);
        Assert.Equal("2026-08-19T00:00:00.0000000", await fixture.ScalarAsync("SELECT IssuedDate FROM DesignConditions"));
        Assert.Equal("4", await fixture.ScalarAsync("PRAGMA user_version"));
        Assert.Equal("1", await fixture.ScalarAsync("SELECT COUNT(*) FROM DesignConditionOptions WHERE Value = '历史专业'"));

        // 历史 ISO 时间戳可按日期查询；迁移不需要改写原日期。
        Assert.Equal(LegacyId, Assert.Single(await fixture.Repository.GetByIssuedDateAsync(new DateTime(2026, 8, 19))).Id);
        Assert.Equal(new DateTime(2026, 8, 19), Assert.Single(await fixture.Repository.GetCalendarSummariesAsync(new DateTime(2026, 8, 1), new DateTime(2026, 8, 31))).IssuedDate);
    }

    [Fact]
    public async Task Received_records_round_trip_and_use_received_date_in_lists_and_calendar()
    {
        using var fixture = await Fixture.CreateAsync();
        var issued = Entry(DesignConditionArchiveKind.Issued);
        var received = Entry(DesignConditionArchiveKind.Received);
        received.ReceivedDate = new DateTime(2026, 10, 5);
        await fixture.Repository.UpsertAsync(issued);
        await fixture.Repository.UpsertAsync(received);

        var entries = await fixture.Repository.GetAllAsync();
        Assert.Equal(received.Id, entries[0].Id);
        Assert.Equal(DesignConditionArchiveKind.Received, entries[0].ArchiveKind);
        Assert.Equal(new DateTime(2026, 10, 1), entries[0].IssuedDate);
        Assert.Equal(new DateTime(2026, 10, 5), entries[0].ReceivedDate);
        Assert.Equal(issued.Id, Assert.Single(await fixture.Repository.GetByIssuedDateAsync(new DateTime(2026, 10, 1))).Id);
        Assert.Equal(received.Id, Assert.Single(await fixture.Repository.GetByIssuedDateAsync(new DateTime(2026, 10, 5))).Id);
        var summaries = await fixture.Repository.GetCalendarSummariesAsync(new DateTime(2026, 10, 1), new DateTime(2026, 10, 5));
        Assert.Equal(2, summaries.Count);
        Assert.All(summaries, item => Assert.Equal(1, item.RecordCount));
        Assert.All(summaries, item => Assert.Equal(2, item.DrawingCount));

        received.ConditionName = "接受条件更新";
        received.ReceivedDate = new DateTime(2026, 10, 6);
        await fixture.Repository.UpsertAsync(received);
        Assert.Empty(await fixture.Repository.GetByIssuedDateAsync(new DateTime(2026, 10, 5)));
        Assert.Equal("接受条件更新", Assert.Single(await fixture.Repository.GetByIssuedDateAsync(new DateTime(2026, 10, 6))).ConditionName);
    }

    [Fact]
    public async Task Repository_rejects_missing_received_date_and_invalid_archive_kind_without_saving()
    {
        using var fixture = await Fixture.CreateAsync();
        var received = Entry(DesignConditionArchiveKind.Received);
        received.ReceivedDate = null;
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.UpsertAsync(received));
        var invalid = Entry((DesignConditionArchiveKind)9);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Repository.UpsertAsync(invalid));
        Assert.Empty(await fixture.Repository.GetAllAsync());
    }

    [Fact]
    public async Task Legacy_issued_date_with_timezone_keeps_its_original_calendar_day()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Issued));
        await fixture.ExecuteAsync("UPDATE DesignConditions SET IssuedDate = '2026-10-01T00:00:00.0000000+14:00'");

        var loaded = Assert.Single(await fixture.Repository.GetByIssuedDateAsync(new DateTime(2026, 10, 1)));
        Assert.Equal(new DateTime(2026, 10, 1), loaded.IssuedDate);
        Assert.Equal(loaded.IssuedDate, loaded.ArchiveDate);
    }

    [Theory]
    [InlineData("ArchiveKind = 9")]
    [InlineData("ArchiveKind = 1, ReceivedDate = NULL")]
    [InlineData("ArchiveKind = 1, ReceivedDate = ''")]
    [InlineData("ArchiveKind = 1, ReceivedDate = '2026-02-30'")]
    [InlineData("ArchiveKind = 1, ReceivedDate = '2026-13-01'")]
    [InlineData("ArchiveKind = 1, ReceivedDate = '0000-01-01'")]
    [InlineData("ArchiveKind = 1, ReceivedDate = 'invalid'")]
    public async Task Database_constraints_reject_invalid_archive_data(string assignment)
    {
        using var fixture = await Fixture.CreateAsync();
        var entry = Entry(DesignConditionArchiveKind.Issued);
        await fixture.Repository.UpsertAsync(entry);
        await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($"UPDATE DesignConditions SET {assignment}"));
        Assert.Equal(DesignConditionArchiveKind.Issued, Assert.Single(await fixture.Repository.GetAllAsync()).ArchiveKind);
    }

    [Fact]
    public async Task Backup_restore_preserves_both_types_dates_and_attachment_contents()
    {
        using var fixture = await Fixture.CreateAsync();
        var issued = Entry(DesignConditionArchiveKind.Issued);
        var received = Entry(DesignConditionArchiveKind.Received);
        await fixture.Repository.UpsertAsync(issued);
        var source = Path.Combine(fixture.Root, "received.dwg");
        await File.WriteAllTextAsync(source, "接收附件内容", TestContext.Current.CancellationToken);
        var files = await fixture.Storage.CopyFilesAsync(received.Id, [source]);
        await fixture.Repository.SaveAggregateAsync(received, [], files);
        var backup = await fixture.Backup.CreateBackupAsync();
        await fixture.Repository.DeleteAsync(received.Id);
        Assert.Empty(await fixture.Attachments.GetByConditionIdsAsync([received.Id]));
        File.Delete(fixture.Storage.GetAbsolutePath(files[0]));
        await fixture.Repository.DeleteAsync(issued.Id);

        var result = await fixture.Backup.RestoreAsync(backup.BackupPath);
        Assert.True(File.Exists(result.ProtectiveBackupPath));
        var restored = await fixture.Repository.GetAllAsync();
        Assert.Equal(2, restored.Count);
        Assert.Contains(restored, item => item.Id == issued.Id && item.ArchiveKind == DesignConditionArchiveKind.Issued);
        var receivedRestored = Assert.Single(restored, item => item.ArchiveKind == DesignConditionArchiveKind.Received);
        Assert.Equal(received.ReceivedDate, receivedRestored.ReceivedDate);
        Assert.Equal(received.IssuedDate, receivedRestored.IssuedDate);
        var attachment = Assert.Single(receivedRestored.Attachments);
        Assert.Equal(files[0].RelativePath, attachment.RelativePath);
        Assert.Equal("接收附件内容", await File.ReadAllTextAsync(fixture.Storage.GetAbsolutePath(attachment), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Legacy_backup_restores_as_issued_and_upgrades_to_v4()
    {
        using var fixture = await Fixture.CreateAsync();
        var legacyDatabase = Path.Combine(fixture.Root, "legacy.db");
        await WriteLegacyDatabaseAsync(legacyDatabase);
        var archivePath = CreateArchive(fixture.Root, legacyDatabase, schemaVersion: 3);
        await fixture.Backup.RestoreAsync(archivePath);
        var restored = Assert.Single(await fixture.Repository.GetAllAsync());
        Assert.Equal(DesignConditionArchiveKind.Issued, restored.ArchiveKind);
        Assert.Null(restored.ReceivedDate);
        Assert.Equal(LegacyId, restored.Id);
        Assert.Equal("4", await fixture.ScalarAsync("PRAGMA user_version"));
    }

    [Fact]
    public async Task Backup_before_migration_keeps_actual_schema_version_and_restores_into_current_schema()
    {
        using var legacy = await Fixture.CreateAsync(initialize: false);
        await WriteLegacyDatabaseAsync(legacy.Options.DatabasePath);

        var backup = await legacy.Backup.CreateBackupAsync(protective: true);

        Assert.Equal("3", await legacy.ScalarAsync("PRAGMA user_version"));
        Assert.Equal("0", await legacy.ScalarAsync("SELECT COUNT(*) FROM pragma_table_info('DesignConditions') WHERE name='ArchiveKind'"));
        using (var archive = ZipFile.OpenRead(backup.BackupPath))
        using (var manifestStream = archive.GetEntry("manifest.json")!.Open())
        using (var manifest = JsonDocument.Parse(manifestStream))
        {
            Assert.Equal(3, manifest.RootElement.GetProperty("DatabaseSchemaVersion").GetInt32());
        }

        using var current = await Fixture.CreateAsync();
        await current.Backup.RestoreAsync(backup.BackupPath);
        var restored = Assert.Single(await current.Repository.GetAllAsync());
        Assert.Equal(LegacyId, restored.Id);
        Assert.Equal(DesignConditionArchiveKind.Issued, restored.ArchiveKind);
        Assert.Equal("2|3", restored.DrawingCounts);
        Assert.Equal("4", await current.ScalarAsync("PRAGMA user_version"));
        Assert.Equal("3", await legacy.ScalarAsync("PRAGMA user_version"));
        Assert.Empty(Directory.EnumerateDirectories(legacy.Options.BackupRoot, ".snapshot-*"));
    }

    [Fact]
    public async Task Backup_during_migration_has_a_consistent_snapshot_and_can_be_restored()
    {
        using var source = await Fixture.CreateAsync(initialize: false);
        await WriteLegacyDatabaseAsync(source.Options.DatabasePath);

        var backupTask = source.Backup.CreateBackupAsync(protective: true);
        var migrationTask = source.Database.InitializeAsync();
        await Task.WhenAll(backupTask, migrationTask);
        var backup = await backupTask;

        var snapshotPath = Path.Combine(source.Root, "archived-snapshot.db");
        int manifestVersion;
        using (var archive = ZipFile.OpenRead(backup.BackupPath))
        {
            archive.GetEntry("DesignConditions.db")!.ExtractToFile(snapshotPath);
            using var manifestStream = archive.GetEntry("manifest.json")!.Open();
            using var manifest = JsonDocument.Parse(manifestStream);
            manifestVersion = manifest.RootElement.GetProperty("DatabaseSchemaVersion").GetInt32();
        }
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = snapshotPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using var version = connection.CreateCommand();
            version.CommandText = "PRAGMA user_version";
            Assert.Equal(manifestVersion, Convert.ToInt32(await version.ExecuteScalarAsync(TestContext.Current.CancellationToken)));
            Assert.Contains(manifestVersion, new[] { 3, 4 });
        }

        using var target = await Fixture.CreateAsync();
        await target.Backup.RestoreAsync(backup.BackupPath);
        Assert.Equal(LegacyId, Assert.Single(await target.Repository.GetAllAsync()).Id);
        Assert.Equal("4", await target.ScalarAsync("PRAGMA user_version"));
        Assert.Empty(Directory.EnumerateDirectories(source.Options.BackupRoot, ".snapshot-*"));
    }

    [Fact]
    public async Task Backup_of_uninitialized_database_fails_without_creating_an_empty_database_or_package()
    {
        using var fixture = await Fixture.CreateAsync(initialize: false);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Backup.CreateBackupAsync());

        Assert.Contains("尚未初始化", error.Message);
        Assert.False(File.Exists(fixture.Options.DatabasePath));
        Assert.Empty(Directory.EnumerateFiles(fixture.Options.BackupRoot));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("ALTER TABLE DesignConditions ADD COLUMN ArchiveKind INTEGER; ALTER TABLE DesignConditions ADD COLUMN ReceivedDate TEXT;", "UPDATE DesignConditions SET ArchiveKind = 9;")]
    [InlineData("ALTER TABLE DesignConditions ADD COLUMN ArchiveKind INTEGER; ALTER TABLE DesignConditions ADD COLUMN ReceivedDate TEXT;", "UPDATE DesignConditions SET ArchiveKind = 1;")]
    [InlineData("ALTER TABLE DesignConditions ADD COLUMN ArchiveKind INTEGER; ALTER TABLE DesignConditions ADD COLUMN ReceivedDate TEXT;", "UPDATE DesignConditions SET ArchiveKind = 1, ReceivedDate = '2026-02-30';")]
    public async Task Invalid_v4_backup_is_rejected_before_current_records_are_replaced(string columns, string data)
    {
        using var fixture = await Fixture.CreateAsync();
        var current = Entry(DesignConditionArchiveKind.Received);
        await fixture.Repository.UpsertAsync(current);
        var invalidDatabase = Path.Combine(fixture.Root, "invalid-v4.db");
        await WriteLegacyDatabaseAsync(invalidDatabase, columns + data + "PRAGMA user_version = 4;");
        var archivePath = CreateArchive(fixture.Root, invalidDatabase, schemaVersion: 4);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Backup.RestoreAsync(archivePath));
        Assert.Equal(current.Id, Assert.Single(await fixture.Repository.GetAllAsync()).Id);
    }

    private static DesignConditionEntry Entry(DesignConditionArchiveKind kind) => new()
    {
        ArchiveKind = kind,
        IssuedDate = new DateTime(2026, 10, 1),
        ReceivedDate = kind == DesignConditionArchiveKind.Received ? new DateTime(2026, 10, 3) : null,
        ProjectNumber = "P100",
        IssuingDiscipline = "工艺",
        ReceivingDiscipline = "设备",
        Receiver = "李四",
        ConditionName = "设计条件",
        DrawingSize = "A1",
        DrawingCounts = "2",
        DrawingCount = 2
    };

    private static async Task WriteLegacyDatabaseAsync(string databasePath, string extraSql = "")
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE DesignConditions (
                Id TEXT PRIMARY KEY, ProjectNumber TEXT NOT NULL,
                IssuingDiscipline TEXT NOT NULL, ReceivingDiscipline TEXT NOT NULL,
                Receiver TEXT NOT NULL, IssuedDate TEXT NOT NULL,
                ConditionName TEXT NOT NULL, Revision TEXT NOT NULL DEFAULT '',
                DrawingSize TEXT NOT NULL DEFAULT '', DrawingCounts TEXT NOT NULL DEFAULT '',
                DrawingCount INTEGER NOT NULL DEFAULT 0 CHECK (DrawingCount >= 0),
                CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
            CREATE TABLE DesignConditionAttachments (
                Id TEXT PRIMARY KEY, DesignConditionId TEXT NOT NULL,
                OriginalFileName TEXT NOT NULL, StoredFileName TEXT NOT NULL,
                RelativePath TEXT NOT NULL, FileExtension TEXT NOT NULL DEFAULT '',
                FileSizeBytes INTEGER NOT NULL, CreatedAt TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (DesignConditionId) REFERENCES DesignConditions(Id) ON DELETE CASCADE);
            CREATE TABLE DesignConditionOptions (
                Id TEXT PRIMARY KEY, OptionType TEXT NOT NULL, Value TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL);
            INSERT INTO DesignConditions VALUES (
                '11111111-1111-1111-1111-111111111111', 'P100', '工艺', '设备', '张三',
                '2026-08-19T00:00:00.0000000', '历史条件', 'A', 'A1|A2', '2|3', 5,
                '2026-08-19T08:00:00.0000000', '2026-08-20T09:00:00.0000000');
            INSERT INTO DesignConditionAttachments VALUES (
                '22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111',
                'legacy.dwg', 'legacy.dwg', 'attachments/legacy.dwg', '.dwg', 10,
                '2026-08-19T08:00:00.0000000', 0);
            INSERT INTO DesignConditionOptions VALUES (
                '33333333-3333-3333-3333-333333333333', 'Discipline', '历史专业', 1000,
                '2026-08-19T08:00:00.0000000');
            PRAGMA user_version = 3;
            """ + extraSql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static string CreateArchive(string root, string databasePath, int schemaVersion)
    {
        var archivePath = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(databasePath, "DesignConditions.db");
        using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open());
        writer.Write($$"""{"module":"DesignConditions","formatVersion":1,"databaseSchemaVersion":{{schemaVersion}}}""");
        return archivePath;
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "KanbanForOne.Archive.Tests", Guid.NewGuid().ToString("N"));
            Options = new DesignConditionStorageOptions(Root);
            Options.EnsureDirectories();
            Database = new DesignConditionDatabaseService(Options);
            Attachments = new DesignConditionAttachmentRepository(Database);
            Repository = new DesignConditionRepository(Database, Attachments);
            Storage = new DesignConditionAttachmentStorageService(Options);
            Backup = new DesignConditionBackupService(Database, Options, new DesignConditionOperationCoordinator());
        }

        public string Root { get; }
        public DesignConditionStorageOptions Options { get; }
        public DesignConditionDatabaseService Database { get; }
        public DesignConditionAttachmentRepository Attachments { get; }
        public DesignConditionRepository Repository { get; }
        public DesignConditionAttachmentStorageService Storage { get; }
        public DesignConditionBackupService Backup { get; }

        public static async Task<Fixture> CreateAsync(bool initialize = true)
        {
            var fixture = new Fixture();
            if (initialize) await fixture.Database.InitializeAsync();
            return fixture;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = Database.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        public async Task<string?> ScalarAsync(string sql)
        {
            await using var connection = Database.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(Root, recursive: true);
        }
    }
}
