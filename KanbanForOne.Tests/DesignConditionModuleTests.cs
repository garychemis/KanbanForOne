using System.IO;
using System.IO.Compression;
using ClosedXML.Excel;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.ViewModels;
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
            Assert.Equal("3", await ScalarAsync(connection, "PRAGMA user_version"));
            Assert.Equal("1", await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='DesignConditions'"));
            Assert.Equal("1", await ScalarAsync(connection, "SELECT COUNT(*) FROM pragma_table_info('DesignConditions') WHERE name='DrawingCounts' AND upper(type)='TEXT'"));
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
            Assert.Equal("3", await ScalarAsync(migrated, "PRAGMA user_version"));
            Assert.Equal("0", await ScalarAsync(migrated, "SELECT COUNT(*) FROM pragma_table_info('DesignConditions') WHERE name='RecordDate'"));
            Assert.Equal("0", await ScalarAsync(migrated, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='IX_DesignConditions_RecordDate'"));
            Assert.Equal("2026-08-19T00:00:00.0000000", await ScalarAsync(migrated, "SELECT IssuedDate FROM DesignConditions WHERE Id='legacy'"));
            Assert.Equal("3", await ScalarAsync(migrated, "SELECT DrawingCounts FROM DesignConditions WHERE Id='legacy'"));
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
    public async Task Version_two_database_adds_delimited_counts_without_changing_totals()
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
                        Id TEXT PRIMARY KEY,
                        ProjectNumber TEXT NOT NULL,
                        IssuingDiscipline TEXT NOT NULL,
                        ReceivingDiscipline TEXT NOT NULL,
                        Receiver TEXT NOT NULL,
                        IssuedDate TEXT NOT NULL,
                        ConditionName TEXT NOT NULL,
                        Revision TEXT NOT NULL DEFAULT '',
                        DrawingSize TEXT NOT NULL DEFAULT '',
                        DrawingCount INTEGER NOT NULL DEFAULT 0 CHECK (DrawingCount >= 0),
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    INSERT INTO DesignConditions VALUES (
                        'v2', 'P200', '工艺', '设备', '李四', '2026-08-20', 'V2条件', 'A', 'A4', 5,
                        '2026-08-20T08:00:00.0000000', '2026-08-20T08:00:00.0000000'
                    );
                    PRAGMA user_version = 2;
                    """;
                await command.ExecuteNonQueryAsync(Xunit.TestContext.Current.CancellationToken);
            }

            var database = new DesignConditionDatabaseService(options);
            await database.InitializeAsync();
            await using var migrated = database.CreateConnection();
            await migrated.OpenAsync(Xunit.TestContext.Current.CancellationToken);
            Assert.Equal("3", await ScalarAsync(migrated, "PRAGMA user_version"));
            Assert.Equal("5", await ScalarAsync(migrated, "SELECT DrawingCounts FROM DesignConditions WHERE Id='v2'"));
            Assert.Equal("5", await ScalarAsync(migrated, "SELECT DrawingCount FROM DesignConditions WHERE Id='v2'"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Version_two_database_with_zero_drawings_migrates_to_valid_counts()
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
                        Id TEXT PRIMARY KEY,
                        ProjectNumber TEXT NOT NULL,
                        IssuingDiscipline TEXT NOT NULL,
                        ReceivingDiscipline TEXT NOT NULL,
                        Receiver TEXT NOT NULL,
                        IssuedDate TEXT NOT NULL,
                        ConditionName TEXT NOT NULL,
                        Revision TEXT NOT NULL DEFAULT '',
                        DrawingSize TEXT NOT NULL DEFAULT '',
                        DrawingCount INTEGER NOT NULL DEFAULT 0 CHECK (DrawingCount >= 0),
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    INSERT INTO DesignConditions VALUES
                        ('11111111-1111-1111-1111-111111111111', 'P200', '工艺', '设备', '李四', '2026-08-20', '零张条件', 'A', 'A0', 0,
                         '2026-08-20T08:00:00.0000000', '2026-08-20T08:00:00.0000000'),
                        ('22222222-2222-2222-2222-222222222222', 'P200', '工艺', '设备', '李四', '2026-08-20', '正常条件', 'A', 'A4', 3,
                         '2026-08-20T08:00:00.0000000', '2026-08-20T08:00:00.0000000'),
                        ('33333333-3333-3333-3333-333333333333', 'P200', '工艺', '设备', '李四', '2026-08-20', '无规格条件', 'A', '', 0,
                         '2026-08-20T08:00:00.0000000', '2026-08-20T08:00:00.0000000');
                    PRAGMA user_version = 2;
                    """;
                await command.ExecuteNonQueryAsync(Xunit.TestContext.Current.CancellationToken);
            }

            var database = new DesignConditionDatabaseService(options);
            await database.InitializeAsync();
            await using var migrated = database.CreateConnection();
            await migrated.OpenAsync(Xunit.TestContext.Current.CancellationToken);
            Assert.Equal("3", await ScalarAsync(migrated, "PRAGMA user_version"));
            // 有图幅的 0 数量记录迁移为默认 1 张，避免 codec 拒绝 "0"
            Assert.Equal("1", await ScalarAsync(migrated, "SELECT DrawingCounts FROM DesignConditions WHERE Id='11111111-1111-1111-1111-111111111111'"));
            Assert.Equal("1", await ScalarAsync(migrated, "SELECT DrawingCount FROM DesignConditions WHERE Id='11111111-1111-1111-1111-111111111111'"));
            // 正常记录数量不变
            Assert.Equal("3", await ScalarAsync(migrated, "SELECT DrawingCounts FROM DesignConditions WHERE Id='22222222-2222-2222-2222-222222222222'"));
            Assert.Equal("3", await ScalarAsync(migrated, "SELECT DrawingCount FROM DesignConditions WHERE Id='22222222-2222-2222-2222-222222222222'"));
            // 无图幅记录保持无规格状态
            Assert.Equal("", await ScalarAsync(migrated, "SELECT DrawingCounts FROM DesignConditions WHERE Id='33333333-3333-3333-3333-333333333333'"));
            Assert.Equal("0", await ScalarAsync(migrated, "SELECT DrawingCount FROM DesignConditions WHERE Id='33333333-3333-3333-3333-333333333333'"));

            var attachments = new DesignConditionAttachmentRepository(database);
            var repository = new DesignConditionRepository(database, attachments);
            var loaded = (await repository.GetAllAsync()).ToDictionary(item => item.Id);
            Assert.Equal([("A0", 1)],
                loaded[Guid.Parse("11111111-1111-1111-1111-111111111111")].DrawingSpecifications.Select(s => (s.DrawingSize, s.DrawingCount)));
            Assert.Empty(loaded[Guid.Parse("33333333-3333-3333-3333-333333333333")].DrawingSpecifications);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Drawing_codec_rejects_zero_duplicate_and_reserved_separator()
    {
        Assert.False(DesignConditionDrawingCodec.TryParse("A1", "0", out _, out var zeroError));
        Assert.Contains("大于 0", zeroError);
        Assert.True(DesignConditionDrawingCodec.TryParse("A1|A4", "1|5", out var items, out _));
        Assert.Equal(6, items.Sum(item => item.DrawingCount));
        Assert.False(DesignConditionDrawingCodec.TryParse("A1|a1", "1|2", out _, out var duplicateError));
        Assert.Contains("重复", duplicateError);
        Assert.False(DesignConditionDrawingCodec.TrySerialize(
            [new DesignConditionDrawingSpec("A|1", 1)], out _, out var separatorError));
        Assert.Contains("|", separatorError);
    }

    [Fact]
    public void Drawing_size_catalog_has_required_order_and_factors()
    {
        Assert.Equal(
            ["A4", "A3", "A2", "A1", "A1+0.25", "A1+0.5", "A0", "A0+0.25", "A0+0.5"],
            DesignConditionDrawingSizeCatalog.Names);

        decimal[] expected = [0.125m, 0.25m, 0.5m, 1m, 1.25m, 1.5m, 2m, 2.5m, 3m];
        Assert.Equal(expected, DesignConditionDrawingSizeCatalog.Definitions.Select(item => item.FoldedA1Factor));
    }

    [Fact]
    public void Drawing_size_catalog_calculates_folded_a1_and_rejects_unknown_size()
    {
        Assert.True(DesignConditionDrawingSizeCatalog.TryCalculate(
            [new("A4", 5), new("A1+0.25", 2), new("A0", 1)],
            out var total,
            out var error));
        Assert.Equal(5.125m, total);
        Assert.Equal(string.Empty, error);

        Assert.False(DesignConditionDrawingSizeCatalog.TryCalculate(
            [new("自定义", 1)],
            out _,
            out error));
        Assert.Contains("自定义", error);
    }

    [Fact]
    public async Task Repository_round_trips_multiple_sizes_and_calendar_uses_total_count()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var entry = CreateEntry(new DateTime(2026, 8, 20));
            entry.DrawingSize = "A1|A4";
            entry.DrawingCounts = "1|5";
            entry.DrawingCount = 6;
            await context.Repository.UpsertAsync(entry);

            var loaded = Assert.Single(await context.Repository.GetAllAsync());
            Assert.Equal(2, loaded.DrawingSpecifications.Count);
            Assert.Equal("A1 × 1 · A4 × 5", loaded.DrawingSummary);
            Assert.Equal(6, loaded.DrawingCount);
            var calendar = Assert.Single(await context.Repository.GetCalendarSummariesAsync(new DateTime(2026, 8, 20), new DateTime(2026, 8, 20)));
            Assert.Equal(1, calendar.RecordCount);
            Assert.Equal(6, calendar.DrawingCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Repository_does_not_persist_fixed_drawing_sizes_as_dynamic_options()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var entry = CreateEntry(DateTime.Today);
            entry.DrawingSize = "A1+0.25";
            entry.DrawingCounts = "1";
            entry.DrawingCount = 1;

            await context.Repository.SaveAggregateAsync(entry, [], []);

            await using var connection = context.Database.CreateConnection();
            await connection.OpenAsync(Xunit.TestContext.Current.CancellationToken);
            Assert.Equal("0", await ScalarAsync(connection,
                "SELECT COUNT(*) FROM DesignConditionOptions WHERE OptionType='DrawingSize' AND Value='A1+0.25'"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Editor_serializes_rows_and_blocks_duplicate_sizes()
    {
        var editor = new DesignConditionEditorViewModel(null, DateTime.Today, ["工艺", "设备"], ["张三"])
        {
            ProjectNumber = "P100",
            IssuingDiscipline = "工艺",
            ReceivingDiscipline = "设备",
            Receiver = "张三",
            ConditionName = "多图幅条件"
        };
        Assert.Equal("1", editor.DrawingRows[0].DrawingCountText);
        Assert.Same(editor.DrawingRows[0], editor.AddDrawingRow());
        editor.DrawingRows[0].DrawingSize = "A1";
        var second = editor.AddDrawingRow();
        second.DrawingSize = "A4";
        second.DrawingCountText = "5";

        Assert.True(editor.TryBuild(out var entry));
        Assert.Equal("A1|A4", entry.DrawingSize);
        Assert.Equal("1|5", entry.DrawingCounts);
        Assert.Equal(6, entry.DrawingCount);

        editor.DrawingRows[0].DrawingCountText = "0";
        Assert.False(editor.TryBuild(out _));
        Assert.Contains("大于 0", editor.ValidationMessage);

        editor.DrawingRows[0].DrawingCountText = "1";
        second.DrawingSize = "a1";
        Assert.False(editor.TryBuild(out _));
        Assert.Contains("重复", editor.ValidationMessage);
    }

    [Fact]
    public void Editor_loads_legacy_single_value_rows_when_specifications_missing()
    {
        var legacy = new DesignConditionEntry
        {
            ProjectNumber = "P100",
            IssuingDiscipline = "工艺",
            ReceivingDiscipline = "设备",
            Receiver = "张三",
            IssuedDate = new DateTime(2026, 8, 20),
            ConditionName = "旧数据条件",
            Revision = "A",
            DrawingSize = "A0|A1",
            DrawingCount = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        var editor = new DesignConditionEditorViewModel(legacy, DateTime.Today, ["工艺", "设备"], ["张三"]);
        // 无有效分图幅数据时按旧单值字段逐项兜底加载
        Assert.Equal(2, editor.DrawingRows.Count);
        Assert.Equal("A0", editor.DrawingRows[0].DrawingSize);
        Assert.Equal("0", editor.DrawingRows[0].DrawingCountText);
        Assert.Equal("A1", editor.DrawingRows[1].DrawingSize);
        Assert.Equal(string.Empty, editor.DrawingRows[1].DrawingCountText);
    }

    [Fact]
    public void Editor_loads_parsed_specifications_when_available()
    {
        var modern = new DesignConditionEntry
        {
            ProjectNumber = "P100",
            IssuingDiscipline = "工艺",
            ReceivingDiscipline = "设备",
            Receiver = "张三",
            IssuedDate = new DateTime(2026, 8, 20),
            ConditionName = "多图幅条件",
            Revision = "A",
            DrawingSize = "A0|A1",
            DrawingCounts = "1|5",
            DrawingCount = 6,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        var editor = new DesignConditionEditorViewModel(modern, DateTime.Today, ["工艺", "设备"], ["张三"]);
        Assert.Equal(2, editor.DrawingRows.Count);
        Assert.Equal("A0", editor.DrawingRows[0].DrawingSize);
        Assert.Equal("1", editor.DrawingRows[0].DrawingCountText);
        Assert.Equal("A1", editor.DrawingRows[1].DrawingSize);
        Assert.Equal("5", editor.DrawingRows[1].DrawingCountText);
    }

    [Fact]
    public void Editor_clears_unknown_legacy_size_without_mutating_source()
    {
        var source = new DesignConditionEntry
        {
            ProjectNumber = "P100",
            IssuingDiscipline = "工艺",
            ReceivingDiscipline = "设备",
            Receiver = "张三",
            IssuedDate = new DateTime(2026, 8, 20),
            ConditionName = "旧图幅",
            DrawingSize = "A1|自定义",
            DrawingCounts = "1|4",
            DrawingCount = 5
        };

        var editor = new DesignConditionEditorViewModel(source, DateTime.Today, ["工艺", "设备"], ["张三"]);

        Assert.Equal(DesignConditionDrawingSizeCatalog.Names, editor.DrawingSizes);
        Assert.Equal("A1", editor.DrawingRows[0].DrawingSize);
        Assert.Equal(string.Empty, editor.DrawingRows[1].DrawingSize);
        Assert.Equal("4", editor.DrawingRows[1].DrawingCountText);
        Assert.Contains("自定义", editor.ValidationMessage);
        Assert.Equal("A1|自定义", source.DrawingSize);
    }

    [Fact]
    public void Editor_rejects_programmatically_assigned_unknown_size()
    {
        var editor = CreateValidEditor();
        editor.DrawingRows[0].DrawingSize = "自定义";

        Assert.False(editor.TryBuild(out _));
        Assert.Contains("固定图幅", editor.ValidationMessage);
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
            entry.DrawingSize = "A1|A4";
            entry.DrawingCounts = "1|5";
            entry.DrawingCount = 6;
            entry.Attachments.Add(new DesignConditionAttachment { DesignConditionId = entry.Id, OriginalFileName = "a.dwg" });
            entry.Attachments.Add(new DesignConditionAttachment { DesignConditionId = entry.Id, OriginalFileName = "b.pdf" });
            var second = CreateEntry(DateTime.Today.AddDays(1));
            second.Id = Guid.NewGuid();
            second.ConditionName = "加长图幅条件";
            second.DrawingSize = "A1+0.25";
            second.DrawingCounts = "1";
            second.DrawingCount = 1;
            var output = Path.Combine(root, "conditions.xlsx");
            await new DesignConditionExportService().ExportAsync(output, [entry, second]);

            using var workbook = new XLWorkbook(output);
            var summary = workbook.Worksheet("设计条件汇总");
            var detail = workbook.Worksheet("设计条件明细");
            Assert.Contains("项目", summary.Column(1).CellsUsed().Select(cell => cell.GetString()));
            Assert.Equal("折A1", summary.Cell(1, 8).GetString());
            Assert.Equal(7, summary.Cell(2, 7).GetValue<int>());
            Assert.Equal(2.875m, summary.Cell(2, 8).GetValue<decimal>());
            var totalRow = summary.Column(1).CellsUsed().Single(cell => cell.GetString() == "总计").Address.RowNumber;
            Assert.Equal(7, summary.Cell(totalRow, 7).GetValue<int>());
            Assert.Equal(2.875m, summary.Cell(totalRow, 8).GetValue<decimal>());
            Assert.Equal("0.###", summary.Column(8).Style.NumberFormat.Format);
            Assert.Equal("A1", detail.Cell(2, 8).GetString());
            Assert.Equal(1, detail.Cell(2, 9).GetValue<int>());
            Assert.Equal("A4", detail.Cell(3, 8).GetString());
            Assert.Equal(5, detail.Cell(3, 9).GetValue<int>());
            Assert.Equal(6, detail.Cell(2, 10).GetValue<int>());
            Assert.Contains("a.dwg", detail.Cell(2, 12).GetString());
            Assert.Contains("b.pdf", detail.Cell(2, 12).GetString());
            Assert.Equal(2, detail.Cell(2, 11).GetValue<int>());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Excel_export_rejects_unknown_size_without_creating_file()
    {
        var root = CreateRoot();
        try
        {
            var entry = CreateEntry(DateTime.Today);
            entry.DrawingSize = "旧图幅";
            entry.DrawingCounts = "2";
            entry.DrawingCount = 2;
            var output = Path.Combine(root, "invalid.xlsx");

            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                new DesignConditionExportService().ExportAsync(output, [entry]));

            Assert.Contains(entry.ConditionName, error.Message);
            Assert.Contains("旧图幅", error.Message);
            Assert.False(File.Exists(output));
        }
        finally
        {
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
    public async Task Restore_rejects_v3_backup_missing_drawing_counts_column()
    {
        var root = CreateRoot();
        try
        {
            var context = await CreateContextAsync(root);
            var entry = CreateEntry(DateTime.Today);
            await context.Repository.UpsertAsync(entry);
            await context.Database.CheckpointAsync();
            context.Database.ClearPool();

            var fakeDatabase = Path.Combine(root, "fake-v3.db");
            await using (var connection = new SqliteConnection($"Data Source={fakeDatabase}"))
            {
                await connection.OpenAsync(Xunit.TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE DesignConditions (
                        Id TEXT PRIMARY KEY,
                        ProjectNumber TEXT NOT NULL,
                        IssuingDiscipline TEXT NOT NULL,
                        ReceivingDiscipline TEXT NOT NULL,
                        Receiver TEXT NOT NULL,
                        IssuedDate TEXT NOT NULL,
                        ConditionName TEXT NOT NULL,
                        Revision TEXT NOT NULL DEFAULT '',
                        DrawingSize TEXT NOT NULL DEFAULT '',
                        DrawingCount INTEGER NOT NULL DEFAULT 0 CHECK (DrawingCount >= 0),
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                    CREATE TABLE DesignConditionAttachments (
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
                    );
                    CREATE TABLE DesignConditionOptions (
                        Id TEXT PRIMARY KEY,
                        OptionType TEXT NOT NULL,
                        Value TEXT NOT NULL,
                        SortOrder INTEGER NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL
                    );
                    PRAGMA user_version = 3;
                    """;
                await command.ExecuteNonQueryAsync(Xunit.TestContext.Current.CancellationToken);
            }
            SqliteConnection.ClearAllPools();
            var invalid = Path.Combine(root, "missing-column.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(invalid, System.IO.Compression.ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(fakeDatabase, "DesignConditions.db");
                var manifest = archive.CreateEntry("manifest.json");
                await using var writer = new StreamWriter(manifest.Open());
                await writer.WriteAsync("{\"module\":\"DesignConditions\",\"formatVersion\":1,\"databaseSchemaVersion\":3}");
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

    private static DesignConditionEditorViewModel CreateValidEditor()
    {
        var editor = new DesignConditionEditorViewModel(null, DateTime.Today, ["工艺", "设备"], ["张三"])
        {
            ProjectNumber = "P100",
            IssuingDiscipline = "工艺",
            ReceivingDiscipline = "设备",
            Receiver = "张三",
            ConditionName = "固定图幅条件"
        };
        editor.DrawingRows[0].DrawingSize = "A1";
        editor.DrawingRows[0].DrawingCountText = "1";
        return editor;
    }

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
