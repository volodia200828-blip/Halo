using System.Windows;
using System.Windows.Input;

namespace Halo.App.Views;

public partial class CyberInputDialog : Window
{
    public string ResultText { get; private set; } = string.Empty;

    public CyberInputDialog(string title, string defaultValue = "")
    {
        InitializeComponent();
        TitleText.Text = title;
        InputBox.Text = defaultValue;
        Loaded += (s, e) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public static string? Prompt(Window owner, string title, string defaultValue = "")
    {
        var dialog = new CyberInputDialog(title, defaultValue)
        {
            Owner = owner
        };
        var result = dialog.ShowDialog();
        return result == true ? dialog.ResultText : null;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnOk_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            BtnCancel_Click(sender, e);
        }
    }
}
