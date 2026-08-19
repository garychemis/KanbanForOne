using System.Collections.ObjectModel;
using System.Collections.Specialized;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Modules.DesignConditions.Models;

public sealed class DesignConditionEntry : ObservableObject
{
    private string _projectNumber = string.Empty;
    private string _issuingDiscipline = string.Empty;
    private string _receivingDiscipline = string.Empty;
    private string _receiver = string.Empty;
    private DateTime _issuedDate = DateTime.Today;
    private string _conditionName = string.Empty;
    private string _revision = string.Empty;
    private string _drawingSize = string.Empty;
    private int _drawingCount;

    public DesignConditionEntry()
    {
        Attachments.CollectionChanged += OnAttachmentsChanged;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    public string ProjectNumber { get => _projectNumber; set => SetProperty(ref _projectNumber, value); }

    public string IssuingDiscipline { get => _issuingDiscipline; set => SetProperty(ref _issuingDiscipline, value); }

    public string ReceivingDiscipline { get => _receivingDiscipline; set => SetProperty(ref _receivingDiscipline, value); }

    public string Receiver { get => _receiver; set => SetProperty(ref _receiver, value); }

    public DateTime IssuedDate { get => _issuedDate; set => SetProperty(ref _issuedDate, value.Date); }

    public string ConditionName { get => _conditionName; set => SetProperty(ref _conditionName, value); }

    public string Revision { get => _revision; set => SetProperty(ref _revision, value); }

    public string DrawingSize { get => _drawingSize; set => SetProperty(ref _drawingSize, value); }

    public int DrawingCount { get => _drawingCount; set => SetProperty(ref _drawingCount, value); }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ObservableCollection<DesignConditionAttachment> Attachments { get; } = new();

    public int AttachmentCount => Attachments.Count;

    public string DisciplineRouteDisplay => $"{IssuingDiscipline} → {ReceivingDiscipline}";

    public string RevisionDisplay => string.IsNullOrWhiteSpace(Revision) ? "未填写" : Revision;

    private void OnAttachmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AttachmentCount));
    }
}
