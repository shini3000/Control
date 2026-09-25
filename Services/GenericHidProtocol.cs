using PsCloneTester.Models;

namespace PsCloneTester.Services;

public static class GenericHidProtocol
{
    public static bool ParseInputReport(byte[] report, ControllerState state)
    {
        if (report == null || report.Length < 6) return false;

        state.RawBytes = (byte[])report.Clone();

        int start = (report[0] == 0x00 || report[0] == 0x01) ? 1 : 0;
        if (report.Length < start + 6) return false;

        state.RawLX = report[start + 0];
        state.RawLY = report[start + 1];
        state.RawRX = report[start + 2];
        state.RawRY = report[start + 3];

        if (report.Length > start + 5)
        {
            state.RawL2 = report[start + 4];
            state.RawR2 = report[start + 5];
        }

        // Leer botones
        if (report.Length > start + 6)
        {
            byte b1 = report[start + 6];
            state.Cross = (b1 & 0x01) != 0;
            state.Circle = (b1 & 0x02) != 0;
            state.Square = (b1 & 0x04) != 0;
            state.Triangle = (b1 & 0x08) != 0;
            state.L1 = (b1 & 0x10) != 0;
            state.R1 = (b1 & 0x20) != 0;
            state.L2Button = (b1 & 0x40) != 0;
            state.R2Button = (b1 & 0x80) != 0;
        }

        if (report.Length > start + 7)
        {
            byte b2 = report[start + 7];
            state.Share = (b2 & 0x01) != 0;
            state.Options = (b2 & 0x02) != 0;
            state.L3 = (b2 & 0x04) != 0;
            state.R3 = (b2 & 0x08) != 0;
            state.PsButton = (b2 & 0x10) != 0;
        }

        return true;
    }
}
