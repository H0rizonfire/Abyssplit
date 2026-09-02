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
    }

    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Decline_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
