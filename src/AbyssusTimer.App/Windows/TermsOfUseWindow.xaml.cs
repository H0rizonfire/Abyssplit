using System.Windows;
using AbyssusTimer.App.Interop;

namespace AbyssusTimer.App.Windows;

public partial class TermsOfUseWindow : Window
{
    public TermsOfUseWindow(bool isFirstRun)
    {
        InitializeComponent();
        TermsText.Text = TermsOfUse.Text;
        AcceptDeclineRow.Visibility = isFirstRun ? Visibility.Visible : Visibility.Collapsed;
        CloseRow.Visibility = isFirstRun ? Visibility.Collapsed : Visibility.Visible;
        Loaded += TermsOfUseWindow_Loaded;
    }

    private void TermsOfUseWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is not null)
            return;

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Top + (workArea.Height - ActualHeight) / 2;
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Decline_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
