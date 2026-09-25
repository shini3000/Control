using PsCloneTester.Models;

namespace PsCloneTester.Services;

public static class XboxBluetoothProtocol
{
    public static bool ParseInputReport(byte[] report, ControllerState state)
    {
        if (report == null || report.Length < 14) return false;

        state.RawBytes = (byte[])report.Clone();

        int offset = (report[0] == 0x01) ? 1 : 0;
        if (report.Length < offset + 14) return false;

        // Joysticks analógicos (16 bits Little Endian 0..65535, centro aprox 32768)
        ushort lx = (ushort)(report[offset + 0] | (report[offset + 1] << 8));
        ushort ly = (ushort)(report[offset + 2] | (report[offset + 3] << 8));
        ushort rx = (ushort)(report[offset + 4] | (report[offset + 5] << 8));
        ushort ry = (ushort)(report[offset + 6] | (report[offset + 7] << 8));

        state.RawLX = (byte)(lx >> 8);
        state.RawLY = (byte)(ly >> 8);
        state.RawRX = (byte)(rx >> 8);
        state.RawRY = (byte)(ry >> 8);

        // Gatillos analógicos (10 bits 0..1023 o 16 bits)
        if (report.Length >= offset + 12)
        {
            ushort lt = (ushort)(report[offset + 8] | (report[offset + 9] << 8));
            ushort rt = (ushort)(report[offset + 10] | (report[offset + 11] << 8));

            state.RawL2 = (byte)Math.Clamp(lt > 255 ? (lt >> 2) : lt, 0, 255);
            state.RawR2 = (byte)Math.Clamp(rt > 255 ? (rt >> 2) : rt, 0, 255);
            state.L2Button = state.RawL2 > 30;
            state.R2Button = state.RawR2 > 30;
        }

        // Cruceta D-Pad (Hat switch)
        if (report.Length > offset + 12)
        {
            byte hat = report[offset + 12];
            if (hat >= 1 && hat <= 8) // Formato estándar Bluetooth Xbox (1=Arriba, 5=Abajo, 0=Neutro)
            {
                state.DpadUp = hat is 1 or 2 or 8;
                state.DpadRight = hat is 2 or 3 or 4;
                state.DpadDown = hat is 4 or 5 or 6;
                state.DpadLeft = hat is 6 or 7 or 8;
            }
            else if (hat <= 7) // Formato base 0
            {
                state.DpadUp = hat is 0 or 1 or 7;
                state.DpadRight = hat is 1 or 2 or 3;
                state.DpadDown = hat is 3 or 4 or 5;
                state.DpadLeft = hat is 5 or 6 or 7;
            }
            else
            {
                state.DpadUp = false;
                state.DpadDown = false;
                state.DpadLeft = false;
                state.DpadRight = false;
            }
        }

        // Botones de acción principales y bumpers
        if (report.Length > offset + 13)
        {
            byte b1 = report[offset + 13];
            state.Cross = (b1 & 0x01) != 0;    // A
            state.Circle = (b1 & 0x02) != 0;   // B
            state.Square = (b1 & 0x08) != 0;   // X
            state.Triangle = (b1 & 0x10) != 0; // Y
            state.L1 = (b1 & 0x40) != 0;       // LB
            state.R1 = (b1 & 0x80) != 0;       // RB
        }

        // Botones de sistema y clics de joystick
        if (report.Length > offset + 14)
        {
            byte b2 = report[offset + 14];
            state.Share = (b2 & 0x04) != 0;    // View / Back
            state.Options = (b2 & 0x08) != 0;  // Menu / Start
            state.PsButton = (b2 & 0x10) != 0; // Xbox Guide
            state.L3 = (b2 & 0x20) != 0;       // LSB
            state.R3 = (b2 & 0x40) != 0;       // RSB
        }

        // Botón Share dedicado en Xbox Series X|S
        if (report.Length > offset + 15)
        {
            byte b3 = report[offset + 15];
            if ((b3 & 0x01) != 0)
            {
                state.MicMute = true;
            }
        }

        return true;
    }
}
