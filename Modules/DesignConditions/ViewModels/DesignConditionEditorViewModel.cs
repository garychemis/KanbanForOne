using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using KanbanForOne.Models;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Modules.DesignConditions.ViewModels;

public sealed class DesignConditionEditorViewModel : ObservableObject
{
    private string _projectNumber = string.Empty;
    private string _issuingDiscipline = string.Empty;
    private string _receivingDiscipline = string.Empty;
    private string _receiver = string.Empty;
    private DateTime? _issuedDate = DateTime.Today;
    private string _conditionName = string.Empty;
    private string _revision = string.Empty;
    private string _validationMessage = string.Empty;

    public DesignConditionEditorViewModel(
        DesignConditionEntry? source,
        DateTime defaultIssuedDate,
        IReadOnlyList<string> disciplines,
        IReadOnlyList<string> receivers,
        IReadOnlyList<string> drawingSizes)
    {
        Source = source;
        Disciplines = disciplines;
        Receivers = receivers;
        DrawingSizes = drawingSizes;
        AttachFilesCommand = new RelayCommand(AttachDroppedFiles);
        AddDrawingRowCommand = new RelayCommand(() => AddDrawingRow());
        RemoveDrawingRowCommand = new RelayCommand(RemoveDrawingRow);
        DrawingRows.CollectionChanged += OnDrawingRowsChanged;
        if (source is null)
        {
            _issuedDate = defaultIssuedDate.Date;
            Id = Guid.NewGuid();
            CreatedAt = DateTime.Now;
            DrawingRows.Add(new DesignConditionDrawingRow());
        }
        else
        {
            Id = source.Id;
            CreatedAt = source.CreatedAt;
            _projectNumber = source.ProjectNumber;
            _issuingDiscipline = source.IssuingDiscipline;
            _receivingDiscipline = source.ReceivingDiscipline;
            _receiver = source.Receiver;
            _issuedDate = source.IssuedDate;
            _conditionName = source.ConditionName;
            _revision = source.Revision;
            LoadDrawingRows(source);
            foreach (var attachment in source.Attachments)
            {
                Attachments.Add(attachment);
            }
        }
        RefreshDrawingStatistics();
    }

    public DesignConditionEntry? Source { get; }
    public Guid Id { get; }
    public DateTime CreatedAt { get; }
    public IReadOnlyList<string> Disciplines { get; }
    public IReadOnlyList<string> Receivers { get; }
    public IReadOnlyList<string> DrawingSizes { get; }
    public ObservableCollection<DesignConditionDrawingRow> DrawingRows { get; } = new();
    public ObservableCollection<DesignConditionAttachment> Attachments { get; } = new();
    public ObservableCollection<DesignConditionAttachment> DeletedAttachments { get; } = new();
    public ObservableCollection<string> PendingFilePaths { get; } = new();
    public RelayCommand AttachFilesCommand { get; }
    public RelayCommand AddDrawingRowCommand { get; }
    public RelayCommand RemoveDrawingRowCommand { get; }

