using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using PsCloneTester.Services;

namespace PsCloneTester;

public partial class App : Application
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Contains("--test-gui"))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            try
            {
                var w = new MainWindow();
                w.Show();
                var cb = FindVisualChild<System.Windows.Controls.ComboBox>(w);
                if (cb != null)
                {
                    w.UpdateLayout();
                    cb.ApplyTemplate();

                    var hitInCb = cb.InputHitTest(new Point(50, 18)) as UIElement;
                    if (hitInCb != null)
                    {
                        var down = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                        {
                            RoutedEvent = System.Windows.Input.Mouse.MouseDownEvent,
                            Source = hitInCb
                        };
                        hitInCb.RaiseEvent(down);
                    }

                    if (!cb.IsDropDownOpen) throw new Exception("ComboBox failed to open drop down upon click!");
                    cb.IsDropDownOpen = false;
                }
                Console.WriteLine("SUCCESS: MainWindow instantiated, ComboBox dropdown click verified, and XAML parsed cleanly!");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex}");
                Shutdown(1);
            }
            return;
        }

        if (e.Args.Contains("--scan") || e.Args.Contains("-s"))
        {
            AttachConsole(ATTACH_PARENT_PROCESS);
            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("        Control - Escáner de Mandos HID           ");
            Console.WriteLine("==================================================");

            var svc = new HidDeviceService();
            var devices = svc.EnumerateDevices();

            if (devices.Count == 0)
            {
                Console.WriteLine("No se encontraron mandos o dispositivos HID de juego conectados.");
            }
            else
            {
                Console.WriteLine($"Se encontraron {devices.Count} dispositivo(s):");
                for (int i = 0; i < devices.Count; i++)
                {
                    var d = devices[i];
                    Console.WriteLine($"[{i + 1}] {d.DisplayTitle}");
                    Console.WriteLine($"    Tipo: {d.GetTypeFriendlyName()} ({d.DetectedType})");
                    Console.WriteLine($"    Conexión: {d.Connection}");
                    Console.WriteLine($"    Fabricante: {d.Manufacturer}");
                    Console.WriteLine($"    Producto: {d.ProductName}");
                    Console.WriteLine($"    VID: 0x{d.VendorId:X4} | PID: 0x{d.ProductId:X4}");
                    Console.WriteLine($"    Reportes: In={d.InputReportLength} bytes, Out={d.OutputReportLength} bytes");
                    Console.WriteLine($"    Ruta: {d.DevicePath}");
                    Console.WriteLine();
                }
            }
            Console.WriteLine("==================================================");
            Shutdown();
            return;
        }

        base.OnStartup(e);
        new MainWindow().Show();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
