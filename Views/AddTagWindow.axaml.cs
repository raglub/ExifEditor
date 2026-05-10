using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace ExifEditor.Views;

public partial class AddTagWindow : Window
{
    public string? TagLabel { get; private set; }

    public AddTagWindow() : this(null, null) {}

    public AddTagWindow(string? initialLabel) : this(initialLabel, null) {}

    public AddTagWindow(string? initialLabel, IEnumerable<string>? suggestions)
    {
        InitializeComponent();

        var okButton = this.FindControl<Button>("OkButton")!;
        var cancelButton = this.FindControl<Button>("CancelButton")!;
        var labelBox = this.FindControl<AutoCompleteBox>("LabelBox")!;

        if (suggestions != null)
        {
            labelBox.ItemsSource = suggestions;
        }

        if (!string.IsNullOrEmpty(initialLabel))
        {
            Title = "Edit Tag";
            labelBox.Text = initialLabel;
        }
        else
        {
            Title = "Add Tag";
        }

        okButton.Click += (s, e) =>
        {
            TagLabel = labelBox.Text;
            Close();
        };

        cancelButton.Click += (s, e) =>
        {
            TagLabel = null;
            Close();
        };

        labelBox.AttachedToVisualTree += (s, e) =>
        {
            labelBox.Focus();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TagLabel = this.FindControl<AutoCompleteBox>("LabelBox")?.Text;
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
