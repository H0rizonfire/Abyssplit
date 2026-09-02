using System.Diagnostics;
using System.Windows;
using AbyssusTimer.App.Interop;

namespace AbyssusTimer.App.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow(string versionText)
    {
        InitializeComponent();
        VersionText.Text = versionText;
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.GitHubRepoUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.LogException("Failed to open GitHub repo from About panel", ex);
        }
    }

    private void Terms_Click(object sender, RoutedEventArgs e) =>
        new TermsOfUseWindow(isFirstRun: false) { Owner = this }.ShowDialog();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
