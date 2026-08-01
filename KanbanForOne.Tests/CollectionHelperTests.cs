using System.Collections.ObjectModel;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Tests;

/// <summary>
/// CollectionHelper.Replace 的语义测试：顺序保持、既有项复用、移除/插入/移动与收敛。
/// </summary>
public sealed class CollectionHelperTests
{
    [Fact]
    public void Replace_keeps_identical_sequence_untouched()
    {
        var target = new ObservableCollection<string> { "A", "B", "C" };

        CollectionHelper.Replace(target, new[] { "A", "B", "C" });

        Assert.Equal(["A", "B", "C"], target);
    }

    [Fact]
    public void Replace_reuses_existing_item_instances()
    {
        var shared = new object();
        var target = new ObservableCollection<object> { shared, new object() };

        CollectionHelper.Replace(target, new[] { shared, new object() });

        Assert.Same(shared, target[0]);
        Assert.Equal(2, target.Count);
    }

    [Fact]
    public void Replace_removes_missing_and_inserts_new_in_order()
    {
        var target = new ObservableCollection<string> { "A", "B", "C" };

        CollectionHelper.Replace(target, new[] { "B", "C", "D" });

        Assert.Equal(["B", "C", "D"], target);
    }

    [Fact]
    public void Replace_moves_items_to_match_source_order()
    {
        var target = new ObservableCollection<string> { "A", "B", "C" };

        CollectionHelper.Replace(target, new[] { "C", "A", "B" });

        Assert.Equal(["C", "A", "B"], target);
    }

    [Fact]
    public void Replace_with_empty_source_clears_collection()
    {
        var target = new ObservableCollection<string> { "A", "B" };

        CollectionHelper.Replace(target, Array.Empty<string>());

        Assert.Empty(target);
    }

    [Fact]
    public void Replace_with_duplicates_in_source_reproduces_duplicates()
    {
        var target = new ObservableCollection<string> { "A", "B" };

        CollectionHelper.Replace(target, new[] { "A", "A" });

        Assert.Equal(["A", "A"], target);
    }

    [Fact]
    public void Replace_shrinks_trailing_extra_items()
    {
        var target = new ObservableCollection<string> { "A", "B", "C", "D" };

        CollectionHelper.Replace(target, new[] { "A", "B" });

        Assert.Equal(["A", "B"], target);
    }
}
