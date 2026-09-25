using System.Windows;
using System.Windows.Media;

namespace PsCloneTester.Controls;

public class HorizonControl : FrameworkElement
{
    public static readonly DependencyProperty PitchProperty =
        DependencyProperty.Register(nameof(Pitch), typeof(double), typeof(HorizonControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RollProperty =
        DependencyProperty.Register(nameof(Roll), typeof(double), typeof(HorizonControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Pitch
    {
        get => (double)GetValue(PitchProperty);
        set => SetValue(PitchProperty, value);
    }

    public double Roll
    {
        get => (double)GetValue(RollProperty);
        set => SetValue(RollProperty, value);
    }

    private static readonly Brush SkyBrush = new SolidColorBrush(Color.FromRgb(15, 60, 110));
    private static readonly Brush GroundBrush = new SolidColorBrush(Color.FromRgb(45, 30, 20));
    private static readonly Pen HorizonPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 220, 255)), 2);
    private static readonly Pen ScalePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1.5);
    private static readonly Pen CrosshairPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 210, 0)), 3);
    private static readonly Brush CrosshairBrush = new SolidColorBrush(Color.FromRgb(255, 210, 0));
    private static readonly Pen OuterBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 50, 70)), 3);

    static HorizonControl()
    {
        HorizonPen.Freeze();
        ScalePen.Freeze();
        CrosshairPen.Freeze();
        OuterBorderPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 20) return;

        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;
        double radius = size / 2.0 - 4;

        // Clip circular para el instrumento esférico
        var clipGeom = new EllipseGeometry(new Point(cx, cy), radius, radius);
        dc.PushClip(clipGeom);

        // Guardar transformación para rotar e inclinar el horizonte artificial
        dc.PushTransform(new RotateTransform(Roll, cx, cy));

        // Desplazamiento vertical por Pitch (aproximadamente 2 píxeles por grado)
        double pitchOffset = -Pitch * (radius / 45.0);
        dc.PushTransform(new TranslateTransform(0, pitchOffset));

        // Cielo (arriba)
        dc.DrawRectangle(SkyBrush, null, new Rect(cx - radius * 2, cy - radius * 4, radius * 4, radius * 4));
        // Tierra (abajo)
        dc.DrawRectangle(GroundBrush, null, new Rect(cx - radius * 2, cy, radius * 4, radius * 4));
        // Línea central de horizonte
        dc.DrawLine(HorizonPen, new Point(cx - radius * 2, cy), new Point(cx + radius * 2, cy));

        // Escala de grados de inclinación (±10°, ±20°, ±30°)
        for (int deg = -30; deg <= 30; deg += 10)
        {
            if (deg == 0) continue;
            double yPos = cy - deg * (radius / 45.0);
            double lineW = (Math.Abs(deg) % 20 == 0) ? 40 : 24;

            dc.DrawLine(ScalePen, new Point(cx - lineW, yPos), new Point(cx + lineW, yPos));

            var ft = new FormattedText(
                Math.Abs(deg).ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                10,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(ft, new Point(cx + lineW + 4, yPos - ft.Height / 2));
            dc.DrawText(ft, new Point(cx - lineW - ft.Width - 4, yPos - ft.Height / 2));
        }

        dc.Pop(); // Pop Translate
        dc.Pop(); // Pop Rotate
        dc.Pop(); // Pop Clip

        // Borde exterior del reloj giroscópico
        dc.DrawEllipse(null, OuterBorderPen, new Point(cx, cy), radius, radius);

        // Mira de referencia fija (Cruz central del avión / mando)
        dc.DrawLine(CrosshairPen, new Point(cx - 30, cy), new Point(cx - 10, cy));
        dc.DrawLine(CrosshairPen, new Point(cx + 10, cy), new Point(cx + 30, cy));
        dc.DrawEllipse(CrosshairBrush, null, new Point(cx, cy), 3, 3);
        dc.DrawLine(CrosshairPen, new Point(cx, cy - 4), new Point(cx, cy - 14));

        // Lectura digital superpuesta
        var infoText = new FormattedText(
            $"P: {Pitch:F1}°  R: {Roll:F1}°",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Bold"),
            11,
            new SolidColorBrush(Color.FromRgb(0, 220, 255)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(infoText, new Point(cx - infoText.Width / 2, cy + radius - infoText.Height - 6));
    }
}
