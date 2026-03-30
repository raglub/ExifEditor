using Avalonia.Controls;
using Avalonia.Input;

namespace ExifEditor.Views;

public partial class AddTagWindow : Window
{
    public string? TagLabel { get; private set; }

    public AddTagWindow() : this(null) {}

    public AddTagWindow(string? initialLabel)
    {
        InitializeComponent();

        var okButton = this.FindControl<Button>("OkButton")!;
        var cancelButton = this.FindControl<Button>("CancelButton")!;
        var textBox = this.FindControl<TextBox>("LabelTextBox")!;

        if (!string.IsNullOrEmpty(initialLabel))
        {
            Title = "Edit Tag";
            textBox.Text = initialLabel;
        }
        else
        {
            Title = "Add Tag";
        }

        okButton.Click += (s, e) =>
        {
            TagLabel = textBox.Text;
            Close();
        };

        cancelButton.Click += (s, e) =>
        {
            TagLabel = null;
            Close();
        };

        textBox.AttachedToVisualTree += (s, e) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TagLabel = this.FindControl<TextBox>("LabelTextBox")?.Text;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            TagLabel = null;
            Close();
        }
        base.OnKeyDown(e);
    }
}
