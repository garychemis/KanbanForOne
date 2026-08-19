using System.Collections.ObjectModel;
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
    private string _drawingSize = string.Empty;
    private string _drawingCountText = "0";
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
        if (source is null)
        {
            _issuedDate = defaultIssuedDate.Date;
            Id = Guid.NewGuid();
            CreatedAt = DateTime.Now;
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
            _drawingSize = source.DrawingSize;
            _drawingCountText = source.DrawingCount.ToString();
            foreach (var attachment in source.Attachments)
            {
                Attachments.Add(attachment);
            }
        }
    }

    public DesignConditionEntry? Source { get; }
    public Guid Id { get; }
    public DateTime CreatedAt { get; }
    public IReadOnlyList<string> Disciplines { get; }
    public IReadOnlyList<string> Receivers { get; }
    public IReadOnlyList<string> DrawingSizes { get; }
    public ObservableCollection<DesignConditionAttachment> Attachments { get; } = new();
    public ObservableCollection<DesignConditionAttachment> DeletedAttachments { get; } = new();
    public ObservableCollection<string> PendingFilePaths { get; } = new();
    public RelayCommand AttachFilesCommand { get; }

    public string ProjectNumber { get => _projectNumber; set => SetProperty(ref _projectNumber, value); }
    public string IssuingDiscipline { get => _issuingDiscipline; set => SetProperty(ref _issuingDiscipline, value); }
    public string ReceivingDiscipline { get => _receivingDiscipline; set => SetProperty(ref _receivingDiscipline, value); }
    public string Receiver { get => _receiver; set => SetProperty(ref _receiver, value); }
    public DateTime? IssuedDate { get => _issuedDate; set => SetProperty(ref _issuedDate, value?.Date); }
    public string ConditionName { get => _conditionName; set => SetProperty(ref _conditionName, value); }
    public string Revision { get => _revision; set => SetProperty(ref _revision, value); }
    public string DrawingSize { get => _drawingSize; set => SetProperty(ref _drawingSize, value); }
    public string DrawingCountText { get => _drawingCountText; set => SetProperty(ref _drawingCountText, value); }
    public string ValidationMessage { get => _validationMessage; private set { if (SetProperty(ref _validationMessage, value)) OnPropertyChanged(nameof(HasValidationMessage)); } }
    public bool HasValidationMessage => ValidationMessage.Length > 0;

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
        if (!int.TryParse(DrawingCountText.Trim(), out var drawingCount) || drawingCount < 0)
        {
            return Fail("图纸数量必须是非负整数。", out entry);
        }

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
            DrawingSize = DrawingSize.Trim(),
            DrawingCount = drawingCount,
            CreatedAt = CreatedAt,
            UpdatedAt = DateTime.Now
        };
        foreach (var attachment in Attachments)
        {
            entry.Attachments.Add(attachment);
        }
        return true;
    }

    private bool Fail(string message, out DesignConditionEntry entry)
    {
        ValidationMessage = message;
        entry = new DesignConditionEntry();
        return false;
    }

    public static string NormalizeProject(string value) => string.Concat(value.Where(ch => !char.IsWhiteSpace(ch))).ToUpperInvariant();
}
