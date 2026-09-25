using PsCloneTester.Models;

namespace PsCloneTester.Services;

public static class GenericHidProtocol
{
    public static bool ParseInputReport(byte[] report, ControllerState state)
    {
        if (report == null || report.Length < 4) return false;

        state.RawBytes = (byte[])report.Clone();

        int start = (report[0] == 0x00 || report[0] == 0x01) ? 1 : 0;
        int available = report.Length - start;

        // Detectar si los ejes son de 16 bits (típico en mandos de 14+ bytes donde los bytes impares oscilan cerca de 128)
        bool is16Bit = available >= 12 &&
                       Math.Abs(report[start + 1] - 128) < 60 &&
                       Math.Abs(report[start + 3] - 128) < 60;

        if (is16Bit)
        {
            ushort lx = (ushort)(report[start + 0] | (report[start + 1] << 8));
            ushort ly = (ushort)(report[start + 2] | (report[start + 3] << 8));
            ushort rx = (ushort)(report[start + 4] | (report[start + 5] << 8));
            ushort ry = (ushort)(report[start + 6] | (report[start + 7] << 8));

            state.RawLX = (byte)(lx >> 8);
            state.RawLY = (byte)(ly >> 8);
            state.RawRX = (byte)(rx >> 8);
            state.RawRY = (byte)(ry >> 8);

            // Gatillos analógicos (si existen en bytes 8-11)
            if (available >= 12)
            {
                ushort l2 = (ushort)(report[start + 8] | (report[start + 9] << 8));
                ushort r2 = (ushort)(report[start + 10] | (report[start + 11] << 8));
                state.RawL2 = (byte)Math.Clamp(l2 > 255 ? (l2 >> 2) : l2, 0, 255);
                state.RawR2 = (byte)Math.Clamp(r2 > 255 ? (r2 >> 2) : r2, 0, 255);
            }

            int btnOffset = start + 12;
            ParseButtonsAndHat(report, btnOffset, state);
            return true;
        }

        // Modo estándar de 8 bits (DirectInput tradicional)
        if (available >= 4)
        {
            state.RawLX = report[start + 0];
            state.RawLY = report[start + 1];
            state.RawRX = report[start + 2];
            state.RawRY = report[start + 3];
        }

        // Gatillos analógicos o Hat
        if (available >= 6)
        {
            state.RawL2 = report[start + 4];
            state.RawR2 = report[start + 5];
        }

        // Buscar botones y Hat switch en el resto del reporte
        int standardBtnOffset = start + 4;
        if (available >= 8)
        {
            standardBtnOffset = start + 6;
        }
        ParseButtonsAndHat(report, standardBtnOffset, state);

        return true;
    }

    private static void ParseButtonsAndHat(byte[] report, int btnOffset, ControllerState state)
    {
        if (btnOffset >= report.Length) return;

        // Inspeccionar bytes en busca de Hat Switch (0..7 con 8 o 15 = centro)
        for (int i = btnOffset - 2; i < Math.Min(report.Length, btnOffset + 4); i++)
        {
            if (i < 0) continue;
            byte b = report[i];
            byte lowNibble = (byte)(b & 0x0F);
            byte highNibble = (byte)(b >> 4);

            if (lowNibble <= 7 && (b == lowNibble || highNibble == 0 || highNibble == 15))
            {
                ApplyHat(lowNibble, state);
                break;
            }
            if (highNibble <= 7 && lowNibble == 0)
            {
                ApplyHat(highNibble, state);
                break;
            }
        }

        // Botones de acción principales (DirectInput estándar: 1=A/Cross, 2=B/Circle, 3=X/Square, 4=Y/Triangle...)
        if (btnOffset < report.Length)
        {
            byte b1 = report[btnOffset];
            state.Cross = (b1 & 0x01) != 0;
            state.Circle = (b1 & 0x02) != 0;
            state.Square = (b1 & 0x04) != 0;
            state.Triangle = (b1 & 0x08) != 0;
            state.L1 = (b1 & 0x10) != 0;
            state.R1 = (b1 & 0x20) != 0;
            state.L2Button = (b1 & 0x40) != 0 || state.RawL2 > 30;
            state.R2Button = (b1 & 0x80) != 0 || state.RawR2 > 30;
        }

        // Botones de sistema (DirectInput estándar: 9=Select, 10=Start, 11=L3, 12=R3, 13=Home...)
        if (btnOffset + 1 < report.Length)
        {
            byte b2 = report[btnOffset + 1];
            state.Share = (b2 & 0x01) != 0;    // Select / Back
            state.Options = (b2 & 0x02) != 0;  // Start
            state.L3 = (b2 & 0x04) != 0;       // L3
            state.R3 = (b2 & 0x08) != 0;       // R3
            state.PsButton = (b2 & 0x10) != 0; // Home / Guide
        }
    }

    private static void ApplyHat(byte hat, ControllerState state)
    {
        if (hat <= 7)
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
}
