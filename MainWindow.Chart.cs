using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DrawPath = System.Windows.Shapes.Path;

namespace HopeChestCore;

public partial class MainWindow
{
    private static readonly Brush GridLine = MakeBrush(0x29, 0x41, 0x68);
    private static readonly Brush MarkerEdge = MakeBrush(0x13, 0x22, 0x40);

    private void ChartResized(object sender, SizeChangedEventArgs e) => DrawCharts();
    private void ChangeChartMetric(object sender, RoutedEventArgs e) => DrawCharts();

    private void DrawCharts()
    {
        if (TotalChart is null || OwnChart is null || TotalChartEmpty is null || OwnChartEmpty is null)
            return;

        var perMinute = ShowMinuteCharts?.IsChecked == true;
        var total = _chartRows.Select(match => perMinute ? match.TotalPerMinute : match.TotalChests).ToArray();
        var own = _chartRows.Select(match => perMinute ? match.OwnPerMinute : match.OwnChests).ToArray();
        var message = _chartRows.Length == 0 ? "Noch keine Matches."
            : total.All(value => !value.HasValue) ? "Keine Replay-Dauer verfügbar." : "";
        TotalChartEmpty.Text = OwnChartEmpty.Text = message;
        TotalChartEmpty.Visibility = OwnChartEmpty.Visibility = message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        var rawStep = Math.Max(perMinute ? 0.1 : 1, total.Max().GetValueOrDefault() / 4);
        var scale = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var step = new[] { 1, 2, 5, 10 }.First(number => number * scale >= rawStep) * scale;

        DrawChart(TotalChart, total, step, perMinute, (SolidColorBrush)FindResource("Cyan"));
        DrawChart(OwnChart, own, step, perMinute, (SolidColorBrush)FindResource("Violet"));
    }

    private void DrawChart(Canvas chart, double?[] values, double step, bool perMinute, SolidColorBrush accent)
    {
        chart.Children.Clear();
        if (values.All(value => !value.HasValue) || chart.ActualWidth < 100 || chart.ActualHeight < 45)
            return;

        const double left = 39;
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
                Foreground = muted, FontSize = 10, Width = 33, TextAlignment = TextAlignment.Right
            };
            Canvas.SetTop(label, y - 7);
            chart.Children.Add(label);
        }

        var positions = _chartRows.Select((_, index) => values.Length == 1
            ? (left + right) / 2 : left + (right - left) * index / (values.Length - 1)).ToArray();
        var points = new List<Point>();
        var markers = new List<Ellipse>();

        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is not { } value)
            {
                DrawSegment(chart, points, bottom, accent);
                points.Clear();
                continue;
            }

            var match = _chartRows[i];
            var point = new Point(positions[i], bottom - (bottom - top) * value / (step * 4));
            points.Add(point);
            var measurement = perMinute ? value.ToString("N2", German) + " Truhen / Min." : value.ToString("N0", German) + " Truhen";
            var dot = new Ellipse
            {
                Width = 10, Height = 10, Fill = accent, Stroke = MarkerEdge, StrokeThickness = 2,
                ToolTip = $"{match.PlayedAt:dd.MM.yyyy HH:mm} · {match.Mode}\n{measurement}"
            };
            Canvas.SetLeft(dot, point.X - dot.Width / 2);
            Canvas.SetTop(dot, point.Y - dot.Height / 2);
            markers.Add(dot);
        }

        DrawSegment(chart, points, bottom, accent);
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
    }

    private static void DrawSegment(Canvas chart, List<Point> points, double bottom, SolidColorBrush accent)
    {
        if (points.Count < 2)
            return;

        var area = new PathFigure { StartPoint = new Point(points[0].X, bottom), IsClosed = true };
        area.Segments.Add(new PolyLineSegment(points.Append(new Point(points[^1].X, bottom)), true));
        var color = accent.Color;
        chart.Children.Add(new DrawPath
        {
            Data = new PathGeometry(new[] { area }),
            Fill = new LinearGradientBrush(Color.FromArgb(0x60, color.R, color.G, color.B), Colors.Transparent, 90),
            IsHitTestVisible = false
        });
        chart.Children.Add(new Polyline { Points = new PointCollection(points), Stroke = accent, StrokeThickness = 2, IsHitTestVisible = false });
    }

    private static SolidColorBrush MakeBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
