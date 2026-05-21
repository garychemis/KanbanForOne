using KanbanForOne.ViewModels;

namespace KanbanForOne.Models;

public sealed class ArchiveSection : ObservableObject
{
    public static readonly Guid DefaultId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private string _name = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _updatedAt = DateTime.Now;
    private bool _isDefault;
    private int _sortOrder;
    private int _taskCount;
    private int _noteCount;
    private bool _isSelected;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }

    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    public int TaskCount
    {
        get => _taskCount;
        set
        {
            if (SetProperty(ref _taskCount, value))
            {
                OnPropertyChanged(nameof(TotalCount));
            }
        }
    }

    public int NoteCount
    {
        get => _noteCount;
        set
        {
            if (SetProperty(ref _noteCount, value))
            {
                OnPropertyChanged(nameof(TotalCount));
            }
        }
    }

    public int TotalCount => TaskCount + NoteCount;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
