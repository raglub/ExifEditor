using Avalonia.Controls;
using Avalonia.Input;
using ExifEditor.Services;

namespace ExifEditor.Views;

public partial class ConfirmWindow : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmWindow() : this("") {}

    public ConfirmWindow(string message)
    {
        InitializeComponent();

        var yesButton = this.FindControl<Button>("YesButton")!;
        var noButton = this.FindControl<Button>("NoButton")!;
        var messageText = this.FindControl<TextBlock>("MessageText")!;

        var loc = LocalizationService.Current;
        Title = loc.Language == AppLanguage.Polish ? "Potwierdź" : "Confirm";
        yesButton.Content = loc.Language == AppLanguage.Polish ? "Tak" : "Yes";
        noButton.Content = loc.Language == AppLanguage.Polish ? "Nie" : "No";

        messageText.Text = message;

        yesButton.Click += (s, e) =>
        {
            Confirmed = true;
            Close();
        };

        noButton.Click += (s, e) =>
        {
            Confirmed = false;
            Close();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Confirmed = true;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            Confirmed = false;
            Close();
        }
        base.OnKeyDown(e);
    }
}