    public string ProjectNumber { get => _projectNumber; set => SetProperty(ref _projectNumber, value); }
    public string IssuingDiscipline { get => _issuingDiscipline; set => SetProperty(ref _issuingDiscipline, value); }
    public string ReceivingDiscipline { get => _receivingDiscipline; set => SetProperty(ref _receivingDiscipline, value); }
    public string Receiver { get => _receiver; set => SetProperty(ref _receiver, value); }
    public DateTime? IssuedDate { get => _issuedDate; set => SetProperty(ref _issuedDate, value?.Date); }
    public string ConditionName { get => _conditionName; set => SetProperty(ref _conditionName, value); }
    public string Revision { get => _revision; set => SetProperty(ref _revision, value); }
    public int DrawingSpecificationCount => DrawingRows.Count(item => !string.IsNullOrWhiteSpace(item.DrawingSize));
    public int TotalDrawingCount
    {
        get
        {
            long total = 0;
            foreach (var row in DrawingRows)
            {
                if (!int.TryParse(row.DrawingCountText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0) continue;
                total += count;
                if (total > int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }
    }
    public string ValidationMessage { get => _validationMessage; private set { if (SetProperty(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage)); } }
    public bool HasValidationMessage => ValidationMessage.Length > 0;

    public DesignConditionDrawingRow AddDrawingRow()
    {
        var existingBlank = DrawingRows.LastOrDefault(item => string.IsNullOrWhiteSpace(item.DrawingSize));
        if (existingBlank is not null) return existingBlank;
        var row = new DesignConditionDrawingRow();
        DrawingRows.Add(row);
        return row;
    }

    public void AddPendingFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(File.Exists))
        {
            if (!PendingFilePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                PendingFilePaths.Add(path);
            }
        }
    }

    private void AttachDroppedFiles(object? parameter)
    {
        if (parameter is FileDropPayload payload && ReferenceEquals(payload.Owner, this))
        {
            AddPendingFiles(payload.FilePaths);
        }
    }

    public void RemovePendingFile(string path) => PendingFilePaths.Remove(path);

    public void ShowOperationError(string message)
    {
        ValidationMessage = message;
    }

    public void RemoveAttachment(DesignConditionAttachment attachment)
    {
        if (Attachments.Remove(attachment))
        {
            DeletedAttachments.Add(attachment);
        }
    }

    public bool TryBuild(out DesignConditionEntry entry)
    {
        entry = new DesignConditionEntry();
        var project = NormalizeProject(ProjectNumber);
        if (project.Length == 0) return Fail("请输入项目。", out entry);
        if (string.IsNullOrWhiteSpace(IssuingDiscipline)) return Fail("请输入提出专业。", out entry);
        if (string.IsNullOrWhiteSpace(ReceivingDiscipline)) return Fail("请输入接收专业。", out entry);
        if (string.IsNullOrWhiteSpace(Receiver)) return Fail("请输入接收人。", out entry);
        if (IssuedDate is null) return Fail("请选择提出日期。", out entry);
        if (string.IsNullOrWhiteSpace(ConditionName)) return Fail("请输入条件名称。", out entry);
        if (!TryBuildDrawings(out var drawingStorage, out var drawingError)) return Fail(drawingError, out entry);

        ValidationMessage = string.Empty;
        entry = new DesignConditionEntry
        {
            Id = Id,
            ProjectNumber = project,
            IssuingDiscipline = IssuingDiscipline.Trim(),
            ReceivingDiscipline = ReceivingDiscipline.Trim(),
            Receiver = Receiver.Trim(),
            IssuedDate = IssuedDate.Value,
            ConditionName = ConditionName.Trim(),
            Revision = Revision.Trim(),
            DrawingSize = drawingStorage.DrawingSizes,
            DrawingCounts = drawingStorage.DrawingCounts,
            DrawingCount = drawingStorage.TotalDrawingCount,
            CreatedAt = CreatedAt,
            UpdatedAt = DateTime.Now
        };
        foreach (var attachment in Attachments)
        {
            entry.Attachments.Add(attachment);
        }
        return true;
    }

    private bool TryBuildDrawings(out DesignConditionDrawingStorage storage, out string errorMessage)
    {
        storage = new DesignConditionDrawingStorage(string.Empty, string.Empty, 0);
        var rows = DrawingRows.Where(item =>
            !string.IsNullOrWhiteSpace(item.DrawingSize) || !string.IsNullOrWhiteSpace(item.DrawingCountText)).ToArray();
        if (rows.Length == 0)
        {
            errorMessage = "请至少添加一种图幅。";
            return false;
        }

        var specifications = new List<DesignConditionDrawingSpec>(rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var size = rows[index].DrawingSize.Trim();
            if (size.Length == 0)
            {
                errorMessage = $"第 {index + 1} 行：条件图幅不能为空。";
                return false;
            }
            if (size.Contains(DesignConditionDrawingCodec.Separator))
            {
                errorMessage = $"第 {index + 1} 行：自定义图幅不能包含半角竖线 |。";
                return false;
            }
            if (!int.TryParse(rows[index].DrawingCountText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0)
            {
                errorMessage = $"第 {index + 1} 行：条件数量必须是大于 0 的整数。";
                return false;
            }
            specifications.Add(new DesignConditionDrawingSpec(size, count));
        }

        if (!DesignConditionDrawingCodec.TrySerialize(specifications, out storage, out errorMessage))
        {
            return false;
        }
        return true;
    }

    private void RemoveDrawingRow(object? parameter)
    {
        if (parameter is not DesignConditionDrawingRow row || !DrawingRows.Remove(row)) return;
        if (DrawingRows.Count == 0) DrawingRows.Add(new DesignConditionDrawingRow());
    }

    private void LoadDrawingRows(DesignConditionEntry source)
    {
        if (source.DrawingSpecifications.Count > 0)
        {
            foreach (var item in source.DrawingSpecifications)
            {
                DrawingRows.Add(new DesignConditionDrawingRow(
                    item.DrawingSize,
                    item.DrawingCount.ToString(CultureInfo.InvariantCulture)));
            }
            return;
        }

        var sizes = source.DrawingSize.Split(DesignConditionDrawingCodec.Separator, StringSplitOptions.None);
        var counts = source.EffectiveDrawingCounts.Split(DesignConditionDrawingCodec.Separator, StringSplitOptions.None);
        var rowCount = Math.Max(sizes.Length, counts.Length);
        for (var index = 0; index < rowCount; index++)
        {
            DrawingRows.Add(new DesignConditionDrawingRow(
                index < sizes.Length ? sizes[index] : string.Empty,
                index < counts.Length ? counts[index] : string.Empty));
        }
        if (DrawingRows.Count == 0) DrawingRows.Add(new DesignConditionDrawingRow());
    }

    private void OnDrawingRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DesignConditionDrawingRow row in e.OldItems) row.PropertyChanged -= OnDrawingRowPropertyChanged;
        }
        if (e.NewItems is not null)
        {
            foreach (DesignConditionDrawingRow row in e.NewItems) row.PropertyChanged += OnDrawingRowPropertyChanged;
        }
        RefreshDrawingStatistics();
    }

    private void OnDrawingRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshDrawingStatistics();
    }

    private void RefreshDrawingStatistics()
    {
        OnPropertyChanged(nameof(DrawingSpecificationCount));
        OnPropertyChanged(nameof(TotalDrawingCount));
    }

    private bool Fail(string message, out DesignConditionEntry entry)
    {
        ValidationMessage = message;
        entry = new DesignConditionEntry();
        return false;
    }

    public static string NormalizeProject(string value) => string.Concat(value.Where(ch => !char.IsWhiteSpace(ch))).ToUpperInvariant();
}
