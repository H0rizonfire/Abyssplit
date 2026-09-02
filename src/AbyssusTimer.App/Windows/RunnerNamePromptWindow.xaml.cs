using System.Windows;
using System.Windows.Input;

namespace AbyssusTimer.App.Windows;

public partial class RunnerNamePromptWindow : Window
{
    public string RunnerName { get; private set; } = "";

    public RunnerNamePromptWindow(string initialName)
    {
        InitializeComponent();
        NameTextBox.Text = initialName;
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        RunnerName = NameTextBox.Text.Trim();
        DialogResult = true;
    }

    private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Export_Click(sender, e);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
