using System.Collections.ObjectModel;

namespace KanbanForOne.ViewModels;

/// <summary>
/// ObservableCollection 的增量替换辅助，尽量复用既有项以避免破坏 UI 状态。
/// </summary>
public static class CollectionHelper
{
    public static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        var desiredItems = source.ToList();
        var comparer = EqualityComparer<T>.Default;

        // 首轮用 HashSet 建立期望集合的索引，把移除判断从 O(n·m) 降为 O(n+m)。
        var desiredSet = new HashSet<T>(desiredItems, comparer);

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredSet.Contains(target[index]))
            {
                target.RemoveAt(index);
            }
        }

        for (var desiredIndex = 0; desiredIndex < desiredItems.Count; desiredIndex++)
        {
            var desiredItem = desiredItems[desiredIndex];

            if (desiredIndex < target.Count && comparer.Equals(target[desiredIndex], desiredItem))
            {
                continue;
            }

            var existingIndex = IndexOf(target, desiredItem, desiredIndex + 1, comparer);

            if (existingIndex >= 0)
            {
                target.Move(existingIndex, desiredIndex);
                continue;
            }

            target.Insert(desiredIndex, desiredItem);
        }

        while (target.Count > desiredItems.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int IndexOf<T>(
        ObservableCollection<T> collection,
        T item,
        int startIndex,
        IEqualityComparer<T> comparer)
    {
        for (var index = startIndex; index < collection.Count; index++)
        {
            if (comparer.Equals(collection[index], item))
            {
                return index;
            }
        }

        return -1;
    }
}
