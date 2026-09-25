using System.Windows;
using System.Windows.Media;

namespace PsCloneTester.Controls;

public class WaveformControl : FrameworkElement
{
    public static readonly DependencyProperty HistoryXProperty =
        DependencyProperty.Register(nameof(HistoryX), typeof(List<double>), typeof(WaveformControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HistoryYProperty =
        DependencyProperty.Register(nameof(HistoryY), typeof(List<double>), typeof(WaveformControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HistoryZProperty =
        DependencyProperty.Register(nameof(HistoryZ), typeof(List<double>), typeof(WaveformControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public List<double>? HistoryX
    {
        get => (List<double>?)GetValue(HistoryXProperty);
        set => SetValue(HistoryXProperty, value);
    }

    public List<double>? HistoryY
    {
        get => (List<double>?)GetValue(HistoryYProperty);
        set => SetValue(HistoryYProperty, value);
    }

    public List<double>? HistoryZ
    {
        get => (List<double>?)GetValue(HistoryZProperty);
        set => SetValue(HistoryZProperty, value);
    }

    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(14, 16, 22));
    private static readonly Pen BorderPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 50, 70)), 1);
    private static readonly Pen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 80, 100, 130)), 1);
    private static readonly Pen ZeroPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 120, 150, 190)), 1);

    private static readonly Pen PenX = new Pen(new SolidColorBrush(Color.FromRgb(255, 75, 75)), 1.5);
    private static readonly Pen PenY = new Pen(new SolidColorBrush(Color.FromRgb(50, 220, 120)), 1.5);
    private static readonly Pen PenZ = new Pen(new SolidColorBrush(Color.FromRgb(40, 180, 255)), 1.5);

    static WaveformControl()
    {
        BorderPen.Freeze();
        GridPen.Freeze();
        ZeroPen.Freeze();
        PenX.Freeze();
        PenY.Freeze();
        PenZ.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 10 || h <= 10) return;

        dc.DrawRoundedRectangle(BgBrush, BorderPen, new Rect(0, 0, w, h), 6, 6);

        // Línea central 0G
        double midY = h / 2.0;
        dc.DrawLine(ZeroPen, new Point(0, midY), new Point(w, midY));

        // Rejilla ±1G y ±2G
        double scaleY = h / 6.0; // ±3G rango completo
        dc.DrawLine(GridPen, new Point(0, midY - scaleY), new Point(w, midY - scaleY));
        dc.DrawLine(GridPen, new Point(0, midY + scaleY), new Point(w, midY + scaleY));
        dc.DrawLine(GridPen, new Point(0, midY - scaleY * 2), new Point(w, midY - scaleY * 2));
        dc.DrawLine(GridPen, new Point(0, midY + scaleY * 2), new Point(w, midY + scaleY * 2));

        DrawSeries(dc, HistoryX, PenX, midY, scaleY, w);
        DrawSeries(dc, HistoryY, PenY, midY, scaleY, w);
        DrawSeries(dc, HistoryZ, PenZ, midY, scaleY, w);
    }

    private void DrawSeries(DrawingContext dc, List<double>? list, Pen pen, double midY, double scaleY, double w)
    {
        if (list == null || list.Count < 2) return;

        double stepX = w / Math.Max(list.Count - 1, 1);
        var geom = new StreamGeometry();
        using (var ctx = geom.Open())
        {
            for (int i = 0; i < list.Count; i++)
            {
                double px = i * stepX;
                double py = Math.Clamp(midY - (list[i] * scaleY), 2, ActualHeight - 2);

                if (i == 0)
                    ctx.BeginFigure(new Point(px, py), false, false);
                else
                    ctx.LineTo(new Point(px, py), true, true);
            }
        }
        geom.Freeze();
        dc.DrawGeometry(null, pen, geom);
    }
}
