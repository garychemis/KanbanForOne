using KanbanForOne.ViewModels;

namespace KanbanForOne.Modules.DesignConditions.Models;

public sealed class DesignConditionAttachment : ObservableObject
{
    private int _sortOrder;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DesignConditionId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int SortOrder { get => _sortOrder; set => SetProperty(ref _sortOrder, value); }
}
