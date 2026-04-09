using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ExifEditor.Controls;

public class CircularProgressControl : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CircularProgressControl, double>(nameof(Value));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<CircularProgressControl, double>(nameof(Maximum), 100);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<CircularProgressControl, double>(nameof(StrokeThickness), 8);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<CircularProgressControl, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<CircularProgressControl, IBrush?>(nameof(IndicatorBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<CircularProgressControl, IBrush?>(nameof(TextBrush));

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? IndicatorBrush
    {
        get => GetValue(IndicatorBrushProperty);
        set => SetValue(IndicatorBrushProperty, value);
    }

    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    static CircularProgressControl()
    {
        AffectsRender<CircularProgressControl>(ValueProperty, MaximumProperty,
            StrokeThicknessProperty, TrackBrushProperty, IndicatorBrushProperty, TextBrushProperty);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = (size - StrokeThickness) / 2;
        var trackPen = new Pen(TrackBrush ?? Brushes.Gray, StrokeThickness);
        var indicatorPen = new Pen(IndicatorBrush ?? Brushes.DodgerBlue, StrokeThickness,
            lineCap: PenLineCap.Round);

        // Draw track circle
        context.DrawEllipse(null, trackPen, center, radius, radius);

        // Draw progress arc
        var fraction = Maximum > 0 ? Math.Clamp(Value / Maximum, 0, 1) : 0;
        if (fraction > 0)
        {
            var sweepAngle = fraction * 360;
            var startAngle = -90.0; // start from top

            var startRad = startAngle * Math.PI / 180;
            var endRad = (startAngle + sweepAngle) * Math.PI / 180;

            var startPoint = new Point(
                center.X + radius * Math.Cos(startRad),
                center.Y + radius * Math.Sin(startRad));
            var endPoint = new Point(
                center.X + radius * Math.Cos(endRad),
                center.Y + radius * Math.Sin(endRad));

            var arcSize = new Size(radius, radius);
            var isLargeArc = sweepAngle > 180;

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(startPoint, false);
                ctx.ArcTo(endPoint, arcSize, 0, isLargeArc, SweepDirection.Clockwise);
            }

            context.DrawGeometry(null, indicatorPen, geometry);
        }

        // Draw percentage text
        var percent = (int)(fraction * 100);
        var text = new FormattedText(
            $"{percent}%",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Bold),
            size * 0.22,
            TextBrush ?? Brushes.White);

        var textOrigin = new Point(
            center.X - text.Width / 2,
            center.Y - text.Height / 2);
        context.DrawText(text, textOrigin);
    }
}
