using System.Windows;
using System.Windows.Media;

namespace PsCloneTester.Controls;

public class TriggerBarControl : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(TriggerBarControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(TriggerBarControl),
            new FrameworkPropertyMetadata(255.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(nameof(BarBrush), typeof(Brush), typeof(TriggerBarControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CapBrushProperty =
        DependencyProperty.Register(nameof(CapBrush), typeof(Brush), typeof(TriggerBarControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public Brush? CapBrush
    {
        get => (Brush?)GetValue(CapBrushProperty);
        set => SetValue(CapBrushProperty, value);
    }

    private static readonly Brush TrackBgBrush = new SolidColorBrush(Color.FromRgb(10, 13, 20));
    private static readonly Pen TrackBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(32, 42, 60)), 1.5);
    private static readonly Pen EmptyTickPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 70, 90, 120)), 1)
    {
        DashStyle = DashStyles.Dash
    };
    private static readonly Pen FillTickPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1)
    {
        DashStyle = DashStyles.Dash
    };
    private static readonly Pen NotchPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 100, 130, 170)), 1.5);

    private static readonly Brush DefaultCyanFill;
    private static readonly Brush DefaultCapBrush = new SolidColorBrush(Colors.White);

    static TriggerBarControl()
    {
        TrackBgBrush.Freeze();
        TrackBorderPen.Freeze();
        EmptyTickPen.Freeze();
        FillTickPen.Freeze();
        NotchPen.Freeze();
        DefaultCapBrush.Freeze();

        var cyanGrad = new LinearGradientBrush(
            Color.FromRgb(0, 120, 255),
            Color.FromRgb(0, 225, 255),
            new Point(0, 1),
            new Point(0, 0));
        cyanGrad.Freeze();
        DefaultCyanFill = cyanGrad;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 8 || h <= 8) return;

        double padding = 2.0;
        double trackX = padding;
        double trackY = padding;
        double trackW = w - (padding * 2);
        double trackH = h - (padding * 2);

        // 1. Pista de fondo (Track)
        var trackRect = new Rect(trackX, trackY, trackW, trackH);
        dc.DrawRoundedRectangle(TrackBgBrush, TrackBorderPen, trackRect, 8, 8);

        // 2. Barra de llenado (Sube desde el fondo hacia arriba)
        double max = Maximum > 0 ? Maximum : 255.0;
        double ratio = Math.Clamp(Value / max, 0.0, 1.0);
        double fillH = ratio * (trackH - 4);
        double fillY = trackY + trackH - 2 - fillH;

        if (fillH > 1.5)
        {
            var fillRect = new Rect(trackX + 2, fillY, trackW - 4, fillH);

            // Clip de esquinas redondeadas para que coincida con el contorno de la pista
            var clipGeom = new RectangleGeometry(trackRect, 8, 8);
            clipGeom.Freeze();
            dc.PushClip(clipGeom);

            // Relleno de color
            Brush activeBrush = BarBrush ?? DefaultCyanFill;
            dc.DrawRoundedRectangle(activeBrush, null, fillRect, 6, 6);

            // Línea de cresta / tope luminoso
            Brush activeCap = CapBrush ?? DefaultCapBrush;
            var capPen = new Pen(activeCap, 2.5);
            capPen.Freeze();
            dc.DrawLine(capPen, new Point(trackX + 4, fillY + 1), new Point(trackX + trackW - 4, fillY + 1));

            dc.Pop(); // Quitar clip
        }

        // 3. Líneas de escala (25%, 50%, 75%) visibles tanto en vacío como sobre el relleno
        for (int p = 25; p <= 75; p += 25)
        {
            double gy = trackY + trackH * (1.0 - p / 100.0);
            bool isSubmerged = (fillH > 1.5) && (gy >= fillY);
            Pen tickPen = isSubmerged ? FillTickPen : EmptyTickPen;

            // Muescas laterales sólidas
            dc.DrawLine(NotchPen, new Point(trackX + 2, gy), new Point(trackX + 8, gy));
            dc.DrawLine(NotchPen, new Point(trackX + trackW - 8, gy), new Point(trackX + trackW - 2, gy));

            // Línea central discontinua
            dc.DrawLine(tickPen, new Point(trackX + 11, gy), new Point(trackX + trackW - 11, gy));
        }
    }
}
