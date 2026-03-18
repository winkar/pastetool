using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PasteTool.App.Models;

namespace PasteTool.App.Controls;

public sealed class HighlightedTextBlock : TextBlock
{
    private static readonly SolidColorBrush HighlightBackgroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(255, 235, 59));
    private static readonly SolidColorBrush HighlightForegroundBrush = CreateFrozenBrush(System.Windows.Media.Color.FromRgb(31, 26, 23));

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
            if (e.OldValue is HistoryListItem oldItem)
            {
                PropertyChangedEventManager.RemoveHandler(oldItem, textBlock.OnItemPropertyChanged, string.Empty);
            }

            if (e.NewValue is HistoryListItem newItem)
            {
                PropertyChangedEventManager.AddHandler(newItem, textBlock.OnItemPropertyChanged, string.Empty);
            }

            textBlock.UpdateInlines();
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HistoryListItem.HighlightedPreviewSegments) ||
            string.IsNullOrWhiteSpace(e.PropertyName))
        {
            UpdateInlines();
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
                run.Background = HighlightBackgroundBrush;
                run.Foreground = HighlightForegroundBrush;
                run.FontWeight = FontWeights.Bold;
            }
            Inlines.Add(run);
        }
    }

    private static SolidColorBrush CreateFrozenBrush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
