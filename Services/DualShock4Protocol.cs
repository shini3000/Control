using PsCloneTester.Models;

namespace PsCloneTester.Services;

public static class DualShock4Protocol
{
    public static bool ParseInputReport(byte[] report, ControllerState state, bool isBluetooth)
    {
        if (report == null || report.Length < 10) return false;

        state.RawBytes = (byte[])report.Clone();
        int offset = 0;

        if (isBluetooth && (report[0] == 0x11 || report[0] == 0x31))
        {
            offset = 2; // DS4 Bluetooth header
        }
        else if (report[0] == 0x01)
        {
            offset = 1;
        }

        if (report.Length < offset + 30) return false;

        // Sticks
        state.RawLX = report[offset + 0];
        state.RawLY = report[offset + 1];
        state.RawRX = report[offset + 2];
        state.RawRY = report[offset + 3];

        // Botones y D-Pad
        byte btn1 = report[offset + 4];
        byte dpad = (byte)(btn1 & 0x0F);
        state.DpadUp = dpad is 0 or 1 or 7;
        state.DpadRight = dpad is 1 or 2 or 3;
        state.DpadDown = dpad is 3 or 4 or 5;
        state.DpadLeft = dpad is 5 or 6 or 7;

        state.Square = (btn1 & 0x10) != 0;
        state.Cross = (btn1 & 0x20) != 0;
        state.Circle = (btn1 & 0x40) != 0;
        state.Triangle = (btn1 & 0x80) != 0;

        byte btn2 = report[offset + 5];
        state.L1 = (btn2 & 0x01) != 0;
        state.R1 = (btn2 & 0x02) != 0;
        state.L2Button = (btn2 & 0x04) != 0;
        state.R2Button = (btn2 & 0x08) != 0;
        state.Share = (btn2 & 0x10) != 0;
        state.Options = (btn2 & 0x20) != 0;
        state.L3 = (btn2 & 0x40) != 0;
        state.R3 = (btn2 & 0x80) != 0;

        byte btn3 = report[offset + 6];
        state.PsButton = (btn3 & 0x01) != 0;
        state.TouchpadButton = (btn3 & 0x02) != 0;

        // Gatillos analógicos L2 y R2
        state.RawL2 = report[offset + 7];
        state.RawR2 = report[offset + 8];

        // Giroscopio y Acelerómetro
        int imuOffset = offset + 12;
        if (report.Length >= imuOffset + 12)
        {
            state.RawGyroPitch = (short)(report[imuOffset] | (report[imuOffset + 1] << 8));
            state.RawGyroYaw = (short)(report[imuOffset + 2] | (report[imuOffset + 3] << 8));
            state.RawGyroRoll = (short)(report[imuOffset + 4] | (report[imuOffset + 5] << 8));

            state.RawAccelX = (short)(report[imuOffset + 6] | (report[imuOffset + 7] << 8));
            state.RawAccelY = (short)(report[imuOffset + 8] | (report[imuOffset + 9] << 8));
            state.RawAccelZ = (short)(report[imuOffset + 10] | (report[imuOffset + 11] << 8));
        }

        // Batería
        int batOffset = offset + 29;
        if (report.Length > batOffset)
        {
            byte bat = report[batOffset];
            bool cable = (bat & 0x10) != 0;
            int level = (bat & 0x0F) * 10;
            state.BatteryPercentage = Math.Clamp(level, 0, 100);
            state.IsCableConnected = cable;
            state.BatteryState = cable ? BatteryStatus.Charging : BatteryStatus.Discharging;
        }

        // Pantalla Táctil
        int touchOffset = offset + 34;
        if (report.Length >= touchOffset + 8)
        {
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

        return true;
    }

    public static byte[] BuildOutputReport(
        bool isBluetooth,
        byte leftMotor,
        byte rightMotor,
        byte r, byte g, byte b)
    {
        if (isBluetooth)
        {
            byte[] report = new byte[78];
            report[0] = 0x11;
            report[1] = 0x80;
            report[3] = 0x0F;
            report[6] = rightMotor;
            report[7] = leftMotor;
            report[8] = r;
            report[9] = g;
            report[10] = b;

            uint crc = Crc32Helper.ComputePsBluetoothCrc(report, 74);
            report[74] = (byte)(crc & 0xFF);
            report[75] = (byte)((crc >> 8) & 0xFF);
            report[76] = (byte)((crc >> 16) & 0xFF);
            report[77] = (byte)((crc >> 24) & 0xFF);
            return report;
        }
        else
        {
            byte[] report = new byte[32];
            report[0] = 0x05;
            report[1] = 0xFF;
            report[2] = 0x04;
            report[4] = rightMotor;
            report[5] = leftMotor;
            report[6] = r;
            report[7] = g;
            report[8] = b;
            return report;
        }
    }
}
