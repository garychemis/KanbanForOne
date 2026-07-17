using System.IO;
using System.Text.Json;

namespace KanbanForOne.Services;

public sealed record WorkHourOptions(
    IReadOnlyList<string> Disciplines,
    IReadOnlyList<string> WorkActivities);

public sealed class WorkHourOptionsService
{
    private static readonly string[] DefaultDisciplines = ["工艺", "管道", "外管", "管材", "管机"];
    private static readonly string[] DefaultWorkActivities = ["设计", "校核", "审核", "审定", "设计管理", "会议"];
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _configPath;

    public WorkHourOptionsService(string? configPath = null)
    {
        _configPath = configPath ?? AppPaths.WorkHourOptionsPath;
    }

    public string ConfigPath => _configPath;

    public async Task<WorkHourOptions> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await LoadCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<WorkHourOptions> AddDisciplineAsync(string value) => UpdateAsync(value, true, true);

    public Task<WorkHourOptions> AddWorkActivityAsync(string value) => UpdateAsync(value, false, true);

    public Task<WorkHourOptions> RemoveDisciplineAsync(string value) => UpdateAsync(value, true, false);

    public Task<WorkHourOptions> RemoveWorkActivityAsync(string value) => UpdateAsync(value, false, false);

    private async Task<WorkHourOptions> UpdateAsync(string value, bool discipline, bool add)
    {
        var normalized = NormalizeOption(value);
        if (normalized.Length == 0)
        {
            return await LoadAsync();
        }

        await _gate.WaitAsync();
        try
        {
            var current = await LoadCoreAsync();
            var disciplines = current.Disciplines.ToList();
            var workActivities = current.WorkActivities.ToList();
            var target = discipline ? disciplines : workActivities;

            if (add)
            {
                if (!target.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    target.Add(normalized);
                }
            }
            else
            {
                target.RemoveAll(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
            }

            var updated = new WorkHourOptions(NormalizeOptions(disciplines), NormalizeOptions(workActivities));
            await SaveCoreAsync(updated);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<WorkHourOptions> LoadCoreAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);

        if (!File.Exists(_configPath))
        {
            var defaults = new WorkHourOptions(DefaultDisciplines, DefaultWorkActivities);
            await SaveCoreAsync(defaults);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_configPath);
            var stored = await JsonSerializer.DeserializeAsync<WorkHourOptionsFile>(stream, SerializerOptions)
                         ?? new WorkHourOptionsFile();
            return new WorkHourOptions(
                NormalizeOptions(stored.Disciplines ?? []),
                NormalizeOptions(stored.WorkActivities ?? []));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"人工时选项配置文件格式无效：{_configPath}", ex);
        }
    }

    private async Task SaveCoreAsync(WorkHourOptions options)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        var tempPath = $"{_configPath}.{Guid.NewGuid():N}.tmp";
        var data = new WorkHourOptionsFile
        {
            Disciplines = options.Disciplines.ToList(),
            WorkActivities = options.WorkActivities.ToList()
        };

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, data, SerializerOptions);
            }

            File.Move(tempPath, _configPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string NormalizeOption(string? value) => value?.Trim() ?? string.Empty;

    private static IReadOnlyList<string> NormalizeOptions(IEnumerable<string> values)
    {
        var result = new List<string>();
        foreach (var value in values)
        {
            var normalized = NormalizeOption(value);
            if (normalized.Length > 0 && !result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private sealed class WorkHourOptionsFile
    {
        public List<string>? Disciplines { get; set; }

        public List<string>? WorkActivities { get; set; }
    }
}
