using KanbanForOne.ViewModels;

namespace KanbanForOne.Modules.DesignConditions.ViewModels;

public sealed class DesignConditionDrawingRow : ObservableObject
{
    private string _drawingSize = string.Empty;
    private string _drawingCountText = "1";

    public DesignConditionDrawingRow()
    {
    }

    public DesignConditionDrawingRow(string drawingSize, string drawingCountText)
    {
        _drawingSize = drawingSize;
        _drawingCountText = drawingCountText;
    }

    public string DrawingSize { get => _drawingSize; set => SetProperty(ref _drawingSize, value ?? string.Empty); }

    public string DrawingCountText { get => _drawingCountText; set => SetProperty(ref _drawingCountText, value ?? string.Empty); }
}
