namespace PsCloneTester.Models;

public struct TouchPoint
{
    public bool IsActive { get; set; }
    public byte Id { get; set; }
    public ushort RawX { get; set; }
    public ushort RawY { get; set; }
    public double NormalizedX => Math.Clamp(RawX / 1920.0, 0.0, 1.0);
    public double NormalizedY => Math.Clamp(RawY / 942.0, 0.0, 1.0);
}

public enum BatteryStatus
{
    Unknown,
    Discharging,
    Charging,
    Full,
    NotCharging,
    Error
}

public class ControllerState
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ulong PacketNumber { get; set; }
    public double PollingRateHz { get; set; }

    // Joysticks (Normalizados -1.0 a 1.0 y valor crudo 0 a 255)
    public byte RawLX { get; set; } = 128;
    public byte RawLY { get; set; } = 128;
    public byte RawRX { get; set; } = 128;
    public byte RawRY { get; set; } = 128;

    public double LeftStickX => (RawLX - 128.0) / 128.0;
    public double LeftStickY => (RawLY - 128.0) / 128.0;
    public double RightStickX => (RawRX - 128.0) / 128.0;
    public double RightStickY => (RawRY - 128.0) / 128.0;

    // Gatillos analógicos (0 a 255 y 0.0 a 1.0)
    public byte RawL2 { get; set; }
    public byte RawR2 { get; set; }
    public double L2Normalized => RawL2 / 255.0;
    public double R2Normalized => RawR2 / 255.0;

    // D-Pad
    public bool DpadUp { get; set; }
    public bool DpadDown { get; set; }
    public bool DpadLeft { get; set; }
    public bool DpadRight { get; set; }

    // Botones de acción principales
    public bool Cross { get; set; }      // Cruz / X
    public bool Circle { get; set; }     // Círculo
    public bool Square { get; set; }     // Cuadrado
    public bool Triangle { get; set; }   // Triángulo

    // Botones frontales y clics de gatillo
    public bool L1 { get; set; }
    public bool R1 { get; set; }
    public bool L2Button { get; set; }
    public bool R2Button { get; set; }
    public bool L3 { get; set; }         // Thumbstick click izquierdo
    public bool R3 { get; set; }         // Thumbstick click derecho

    // Botones de sistema
    public bool Share { get; set; }      // Share / Create
    public bool Options { get; set; }
    public bool PsButton { get; set; }
    public bool TouchpadButton { get; set; } // Clic físico en trackpad
    public bool MicMute { get; set; }

    // Sensores de Movimiento (IMU 6-Ejes)
    // Acelerómetro (en Gs y enteros con signo)
    public short RawAccelX { get; set; }
    public short RawAccelY { get; set; }
    public short RawAccelZ { get; set; }

    // Factor típico DualSense/DS4: aprox 8192 LSB/g (±4g)
    public double AccelX_G => RawAccelX / 8192.0;
    public double AccelY_G => RawAccelY / 8192.0;
    public double AccelZ_G => RawAccelZ / 8192.0;
    public double TotalGForce => Math.Sqrt(AccelX_G * AccelX_G + AccelY_G * AccelY_G + AccelZ_G * AccelZ_G);

    // Giroscopio (grados por segundo y enteros con signo)
    public short RawGyroPitch { get; set; }
    public short RawGyroYaw { get; set; }
    public short RawGyroRoll { get; set; }

    // Factor típico DualSense/DS4: aprox 16.4 LSB/(deg/s) (±2000 deg/s)
    public double GyroPitchDegS => RawGyroPitch / 16.4;
    public double GyroYawDegS => RawGyroYaw / 16.4;
    public double GyroRollDegS => RawGyroRoll / 16.4;

    // Estimación de ángulos de inclinación (Calculados por filtro de orientación)
    public double EstimatedPitchDeg { get; set; }
    public double EstimatedRollDeg { get; set; }
    public double EstimatedYawDeg { get; set; }

    // Pantalla Táctil / Trackpad (Multi-touch de 2 dedos)
    public TouchPoint Touch1 { get; set; }
    public TouchPoint Touch2 { get; set; }

    // Batería
    public int BatteryPercentage { get; set; } = 100;
    public BatteryStatus BatteryState { get; set; } = BatteryStatus.Unknown;
    public bool IsCableConnected { get; set; } = true;

    // Raw bytes para el inspector hexadecimal
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();
    public byte ReportId => RawBytes.Length > 0 ? RawBytes[0] : (byte)0;
}
