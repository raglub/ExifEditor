using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace ExifEditor.Views;

public partial class SelectRecentWindow : Window
{
    public string? SelectedValue { get; private set; }

    public SelectRecentWindow(List<string> recentValues)
    {
        InitializeComponent();

        var listBox = this.FindControl<ListBox>("RecentListBox")!;
        var okButton = this.FindControl<Button>("OkButton")!;
        var cancelButton = this.FindControl<Button>("CancelButton")!;

        listBox.ItemsSource = recentValues;
        if (recentValues.Count > 0)
            listBox.SelectedIndex = 0;

        listBox.DoubleTapped += (s, e) =>
        {
            SelectedValue = listBox.SelectedItem as string;
            if (SelectedValue != null) Close();
        };

        okButton.Click += (s, e) =>
        {
            SelectedValue = listBox.SelectedItem as string;
            Close();
        };

        cancelButton.Click += (s, e) =>
        {
            SelectedValue = null;
            Close();
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SelectedValue = this.FindControl<ListBox>("RecentListBox")?.SelectedItem as string;
            Close();
        }
        else if (e.Key == Key.Escape)
        {
            SelectedValue = null;
            Close();
        }
        base.OnKeyDown(e);
    }
}
