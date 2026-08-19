using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace KanbanForOne.Controls;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
    }

    private void OnExternalLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri.Scheme is not ("https" or "http" or "mailto"))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
