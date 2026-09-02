using System.Windows;

namespace AbyssusTimer.App.Windows;

public partial class ConfirmDeletePersonalBestWindow : Window
{
    public ConfirmDeletePersonalBestWindow(string currentPbText, string? nextFastestText)
    {
        InitializeComponent();
        CurrentPbText.Text = $"Current PB: {currentPbText}";
        RevertText.Text = nextFastestText is not null
            ? $"Your PB will revert to the next-fastest completed run in your history: {nextFastestText}."
            : "You have no other completed runs recorded — your PB will be cleared entirely until you complete a new one.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
