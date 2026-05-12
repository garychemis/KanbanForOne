namespace KanbanForOne.Models;

public static class DragDropFormats
{
    public const string TaskCard = "Kanban41.TaskCard";
    public const string NoteCard = "Kanban41.NoteCard";
}

public sealed record CardDropPayload(object Item, KanbanColumnKind TargetColumn, int TargetIndex);

public sealed record FileDropPayload(object Owner, IReadOnlyList<string> FilePaths);
