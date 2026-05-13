namespace KanbanForOne.Models;

public static class DragDropFormats
{
    public const string TaskCard = "Kanban41.TaskCard";
    public const string NoteCard = "Kanban41.NoteCard";
}

public sealed record CardDropPayload(object Item, KanbanColumnKind TargetColumn, int TargetIndex);

public sealed record FileDropPayload(object Owner, IReadOnlyList<string> FilePaths);

public sealed record CalendarTaskDateDropPayload(TaskItem Task, DateTime Date);

public static class CardDragDropSession
{
    public static event EventHandler? Ended;

    public static void NotifyEnded()
    {
        Ended?.Invoke(null, EventArgs.Empty);
    }
}
