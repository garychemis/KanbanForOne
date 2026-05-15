using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Models;

public sealed class NoteItem : ObservableObject
{
    private string _title = string.Empty;
    private string _content = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _updatedAt = DateTime.Now;
    private bool _isArchived;
    private int _sortOrder;
    private bool _isExpanded;

    public NoteItem()
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

    public string Content
    {
        get => _content;
        set
        {
            if (_content == value)
            {
                return;
            }

            _content = value;
            Touch();
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> Tags { get; } = new();

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
    }
}
