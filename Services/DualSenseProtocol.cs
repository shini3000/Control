using PsCloneTester.Models;

namespace PsCloneTester.Services;

public static class DualSenseProtocol
{
    public static bool ParseInputReport(byte[] report, ControllerState state, bool isBluetooth)
    {
        if (report == null || report.Length < 10) return false;

        state.RawBytes = (byte[])report.Clone();
        int offset = 0;

        // Si es Bluetooth con reporte completo 0x31, los datos del mando empiezan tras el encabezado
        if (isBluetooth && report[0] == 0x31)
        {
            offset = 1; // Salta el 0x31 (en algunos firmwares hay 1 o 2 bytes de flag)
            if (report.Length > 2 && (report[1] & 0x01) == 0)
            {
                offset = 2;
            }
        }
        else if (report[0] == 0x01)
        {
            offset = 1; // Salta el Report ID 0x01 habitual en USB
        }

        if (report.Length < offset + 40) return false;

        // Sticks
        state.RawLX = report[offset + 0];
        state.RawLY = report[offset + 1];
        state.RawRX = report[offset + 2];
        state.RawRY = report[offset + 3];

        // Gatillos Analógicos
        state.RawL2 = report[offset + 4];
        state.RawR2 = report[offset + 5];

        // Botones de acción y D-Pad (byte offset + 7)
        byte btn1 = report[offset + 7];
        byte dpad = (byte)(btn1 & 0x0F);
        state.DpadUp = dpad is 0 or 1 or 7;
        state.DpadRight = dpad is 1 or 2 or 3;
        state.DpadDown = dpad is 3 or 4 or 5;
        state.DpadLeft = dpad is 5 or 6 or 7;

        state.Square = (btn1 & 0x10) != 0;
        state.Cross = (btn1 & 0x20) != 0;
        state.Circle = (btn1 & 0x40) != 0;
        state.Triangle = (btn1 & 0x80) != 0;

        // Bumpers y modificadores (byte offset + 8)
        byte btn2 = report[offset + 8];
        state.L1 = (btn2 & 0x01) != 0;
        state.R1 = (btn2 & 0x02) != 0;
        state.L2Button = (btn2 & 0x04) != 0;
        state.R2Button = (btn2 & 0x08) != 0;
        state.Share = (btn2 & 0x10) != 0;
        state.Options = (btn2 & 0x20) != 0;
        state.L3 = (btn2 & 0x40) != 0;
        state.R3 = (btn2 & 0x80) != 0;

        // Botones de sistema (byte offset + 9)
        byte btn3 = report[offset + 9];
        state.PsButton = (btn3 & 0x01) != 0;
        state.TouchpadButton = (btn3 & 0x02) != 0;
        state.MicMute = (btn3 & 0x04) != 0;

        // Acelerómetro (16-bit con signo)
        int accOffset = offset + 15;
        if (report.Length >= accOffset + 12)
        {
            state.RawAccelX = (short)(report[accOffset] | (report[accOffset + 1] << 8));
            state.RawAccelY = (short)(report[accOffset + 2] | (report[accOffset + 3] << 8));
            state.RawAccelZ = (short)(report[accOffset + 4] | (report[accOffset + 5] << 8));

            // Giroscopio (Pitch, Yaw, Roll)
            state.RawGyroPitch = (short)(report[accOffset + 6] | (report[accOffset + 7] << 8));
            state.RawGyroYaw = (short)(report[accOffset + 8] | (report[accOffset + 9] << 8));
            state.RawGyroRoll = (short)(report[accOffset + 10] | (report[accOffset + 11] << 8));
        }

        // Pantalla Táctil (Trackpad Multi-Touch)
        int touchOffset = offset + 32;
        if (report.Length >= touchOffset + 8)
        {
            // Touch 0
            byte t0Header = report[touchOffset];
            bool t0Active = (t0Header & 0x80) == 0;
            byte t0Id = (byte)(t0Header & 0x7F);
            ushort t0X = (ushort)(report[touchOffset + 1] | ((report[touchOffset + 2] & 0x0F) << 8));
            ushort t0Y = (ushort)((report[touchOffset + 3] << 4) | ((report[touchOffset + 2] & 0xF0) >> 4));

            state.Touch1 = new TouchPoint
            {
                IsActive = t0Active,
                Id = t0Id,
                RawX = t0X,
                RawY = t0Y
            };

            // Touch 1
            byte t1Header = report[touchOffset + 4];
            bool t1Active = (t1Header & 0x80) == 0;
            byte t1Id = (byte)(t1Header & 0x7F);
            ushort t1X = (ushort)(report[touchOffset + 5] | ((report[touchOffset + 6] & 0x0F) << 8));
            ushort t1Y = (ushort)((report[touchOffset + 7] << 4) | ((report[touchOffset + 6] & 0xF0) >> 4));

            state.Touch2 = new TouchPoint
            {
                IsActive = t1Active,
                Id = t1Id,
                RawX = t1X,
                RawY = t1Y
            };
        }

        // Batería
        int batOffset = offset + 52;
        if (report.Length > batOffset)
        {
            byte bat = report[batOffset];
            byte stateNibble = (byte)((bat & 0xF0) >> 4);
            state.BatteryState = stateNibble switch
            {
                0x0 => BatteryStatus.Discharging,
                0x1 => BatteryStatus.Charging,
                0x2 => BatteryStatus.Full,
                0xB => BatteryStatus.NotCharging,
                _ => BatteryStatus.Unknown
            };
            state.BatteryPercentage = Math.Min((bat & 0x0F) * 10 + 5, 100);
            state.IsCableConnected = !isBluetooth || state.BatteryState == BatteryStatus.Charging;
        }

        return true;
    }

