using System.Windows;
using System.Windows.Media;

namespace PsCloneTester.Controls;

public class CircularityControl : FrameworkElement
{
    public static readonly DependencyProperty StickXProperty =
        DependencyProperty.Register(nameof(StickX), typeof(double), typeof(CircularityControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StickYProperty =
        DependencyProperty.Register(nameof(StickY), typeof(double), typeof(CircularityControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SectorPointsProperty =
        DependencyProperty.Register(nameof(SectorPoints), typeof(double[]), typeof(CircularityControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SamplePointsProperty =
        DependencyProperty.Register(nameof(SamplePoints), typeof(List<Point>), typeof(CircularityControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsCompletedProperty =
        DependencyProperty.Register(nameof(IsCompleted), typeof(bool), typeof(CircularityControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AverageErrorProperty =
        DependencyProperty.Register(nameof(AverageError), typeof(double), typeof(CircularityControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double StickX
    {
        get => (double)GetValue(StickXProperty);
        set => SetValue(StickXProperty, value);
    }

    public double StickY
    {
        get => (double)GetValue(StickYProperty);
        set => SetValue(StickYProperty, value);
    }

    public double[]? SectorPoints
    {
        get => (double[]?)GetValue(SectorPointsProperty);
        set => SetValue(SectorPointsProperty, value);
    }

    public List<Point>? SamplePoints
    {
        get => (List<Point>?)GetValue(SamplePointsProperty);
        set => SetValue(SamplePointsProperty, value);
    }

    public bool IsCompleted
    {
        get => (bool)GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    public double AverageError
    {
        get => (double)GetValue(AverageErrorProperty);
        set => SetValue(AverageErrorProperty, value);
    }

    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(14, 16, 24));
    private static readonly Pen OuterBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(35, 45, 65)), 1.5);
    private static readonly Pen AxisPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 100, 140, 200)), 1);

    // Círculo ideal de referencia (r = 1.0)
    private static readonly Pen IdealCirclePen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 195, 255)), 1.5)
    {
        DashStyle = DashStyles.Dash
    };

    // Polígono medido
    private static readonly Brush PolygonFill = new SolidColorBrush(Color.FromArgb(50, 0, 229, 153));
    private static readonly Pen PolygonPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 229, 153)), 2);

    private static readonly Brush PointBrush = new SolidColorBrush(Color.FromArgb(140, 255, 179, 0));
    private static readonly Brush LiveCursorBrush = new SolidColorBrush(Color.FromRgb(255, 68, 204));
    private static readonly Pen LiveCrossPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 68, 204)), 2);

    static CircularityControl()
    {
        OuterBorderPen.Freeze();
        AxisPen.Freeze();
        IdealCirclePen.Freeze();
        PolygonFill.Freeze();
        PolygonPen.Freeze();
        LiveCrossPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 20) return;

        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;
        // Escala: radio 1.0 ocupa el 72% del espacio para dar margen a overshoot (ej: r=1.3 de mandos cuadrados)
        double unitRadius = (size / 2.0 - 10) * 0.72;

        // Fondo y marco
        dc.DrawRoundedRectangle(BgBrush, OuterBorderPen, new Rect(0, 0, ActualWidth, ActualHeight), 10, 10);

        // Ejes X / Y
        dc.DrawLine(AxisPen, new Point(cx, 10), new Point(cx, ActualHeight - 10));
        dc.DrawLine(AxisPen, new Point(10, cy), new Point(ActualWidth - 10, cy));

        // Círculo de referencia ideal (r = 1.0)
        dc.DrawEllipse(null, IdealCirclePen, new Point(cx, cy), unitRadius, unitRadius);

        // Círculo exterior límite (r = 1.35)
        var limitPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 255, 68, 68)), 1);
        dc.DrawEllipse(null, limitPen, new Point(cx, cy), unitRadius * 1.35, unitRadius * 1.35);

        // Puntos muestreados acumulados (Scatter de muestras directas del stick)
        if (SamplePoints != null && SamplePoints.Count > 0)
        {
            foreach (var pt in SamplePoints)
            {
                double px = cx + (pt.X * unitRadius);
                double py = cy + (pt.Y * unitRadius);
                dc.DrawEllipse(PointBrush, null, new Point(px, py), 2, 2);
            }
        }

        // Perímetro medido por sectores angulares
        if (SectorPoints != null && SectorPoints.Length >= 8)
        {
            int count = SectorPoints.Length;
            double angleStep = 2.0 * Math.PI / count;

            int validCount = 0;
            for (int k = 0; k < count; k++)
            {
                if (SectorPoints[k] > 0.35) validCount++;
            }
            bool shouldFill = IsCompleted || (validCount >= count * 0.85);

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                bool figureStarted = false;
                int lastVisitedIndex = -999;

                for (int i = 0; i < count; i++)
                {
                    double r = SectorPoints[i];
                    if (r < 0.35) continue; // Nunca conectar hacia el centro

                    double angle = i * angleStep;
                    double px = cx + (r * Math.Cos(angle) * unitRadius);
                    double py = cy + (r * Math.Sin(angle) * unitRadius);

                    if (!figureStarted || (i - lastVisitedIndex > 2 && !shouldFill))
                    {
                        ctx.BeginFigure(new Point(px, py), isFilled: shouldFill, isClosed: shouldFill);
                        figureStarted = true;
                    }
                    else
                    {
                        ctx.LineTo(new Point(px, py), true, true);
                    }
                    lastVisitedIndex = i;
                }
            }
            geom.Freeze();

            if (shouldFill)
            {
                dc.DrawGeometry(PolygonFill, PolygonPen, geom);
            }
            else
            {
                dc.DrawGeometry(null, PolygonPen, geom);
            }
        }

        // Posición actual del stick (Cursor rosa neón)
        double currentX = cx + (StickX * unitRadius);
        double currentY = cy + (StickY * unitRadius);

        dc.DrawLine(LiveCrossPen, new Point(currentX - 7, currentY), new Point(currentX + 7, currentY));
        dc.DrawLine(LiveCrossPen, new Point(currentX, currentY - 7), new Point(currentX, currentY + 7));
        dc.DrawEllipse(LiveCursorBrush, null, new Point(currentX, currentY), 4, 4);

        // Etiquetas fijas
        var idealLabel = new FormattedText(
            "Círculo Ideal (r = 1.0)",
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"),
            10,
            new SolidColorBrush(Color.FromRgb(0, 195, 255)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(idealLabel, new Point(cx + unitRadius - idealLabel.Width, cy - unitRadius - 16));

        // Indicador central de Error Promedio en el Canvas (Estilo Gamepad-Tester)
        if (AverageError > 0.0)
        {
            var errorText = new FormattedText(
                $"Avg Error: {AverageError:F1}%",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                13,
                new SolidColorBrush(Color.FromRgb(0, 229, 153)),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // Badge semitransparente detrás del texto
            double badgeW = errorText.Width + 14;
            double badgeH = errorText.Height + 6;
            var badgeRect = new Rect(14, 14, badgeW, badgeH);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(180, 16, 22, 34)), new Pen(new SolidColorBrush(Color.FromRgb(0, 229, 153)), 1), badgeRect, 4, 4);
            dc.DrawText(errorText, new Point(21, 17));
        }
    }
}
