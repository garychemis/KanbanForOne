using KanbanForOne.ViewModels;

namespace KanbanForOne.Models;

public sealed class AttachmentItem : ObservableObject
{
    private string _originalFileName = string.Empty;
    private string _storedFileName = string.Empty;
    private string _relativePath = string.Empty;
    private string _fileExtension = string.Empty;
    private long _fileSizeBytes;
    private int _sortOrder;

    public Guid Id { get; set; } = Guid.NewGuid();

    public AttachmentOwnerType OwnerType { get; set; }

    public Guid OwnerId { get; set; }

    public string OriginalFileName
    {
        get => _originalFileName;
        set => SetProperty(ref _originalFileName, value);
    }

    public string StoredFileName
    {
        get => _storedFileName;
        set => SetProperty(ref _storedFileName, value);
    }

    public string RelativePath
    {
        get => _relativePath;
        set => SetProperty(ref _relativePath, value);
    }

    public string FileExtension
    {
        get => _fileExtension;
        set => SetProperty(ref _fileExtension, value);
    }

    public long FileSizeBytes
    {
        get => _fileSizeBytes;
        set => SetProperty(ref _fileSizeBytes, value);
    }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }
}
