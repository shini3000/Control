using PsCloneTester.Models;

namespace PsCloneTester.Services;

public static class SwitchProProtocol
{
    public static bool ParseInputReport(byte[] report, ControllerState state)
    {
        if (report == null || report.Length < 11) return false;

        state.RawBytes = (byte[])report.Clone();

        // 1. Reporte 0x30 o 0x21 (Modo completo estándar con IMU y batería)
        if (report[0] == 0x30 || report[0] == 0x21)
        {
            return ParseFullReport(report, state);
        }

        // 2. Reporte 0x3F (Modo HID básico por defecto)
        if (report[0] == 0x3F || report.Length >= 12)
        {
            return ParseBasicReport(report, state);
        }

        return false;
    }

    private static bool ParseFullReport(byte[] report, ControllerState state)
    {
        if (report.Length < 12) return false;

        // Batería en byte 2
        byte batByte = report[2];
        int batLevel = (batByte >> 4) & 0x0F;
        state.BatteryPercentage = batLevel switch
        {
            >= 8 => 100,
            >= 6 => 70,
            >= 4 => 45,
            >= 2 => 20,
            _ => 10
        };
        state.IsCableConnected = (batByte & 0x01) != 0;
        state.BatteryState = state.IsCableConnected ? BatteryStatus.Charging : BatteryStatus.Discharging;

        // Botones Byte 3 (Joy-Con Derecho)
        byte bRight = report[3];
        state.Square = (bRight & 0x01) != 0;   // Y en Switch -> Cuadrado
        state.Triangle = (bRight & 0x02) != 0; // X en Switch -> Triángulo
        state.Cross = (bRight & 0x04) != 0;    // B en Switch -> Cruz (Aceptación en Nintendo)
        state.Circle = (bRight & 0x08) != 0;   // A en Switch -> Círculo
        state.R1 = (bRight & 0x40) != 0;       // R
        state.R2Button = (bRight & 0x80) != 0; // ZR
        state.RawR2 = (byte)(state.R2Button ? 255 : 0);

        // Botones Byte 4 (Compartidos)
        byte bShared = report[4];
        state.Share = (bShared & 0x01) != 0;      // Minus (-)
        state.Options = (bShared & 0x02) != 0;    // Plus (+)
        state.R3 = (bShared & 0x04) != 0;         // Clic R3
        state.L3 = (bShared & 0x08) != 0;         // Clic L3
        state.PsButton = (bShared & 0x10) != 0;   // Home
        state.MicMute = (bShared & 0x20) != 0;    // Capture

        // Botones Byte 5 (Joy-Con Izquierdo)
        byte bLeft = report[5];
        state.DpadDown = (bLeft & 0x01) != 0;
        state.DpadUp = (bLeft & 0x02) != 0;
        state.DpadRight = (bLeft & 0x04) != 0;
        state.DpadLeft = (bLeft & 0x08) != 0;
        state.L1 = (bLeft & 0x40) != 0;        // L
        state.L2Button = (bLeft & 0x80) != 0;  // ZL
        state.RawL2 = (byte)(state.L2Button ? 255 : 0);

        // Sticks empaquetados en 12 bits (0..4095, centro aprox 2048)
        ushort lx12 = (ushort)(report[6] | ((report[7] & 0x0F) << 8));
        ushort ly12 = (ushort)((report[7] >> 4) | (report[8] << 4));
        ushort rx12 = (ushort)(report[9] | ((report[10] & 0x0F) << 8));
        ushort ry12 = (ushort)((report[10] >> 4) | (report[11] << 4));

        state.RawLX = (byte)Math.Clamp(lx12 / 16, 0, 255);
        state.RawLY = (byte)Math.Clamp(255 - (ly12 / 16), 0, 255); // Invertir para que arriba sea 0
        state.RawRX = (byte)Math.Clamp(rx12 / 16, 0, 255);
        state.RawRY = (byte)Math.Clamp(255 - (ry12 / 16), 0, 255);

        // IMU (Acelerómetro y Giróscopo en bytes 13 a 24)
        if (report.Length >= 25)
        {
            state.RawAccelX = (short)(report[13] | (report[14] << 8));
            state.RawAccelY = (short)(report[15] | (report[16] << 8));
            state.RawAccelZ = (short)(report[17] | (report[18] << 8));

            state.RawGyroPitch = (short)(report[19] | (report[20] << 8));
            state.RawGyroYaw = (short)(report[21] | (report[22] << 8));
            state.RawGyroRoll = (short)(report[23] | (report[24] << 8));
        }

        return true;
    }

    private static bool ParseBasicReport(byte[] report, ControllerState state)
    {
        int offset = (report[0] == 0x3F) ? 1 : 0;
        if (report.Length < offset + 11) return false;

        byte b1 = report[offset + 0];
        byte b2 = report[offset + 1];

        state.Square = (b1 & 0x01) != 0;   // Y
        state.Triangle = (b1 & 0x02) != 0; // X
        state.Cross = (b1 & 0x04) != 0;    // B
        state.Circle = (b1 & 0x08) != 0;   // A
        state.R1 = (b1 & 0x40) != 0;       // R
        state.R2Button = (b1 & 0x80) != 0; // ZR
        state.RawR2 = (byte)(state.R2Button ? 255 : 0);

        state.Share = (b2 & 0x01) != 0;    // Minus
        state.Options = (b2 & 0x02) != 0;  // Plus
        state.R3 = (b2 & 0x04) != 0;
        state.L3 = (b2 & 0x08) != 0;
        state.PsButton = (b2 & 0x10) != 0; // Home
        state.MicMute = (b2 & 0x20) != 0;  // Capture
        state.L1 = (b2 & 0x40) != 0;       // L
        state.L2Button = (b2 & 0x80) != 0; // ZL
        state.RawL2 = (byte)(state.L2Button ? 255 : 0);

        // Hat switch
        byte hat = report[offset + 2];
        if (hat <= 7)
        {
            state.DpadUp = hat is 0 or 1 or 7;
            state.DpadRight = hat is 1 or 2 or 3;
            state.DpadDown = hat is 3 or 4 or 5;
            state.DpadLeft = hat is 5 or 6 or 7;
        }
        else
        {
            state.DpadUp = state.DpadDown = state.DpadLeft = state.DpadRight = false;
        }

        // Sticks (16 bits)
        if (report.Length >= offset + 11)
        {
            ushort lx = (ushort)(report[offset + 3] | (report[offset + 4] << 8));
            ushort ly = (ushort)(report[offset + 5] | (report[offset + 6] << 8));
            ushort rx = (ushort)(report[offset + 7] | (report[offset + 8] << 8));
            ushort ry = (ushort)(report[offset + 9] | (report[offset + 10] << 8));

            state.RawLX = (byte)(lx >> 8);
            state.RawLY = (byte)(255 - (ly >> 8));
            state.RawRX = (byte)(rx >> 8);
            state.RawRY = (byte)(255 - (ry >> 8));
        }

        return true;
    }
}