    public static byte[] BuildOutputReport(
        bool isBluetooth,
        TriggerConfig leftTrigger,
        TriggerConfig rightTrigger,
        byte leftMotor,
        byte rightMotor,
        byte r, byte g, byte b,
        byte playerLeds = 0x00)
    {
        if (isBluetooth)
        {
            byte[] report = new byte[78];
            report[0] = 0x31; // Bluetooth Report ID
            report[1] = 0x02; // Flags

            // Valid flags
            report[2] = 0xFF; // Motors, Triggers, Volume
            report[3] = 0x1 | 0x2 | 0x4 | 0x10 | 0x40; // LEDs, Lightbar

            report[4] = rightMotor; // Motor derecho (ligero / alta frecuencia)
            report[5] = leftMotor;  // Motor izquierdo (pesado / baja frecuencia)

            // Gatillo Derecho (R2)
            report[12] = GetHardwareModeByte(rightTrigger.Mode);
            byte[] rForces = rightTrigger.GetForcesPayload();
            report[13] = rForces[0];
            report[14] = rForces[1];
            report[15] = rForces[2];
            report[16] = rForces[3];
            report[17] = rForces[4];
            report[18] = rForces[5];
            report[21] = rForces[6];

            // Gatillo Izquierdo (L2)
            report[23] = GetHardwareModeByte(leftTrigger.Mode);
            byte[] lForces = leftTrigger.GetForcesPayload();
            report[24] = lForces[0];
            report[25] = lForces[1];
            report[26] = lForces[2];
            report[27] = lForces[3];
            report[28] = lForces[4];
            report[29] = lForces[5];
            report[32] = lForces[6];

            // Barra de luz LED y Player LEDs (valid_flag2: 0x02 lightbar + 0x04 compatible vibration 2)
            report[40] = 0x02 | 0x04; // Uninterruptible LED + Compatible Vibration 2
            report[44] = 0x00; // Brightness
            report[45] = playerLeds;
            report[46] = r;
            report[47] = g;
            report[48] = b;

            // Calcular y adjuntar CRC-32
            uint crc = Crc32Helper.ComputePsBluetoothCrc(report, 74);
            report[74] = (byte)(crc & 0xFF);
            report[75] = (byte)((crc >> 8) & 0xFF);
            report[76] = (byte)((crc >> 16) & 0xFF);
            report[77] = (byte)((crc >> 24) & 0xFF);

            return report;
        }
        else
        {
            // USB Output Report (0x02) - 63 o 64 bytes
            byte[] report = new byte[64];
            report[0] = 0x02; // USB Output Report ID
            report[1] = 0xFF; // Valid flags 1 (Motors & Triggers: 0x01 rumble + 0x02 haptics + triggers)
            report[2] = 0x1 | 0x2 | 0x4 | 0x10 | 0x40; // Valid flags 2 (LEDs & Lightbar)

            report[3] = rightMotor; // Motor derecho (ligero / alta frecuencia)
            report[4] = leftMotor;  // Motor izquierdo (pesado / baja frecuencia)

            // Gatillo Derecho (R2)
            report[11] = GetHardwareModeByte(rightTrigger.Mode);
            byte[] rForces = rightTrigger.GetForcesPayload();
            report[12] = rForces[0];
            report[13] = rForces[1];
            report[14] = rForces[2];
            report[15] = rForces[3];
            report[16] = rForces[4];
            report[17] = rForces[5];
            report[20] = rForces[6];

            // Gatillo Izquierdo (L2)
            report[22] = GetHardwareModeByte(leftTrigger.Mode);
            byte[] lForces = leftTrigger.GetForcesPayload();
            report[23] = lForces[0];
            report[24] = lForces[1];
            report[25] = lForces[2];
            report[26] = lForces[3];
            report[27] = lForces[4];
            report[28] = lForces[5];
            report[31] = lForces[6];

            // Barra de luz LED y valid_flag2 (0x02 Lightbar + 0x04 Compatible Vibration 2)
            report[39] = 0x02 | 0x04; // Uninterruptible LED + Compatible Vibration 2
            report[43] = 0x00; // Brightness
            report[44] = playerLeds;
            report[45] = r;
            report[46] = g;
            report[47] = b;

            return report;
        }
    }

    private static byte GetHardwareModeByte(TriggerEffectMode mode)
    {
        return mode switch
        {
            TriggerEffectMode.HardStop => 0x01,
            TriggerEffectMode.Galloping => 0x06,
            _ => (byte)mode
        };
    }
}
