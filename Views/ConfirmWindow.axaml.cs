using Avalonia.Controls;
using Avalonia.Input;

namespace ExifEditor.Views;

public partial class ConfirmWindow : Window
{
    public bool Confirmed { get; private set; }

    public ConfirmWindow() : this("Are you sure?") {}

    public ConfirmWindow(string message)
    {
        InitializeComponent();

        var yesButton = this.FindControl<Button>("YesButton")!;
        var noButton = this.FindControl<Button>("NoButton")!;
        var messageText = this.FindControl<TextBlock>("MessageText")!;

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
