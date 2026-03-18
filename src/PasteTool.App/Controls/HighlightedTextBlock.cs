using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PasteTool.App.Models;

namespace PasteTool.App.Controls;

public sealed class HighlightedTextBlock : TextBlock
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(
            nameof(Item),
            typeof(HistoryListItem),
            typeof(HighlightedTextBlock),
            new PropertyMetadata(null, OnItemChanged));

    public HistoryListItem? Item
    {
        get => (HistoryListItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HighlightedTextBlock textBlock)
        {
            textBlock.UpdateInlines();
        }
    }

    private void UpdateInlines()
    {
        Inlines.Clear();

        if (Item == null)
        {
            return;
        }

        foreach (var segment in Item.HighlightedPreviewSegments)
        {
            var run = new Run(segment.Text);
            if (segment.IsHighlighted)
            {
                run.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 235, 59));
                run.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 26, 23));
                run.FontWeight = FontWeights.Bold;
            }
            Inlines.Add(run);
        }
    }
}
