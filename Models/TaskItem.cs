using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Models;

public sealed class TaskItem : ObservableObject
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private TaskStatus _status;
    private TaskPriority _priority = TaskPriority.Medium;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _updatedAt = DateTime.Now;
    private DateTime? _completedAt;
    private bool _isArchived;
    private int _sortOrder;
    private bool _isExpanded;

    public TaskItem()
    {
        Attachments.CollectionChanged += OnAttachmentsChanged;
        Tags.CollectionChanged += OnTagsChanged;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value)
            {
                return;
            }

            _description = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public TaskStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            _completedAt = value == TaskStatus.Done ? DateTime.Now : null;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(CompletedAt));
        }
    }

    public TaskPriority Priority
    {
        get => _priority;
        set
        {
            if (_priority == value)
            {
                return;
            }

            _priority = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> Tags { get; } = new();

    public DateTime? StartDate
    {
        get => _startDate;
        set
        {
            if (_startDate == value)
            {
                return;
            }

            _startDate = value;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DateRangeDisplay));
        }
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set
        {
            if (_endDate == value)
            {
                return;
            }

            _endDate = value;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DateRangeDisplay));
        }
    }

    public string DateRangeDisplay
    {
        get
        {
            var startDate = StartDate;
            var endDate = EndDate;

            if (startDate is null && endDate is null)
            {
                return "无日期";
            }

            if (startDate is null && endDate is not null)
            {
                return $"~{FormatDate(endDate.Value)}";
            }

            if (startDate is not null && endDate is null)
            {
                return $"{FormatDate(startDate.Value)}~";
            }

            return $"{FormatDate(startDate!.Value)}~{FormatDate(endDate!.Value)}";
        }
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

    public DateTime? CompletedAt
    {
        get => _completedAt;
        set => SetProperty(ref _completedAt, value);
    }

    public bool IsArchived
    {
        get => _isArchived;
        set
        {
            if (_isArchived == value)
            {
                return;
            }

            _isArchived = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public ObservableCollection<AttachmentItem> Attachments { get; } = new();

    public int AttachmentCount => Attachments.Count;

    public string PrimaryTag => Tags.FirstOrDefault() ?? "TASK";

    public string TagsDisplay
    {
        get => string.Join(", ", Tags);
        set
        {
            Tags.CollectionChanged -= OnTagsChanged;
            Tags.Clear();

            foreach (var tag in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                Tags.Add(tag);
            }

            Tags.CollectionChanged += OnTagsChanged;
            Touch();
            OnPropertyChanged();
            OnPropertyChanged(nameof(PrimaryTag));
        }
    }

    private void Touch()
    {
        _updatedAt = DateTime.Now;
        OnPropertyChanged(nameof(UpdatedAt));
    }

    private void OnAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AttachmentCount));
    }

    private void OnTagsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Touch();
        OnPropertyChanged(nameof(TagsDisplay));
        OnPropertyChanged(nameof(PrimaryTag));
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToString("yyyy.MM/dd");
    }
}
