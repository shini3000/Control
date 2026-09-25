using System.Windows;
using System.Windows.Media;
using PsCloneTester.Models;

namespace PsCloneTester.Controls;

public class TouchpadControl : FrameworkElement
{
    public static readonly DependencyProperty Touch1Property =
        DependencyProperty.Register(nameof(Touch1), typeof(TouchPoint), typeof(TouchpadControl),
            new FrameworkPropertyMetadata(default(TouchPoint), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty Touch2Property =
        DependencyProperty.Register(nameof(Touch2), typeof(TouchPoint), typeof(TouchpadControl),
            new FrameworkPropertyMetadata(default(TouchPoint), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsClickedProperty =
        DependencyProperty.Register(nameof(IsClicked), typeof(bool), typeof(TouchpadControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrailPointsProperty =
        DependencyProperty.Register(nameof(TrailPoints), typeof(List<Point>), typeof(TouchpadControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public TouchPoint Touch1
    {
        get => (TouchPoint)GetValue(Touch1Property);
        set => SetValue(Touch1Property, value);
    }

    public TouchPoint Touch2
    {
        get => (TouchPoint)GetValue(Touch2Property);
        set => SetValue(Touch2Property, value);
    }

    public bool IsClicked
    {
        get => (bool)GetValue(IsClickedProperty);
        set => SetValue(IsClickedProperty, value);
    }

    public List<Point>? TrailPoints
    {
        get => (List<Point>?)GetValue(TrailPointsProperty);
        set => SetValue(TrailPointsProperty, value);
    }

    private static readonly Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(18, 20, 28));
    private static readonly Brush BorderBrushNormal = new SolidColorBrush(Color.FromRgb(50, 60, 85));
    private static readonly Brush BorderBrushClicked = new SolidColorBrush(Color.FromRgb(0, 220, 130));
    private static readonly Pen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 100, 130, 180)), 1);

    private static readonly Brush Touch1Brush = new SolidColorBrush(Color.FromRgb(0, 200, 255));
    private static readonly Brush Touch1Glow = new SolidColorBrush(Color.FromArgb(80, 0, 200, 255));
    private static readonly Brush Touch2Brush = new SolidColorBrush(Color.FromRgb(255, 120, 0));
    private static readonly Brush Touch2Glow = new SolidColorBrush(Color.FromArgb(80, 255, 120, 0));

    private static readonly Pen TrailPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 220, 255)), 2);

    static TouchpadControl()
    {
        GridPen.Freeze();
        TrailPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 10 || height <= 10) return;

        var rect = new Rect(0, 0, width, height);

        // Fondo del touchpad
        dc.DrawRoundedRectangle(BackgroundBrush, null, rect, 12, 12);

        // Rejilla sutil de calibración (4x2 cuadrículas)
        for (int i = 1; i < 4; i++)
        {
            double x = (width / 4) * i;
            dc.DrawLine(GridPen, new Point(x, 4), new Point(x, height - 4));
        }
        for (int j = 1; j < 3; j++)
        {
            double y = (height / 3) * j;
            dc.DrawLine(GridPen, new Point(4, y), new Point(width - 4, y));
        }

        // Borde exterior (cambia de color cuando se pulsa el botón físico del trackpad)
        var borderPen = new Pen(IsClicked ? BorderBrushClicked : BorderBrushNormal, IsClicked ? 3 : 1.5);
        dc.DrawRoundedRectangle(null, borderPen, rect, 12, 12);

        // Dibujar trazos históricos del dedo (para probar zonas muertas)
        if (TrailPoints != null && TrailPoints.Count > 1)
        {
            var streamGeom = new StreamGeometry();
            using (var ctx = streamGeom.Open())
            {
                bool first = true;
                foreach (var pt in TrailPoints)
                {
                    double sx = Math.Clamp(pt.X / 1920.0 * width, 0, width);
                    double sy = Math.Clamp(pt.Y / 942.0 * height, 0, height);
                    if (first)
                    {
                        ctx.BeginFigure(new Point(sx, sy), false, false);
                        first = false;
                    }
                    else
                    {
                        ctx.LineTo(new Point(sx, sy), true, true);
                    }
                }
            }
            streamGeom.Freeze();
            dc.DrawGeometry(null, TrailPen, streamGeom);
        }

        // Dedo 1 (Azul Neón)
        if (Touch1.IsActive)
        {
            double x = Math.Clamp(Touch1.NormalizedX * width, 8, width - 8);
            double y = Math.Clamp(Touch1.NormalizedY * height, 8, height - 8);

            // Halo de brillo
            dc.DrawEllipse(Touch1Glow, null, new Point(x, y), 24, 24);
            dc.DrawEllipse(Touch1Brush, null, new Point(x, y), 8, 8);

            // Etiqueta de coordenadas
            var ft = new FormattedText(
                $"Dedo 1 [X:{Touch1.RawX}, Y:{Touch1.RawY}]",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                11,
                Touch1Brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double labelX = Math.Clamp(x + 12, 4, width - ft.Width - 4);
            double labelY = Math.Clamp(y - 18, 4, height - ft.Height - 4);
            dc.DrawText(ft, new Point(labelX, labelY));
        }

        // Dedo 2 (Naranja Neón)
        if (Touch2.IsActive)
        {
            double x = Math.Clamp(Touch2.NormalizedX * width, 8, width - 8);
            double y = Math.Clamp(Touch2.NormalizedY * height, 8, height - 8);

            dc.DrawEllipse(Touch2Glow, null, new Point(x, y), 24, 24);
            dc.DrawEllipse(Touch2Brush, null, new Point(x, y), 8, 8);

            var ft = new FormattedText(
                $"Dedo 2 [X:{Touch2.RawX}, Y:{Touch2.RawY}]",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI Semibold"),
                11,
                Touch2Brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double labelX = Math.Clamp(x + 12, 4, width - ft.Width - 4);
            double labelY = Math.Clamp(y - 18, 4, height - ft.Height - 4);
            dc.DrawText(ft, new Point(labelX, labelY));
        }

        // Indicador si el botón físico está presionado
        if (IsClicked)
        {
            var clickText = new FormattedText(
                "¡CLIC FÍSICO ACTIVO!",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                12,
                BorderBrushClicked,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(clickText, new Point(width / 2 - clickText.Width / 2, height - clickText.Height - 8));
        }
    }
}
