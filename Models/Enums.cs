namespace KanbanForOne.Models;

public enum TaskStatus
{
    Todo,
    Doing,
    Blocked,
    Done
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public enum AttachmentOwnerType
{
    Task,
    Note
}

public enum KanbanColumnKind
{
    Todo,
    Doing,
    Blocked,
    Done,
    Notes
}
