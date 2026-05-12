using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class AttachmentDropZoneControl : UserControl
{
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(
        nameof(Owner),
        typeof(object),
        typeof(AttachmentDropZoneControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty AttachFilesCommandProperty = DependencyProperty.Register(
        nameof(AttachFilesCommand),
        typeof(ICommand),
        typeof(AttachmentDropZoneControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty TitleTextProperty = DependencyProperty.Register(
        nameof(TitleText),
        typeof(string),
        typeof(AttachmentDropZoneControl),
        new PropertyMetadata("拖入文件保存为本地附件"));

    public static readonly DependencyProperty HelperTextProperty = DependencyProperty.Register(
        nameof(HelperText),
        typeof(string),
        typeof(AttachmentDropZoneControl),
        new PropertyMetadata("最多 10 个文件，文件会复制到 EXE 所在目录的 attachments/"));

    public static readonly DependencyProperty DropHintTextProperty = DependencyProperty.Register(
        nameof(DropHintText),
        typeof(string),
        typeof(AttachmentDropZoneControl),
        new PropertyMetadata("释放文件以保存为附件"));

    public AttachmentDropZoneControl()
    {
        InitializeComponent();
    }

    public object? Owner
    {
        get => GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }

    public ICommand? AttachFilesCommand
    {
        get => (ICommand?)GetValue(AttachFilesCommandProperty);
        set => SetValue(AttachFilesCommandProperty, value);
    }

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public string HelperText
    {
        get => (string)GetValue(HelperTextProperty);
        set => SetValue(HelperTextProperty, value);
    }

    public string DropHintText
    {
        get => (string)GetValue(DropHintTextProperty);
        set => SetValue(DropHintTextProperty, value);
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        ResetDropState();
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (Owner is not null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = ((string[])e.Data.GetData(DataFormats.FileDrop)).Where(File.Exists).ToArray();
            var payload = new FileDropPayload(Owner, files);

            if (AttachFilesCommand?.CanExecute(payload) == true)
            {
                AttachFilesCommand.Execute(payload);
            }
        }

        ResetDropState();
        e.Handled = true;
    }

    private void UpdateDragState(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        DropBorder.Background = (Brush)new BrushConverter().ConvertFromString("#E6FFFFFF")!;
        DropHint.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void ResetDropState()
    {
        DropBorder.Background = (Brush)new BrushConverter().ConvertFromString("#80FFFFFF")!;
        DropHint.Visibility = Visibility.Collapsed;
    }
}
