using System.Windows;

namespace AbyssusTimer.App.Windows;

public partial class ConfirmDeleteRunWindow : Window
{
    public ConfirmDeleteRunWindow(string runLabel, bool isPersonalBest)
    {
        InitializeComponent();
        RunLabelText.Text = runLabel;
        PbWarningText.Visibility = isPersonalBest ? Visibility.Visible : Visibility.Collapsed;
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
