using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DrawPath = System.Windows.Shapes.Path;

namespace HopeChestCore;

public partial class MainWindow
{
    private static readonly Brush GridLine = MakeBrush(0x46, 0x43, 0x6A);
    private static readonly Brush MarkerEdge = MakeBrush(0x28, 0x27, 0x49);

    private void ChartResized(object sender, SizeChangedEventArgs e) => DrawCharts();
    private void ChangeChartMetric(object sender, RoutedEventArgs e) => DrawCharts();

    private void DrawCharts()
    {
        if (ChestChart is null || ChartEmpty is null)
            return;

        var perMinute = ShowMinuteCharts?.IsChecked == true;
        var total = _chartRows.Select(match => perMinute ? match.TotalPerMinute : match.TotalChests).ToArray();
        var own = _chartRows.Select(match => perMinute ? match.OwnPerMinute : match.OwnChests).ToArray();
        var message = _chartRows.Length == 0 ? "Noch keine Matches."
            : total.All(value => !value.HasValue) ? "Keine Replay-Dauer verfügbar." : "";
        ChartEmpty.Text = message;
        ChartEmpty.Visibility = message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        ChestChart.Children.Clear();
        if (message.Length > 0 || ChestChart.ActualWidth < 100 || ChestChart.ActualHeight < 45)
            return;

        var rawStep = Math.Max(perMinute ? 0.1 : 1, total.Max().GetValueOrDefault() / 4);
        var scale = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var step = new[] { 1, 2, 5, 10 }.First(number => number * scale >= rawStep) * scale;

        var chart = ChestChart;
        const double left = 48;
        const double top = 9;
        var right = chart.ActualWidth - 13;
        var bottom = chart.ActualHeight - 21;
        var muted = (Brush)FindResource("Muted");

        for (var tick = 0; tick <= 4; tick++)
        {
            var y = bottom - (bottom - top) * tick / 4;
            chart.Children.Add(new Line { X1 = left, X2 = right, Y1 = y, Y2 = y, Stroke = GridLine });
            var label = new TextBlock
            {
                Text = (tick * step).ToString(perMinute ? "0.##" : "N0", German),
                Foreground = muted, FontSize = 10, Width = 40, TextAlignment = TextAlignment.Right
            };
            Canvas.SetTop(label, y - 7);
            chart.Children.Add(label);
        }

        var positions = _chartRows.Select((_, index) => total.Length == 1
            ? (left + right) / 2 : left + (right - left) * index / (total.Length - 1)).ToArray();
        var markers = new List<Ellipse>();
        DrawSeries(total, (SolidColorBrush)FindResource("Periwinkle"), fill: true);
        DrawSeries(own, (SolidColorBrush)FindResource("Rose"), fill: false);
        foreach (var marker in markers)
            chart.Children.Add(marker);

        var labelCount = Math.Min(5, positions.Length);
        for (var i = 0; i < labelCount; i++)
        {
            var index = labelCount == 1 ? 0 : (int)Math.Round(i * (positions.Length - 1.0) / (labelCount - 1));
            var label = new TextBlock { Text = _chartRows[index].PlayedAt.ToString("dd.MM.", German), Foreground = muted, FontSize = 10 };
            label.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, Math.Min(positions[index] - label.DesiredSize.Width / 2, chart.ActualWidth - label.DesiredSize.Width));
            Canvas.SetTop(label, bottom + 6);
            chart.Children.Add(label);
        }

        void DrawSeries(double?[] values, SolidColorBrush accent, bool fill)
        {
            var unit = perMinute ? "Truhen / Min." : "Truhen";
            var format = perMinute ? "N2" : "N0";
            var points = new List<Point>();
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is not { } value)
                {
                    DrawSegment(chart, points, bottom, accent, fill);
                    points.Clear();
                    continue;
                }

                var match = _chartRows[i];
                var point = new Point(positions[i], bottom - (bottom - top) * value / (step * 4));
                points.Add(point);
                var dot = new Ellipse
                {
                    Width = fill ? 10 : 8, Height = fill ? 10 : 8,
                    Fill = accent, Stroke = MarkerEdge, StrokeThickness = 1.5,
                    ToolTip = $"{match.PlayedAt:dd.MM.yyyy HH:mm} · {match.Mode}\nGesamt {total[i]!.Value.ToString(format, German)} · Eigene {own[i]!.Value.ToString(format, German)} {unit}"
                };
                Canvas.SetLeft(dot, point.X - dot.Width / 2);
                Canvas.SetTop(dot, point.Y - dot.Height / 2);
                markers.Add(dot);
            }

            DrawSegment(chart, points, bottom, accent, fill);
        }
    }

    private static void DrawSegment(Canvas chart, List<Point> points, double bottom, SolidColorBrush accent, bool fill)
    {
        if (points.Count < 2)
            return;

        var curve = new PolyBezierSegment();
        for (var i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1];
            var current = points[i];
            var middle = (previous.X + current.X) / 2;
            curve.Points.Add(new Point(middle, previous.Y));
            curve.Points.Add(new Point(middle, current.Y));
            curve.Points.Add(current);
        }

        if (fill)
        {
            var area = new PathFigure { StartPoint = new Point(points[0].X, bottom), IsClosed = true };
            area.Segments.Add(new LineSegment(points[0], false));
            area.Segments.Add(curve.Clone());
            area.Segments.Add(new LineSegment(new Point(points[^1].X, bottom), false));
            var color = accent.Color;
            chart.Children.Add(new DrawPath
            {
                Data = new PathGeometry(new[] { area }),
                Fill = new LinearGradientBrush(Color.FromArgb(0x45, color.R, color.G, color.B), Colors.Transparent, 90),
                IsHitTestVisible = false
            });
        }

        var line = new PathFigure { StartPoint = points[0] };
        line.Segments.Add(curve);
        chart.Children.Add(new DrawPath
        {
            Data = new PathGeometry(new[] { line }), Stroke = accent, StrokeThickness = 2,
            IsHitTestVisible = false
        });
    }

    private static SolidColorBrush MakeBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
