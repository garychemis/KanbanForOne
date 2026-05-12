using System.Windows;
using System.Windows.Controls;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class NoteDrawerControl : UserControl
{
    public static readonly DependencyProperty SelectedNoteProperty = DependencyProperty.Register(
        nameof(SelectedNote),
        typeof(NoteItem),
        typeof(NoteDrawerControl),
        new PropertyMetadata(null));

    public NoteDrawerControl()
    {
        InitializeComponent();
    }

    public NoteItem? SelectedNote
    {
        get => (NoteItem?)GetValue(SelectedNoteProperty);
        set => SetValue(SelectedNoteProperty, value);
    }

    private void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        NoteTagEditor.FocusInput();
    }
}
