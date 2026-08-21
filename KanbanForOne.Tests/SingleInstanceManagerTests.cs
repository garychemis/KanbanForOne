using KanbanForOne.Services;

namespace KanbanForOne.Tests;

public sealed class SingleInstanceManagerTests
{
    [Fact]
    public void Only_one_manager_can_be_the_primary_instance()
    {
        var instanceName = $"KanbanForOne.Tests.{Guid.NewGuid():N}";

        using var first = new SingleInstanceManager(instanceName, () => { });
        using var second = new SingleInstanceManager(instanceName, () => { });

        Assert.True(first.IsPrimaryInstance);
        Assert.False(second.IsPrimaryInstance);
    }

    [Fact]
    public void Secondary_instance_notifies_the_primary_instance()
    {
        var instanceName = $"KanbanForOne.Tests.{Guid.NewGuid():N}";
        using var activationReceived = new ManualResetEventSlim();
        using var first = new SingleInstanceManager(instanceName, activationReceived.Set);
        using var second = new SingleInstanceManager(instanceName, () => { });

        second.NotifyPrimaryInstance();

        Assert.True(activationReceived.Wait(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Another_instance_can_start_after_the_primary_is_disposed()
    {
        var instanceName = $"KanbanForOne.Tests.{Guid.NewGuid():N}";
        var first = new SingleInstanceManager(instanceName, () => { });
        first.Dispose();

        using var replacement = new SingleInstanceManager(instanceName, () => { });

        Assert.True(replacement.IsPrimaryInstance);
    }
}
