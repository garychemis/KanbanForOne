using System.Windows;

namespace KanbanForOne.Modules.DesignConditions.Views;

/// <summary>Applies archive colors locally while retaining the shared brushes updated by ThemeService.</summary>
public static class DesignConditionThemeScope
{
    private static readonly string[] ScopedBrushKeys =
    [
        "DesignConditionModuleTintBrush", "DesignConditionModuleHoverBrush",
        "DesignConditionModuleAccentBrush", "DesignConditionModuleAccentHoverBrush",
        "DesignConditionModuleAccentPressedBrush", "DesignConditionModuleBorderBrush",
        "DesignConditionModuleHoverBorderBrush", "DesignConditionModuleTextBrush",
        "ConditionDialogSurfaceBrush", "ConditionFieldBackgroundBrush", "ConditionFieldBorderBrush",
        "ConditionFieldHoverBrush", "ConditionFieldSelectedBrush", "ConditionPrimaryTextBrush",
        "ConditionSecondaryTextBrush", "ConditionDrawingHeaderBrush", "ConditionDrawingAlternateRowBrush",
        "ConditionInputHoverBorderBrush", "ConditionRowHoverBrush", "ConditionTreeSecondaryBrush",
        "ConditionButtonBrush", "ConditionButtonPressedBrush", "ConditionButtonForegroundBrush"
    ];

    public static readonly DependencyProperty IsReceivedProperty = DependencyProperty.RegisterAttached(
        "IsReceived", typeof(bool), typeof(DesignConditionThemeScope),
        new PropertyMetadata(false, OnIsReceivedChanged));

    public static bool GetIsReceived(DependencyObject element) => (bool)element.GetValue(IsReceivedProperty);
    public static void SetIsReceived(DependencyObject element, bool value) => element.SetValue(IsReceivedProperty, value);

    private static void OnIsReceivedChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element) return;
        foreach (var key in ScopedBrushKeys)
        {
            if ((bool)args.NewValue && Application.Current?.TryFindResource("Received" + key) is { } brush)
                element.Resources[key] = brush;
            else
                element.Resources.Remove(key);
        }
    }
}
