namespace PsCloneTester.Models;

public enum ControllerType
{
    Unknown,
    DualSense,      // PS5
    DualSenseEdge,  // PS5 Edge
    DualShock4V1,   // PS4 v1
    DualShock4V2,   // PS4 v2
    ClonePs5,       // PS5 Imitation
    ClonePs4,       // PS4 Imitation
    Xbox360,        // Xbox 360
    XboxOne,        // Xbox One / S / X
    XboxSeries,     // Xbox Series X|S
    SwitchPro,      // Nintendo Switch Pro Controller
    SwitchJoyCon,   // Nintendo Joy-Con
    GenericHid      // Generic Gamepad
}

public enum ConnectionType
{
    Unknown,
    USB,
    Bluetooth,
    XInput
}

public class DeviceInfo
{
    public string DevicePath { get; set; } = string.Empty;
    public ushort VendorId { get; set; }
    public ushort ProductId { get; set; }
    public ushort VersionNumber { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public ushort UsagePage { get; set; }
    public ushort Usage { get; set; }
    public ControllerType DetectedType { get; set; } = ControllerType.Unknown;
    public ConnectionType Connection { get; set; } = ConnectionType.Unknown;
    public int InputReportLength { get; set; } = 64;
    public int OutputReportLength { get; set; } = 64;
    public int XInputUserIndex { get; set; } = -1;
    public bool IsXInput => XInputUserIndex >= 0 || Connection == ConnectionType.XInput;

    public string DisplayTitle
    {
        get
        {
            string name = !string.IsNullOrWhiteSpace(ProductName) ? ProductName : GetTypeFriendlyName();
            if (IsXInput)
            {
                return $"{name} [XInput Slot {XInputUserIndex + 1}]";
            }
            string conn = Connection == ConnectionType.USB ? "USB" : (Connection == ConnectionType.Bluetooth ? "BT" : "HID");
            return $"{name} [{conn}] (VID:{VendorId:X4} PID:{ProductId:X4})";
        }
    }

    public string GetTypeFriendlyName()
    {
        return DetectedType switch
        {
            ControllerType.DualSense => "Sony DualSense (PS5)",
            ControllerType.DualSenseEdge => "Sony DualSense Edge (PS5)",
            ControllerType.DualShock4V1 => "Sony DualShock 4 v1 (PS4)",
            ControllerType.DualShock4V2 => "Sony DualShock 4 v2 (PS4)",
            ControllerType.ClonePs5 => "Mando Imitación PS5",
            ControllerType.ClonePs4 => "Mando Imitación PS4",
            ControllerType.Xbox360 => "Mando Xbox 360",
            ControllerType.XboxOne => "Mando Xbox One",
            ControllerType.XboxSeries => "Mando Xbox Series X|S",
            ControllerType.SwitchPro => "Nintendo Switch Pro Controller",
            ControllerType.SwitchJoyCon => "Nintendo Joy-Con",
            ControllerType.GenericHid => "Mando HID Genérico",
            _ => "Dispositivo de Control"
        };
    }

    public override string ToString() => DisplayTitle;
}
