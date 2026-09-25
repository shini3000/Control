using System.Windows;
using System.Windows.Media;

namespace PsCloneTester.Controls;

public class ThumbstickControl : FrameworkElement
{
    public static readonly DependencyProperty StickXProperty =
        DependencyProperty.Register(nameof(StickX), typeof(double), typeof(ThumbstickControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StickYProperty =
        DependencyProperty.Register(nameof(StickY), typeof(double), typeof(ThumbstickControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsClickedProperty =
        DependencyProperty.Register(nameof(IsClicked), typeof(bool), typeof(ThumbstickControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ThumbstickControl),
            new FrameworkPropertyMetadata("STICK", FrameworkPropertyMetadataOptions.AffectsRender));

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

    public bool IsClicked
    {
        get => (bool)GetValue(IsClickedProperty);
        set => SetValue(IsClickedProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static readonly Brush BgBrush = new SolidColorBrush(Color.FromRgb(14, 16, 23));
    private static readonly Pen OuterPen = new Pen(new SolidColorBrush(Color.FromRgb(36, 43, 60)), 1.5);
    private static readonly Pen MidPen = new Pen(new SolidColorBrush(Color.FromRgb(24, 29, 42)), 1.0);
    private static readonly Pen InnerPen = new Pen(new SolidColorBrush(Color.FromRgb(20, 24, 35)), 1.0);
    private static readonly Pen DeadzonePen = new Pen(new SolidColorBrush(Color.FromArgb(90, 50, 65, 90)), 1.0)
    {
        DashStyle = DashStyles.Dash
    };
    private static readonly Pen AxisPen = new Pen(new SolidColorBrush(Color.FromRgb(28, 34, 48)), 1.0);
    private static readonly Brush CursorDotBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush CursorGlowBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
    private static readonly Brush ClickedGlowBrush = new SolidColorBrush(Color.FromArgb(140, 0, 229, 153));
    private static readonly Pen ClickedPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 229, 153)), 2);

    static ThumbstickControl()
    {
        OuterPen.Freeze();
        MidPen.Freeze();
        InnerPen.Freeze();
        DeadzonePen.Freeze();
        AxisPen.Freeze();
        CursorDotBrush.Freeze();
        CursorGlowBrush.Freeze();
        ClickedGlowBrush.Freeze();
        ClickedPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 20) return;

        double cx = ActualWidth / 2.0;
        double cy = ActualHeight / 2.0;
        double radius = size / 2.0 - 6;

        // Fondo y círculo exterior
        dc.DrawEllipse(BgBrush, OuterPen, new Point(cx, cy), radius, radius);

        // Anillos concéntricos interiores
        dc.DrawEllipse(null, MidPen, new Point(cx, cy), radius * 0.66, radius * 0.66);
        dc.DrawEllipse(null, InnerPen, new Point(cx, cy), radius * 0.33, radius * 0.33);

        // Zona muerta central (~12%)
        dc.DrawEllipse(null, DeadzonePen, new Point(cx, cy), radius * 0.12, radius * 0.12);

        // Ejes X / Y ortogonales
        dc.DrawLine(AxisPen, new Point(cx - radius + 2, cy), new Point(cx + radius - 2, cy));
        dc.DrawLine(AxisPen, new Point(cx, cy - radius + 2), new Point(cx, cy + radius - 2));

        // Posición actual del stick
        double sx = Math.Clamp(StickX, -1.0, 1.0);
        double sy = Math.Clamp(StickY, -1.0, 1.0);

        // Rango de recorrido del cursor
        double maxTravel = radius * 0.88;
        double hx = cx + (sx * maxTravel);
        double hy = cy + (sy * maxTravel);

        // Halo de brillo
        if (IsClicked)
        {
            dc.DrawEllipse(ClickedGlowBrush, ClickedPen, new Point(hx, hy), 8, 8);
        }
        else
        {
            dc.DrawEllipse(CursorGlowBrush, null, new Point(hx, hy), 7, 7);
        }

        // Punto central brillante blanco
        dc.DrawEllipse(CursorDotBrush, null, new Point(hx, hy), 3.5, 3.5);
    }
}
