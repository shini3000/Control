using System.Runtime.InteropServices;

namespace PsCloneTester.Services;

public static class XInputService
{
    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState14(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState13(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState91(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    private static int _activeDll = 0; // 0 = not checked, 1 = 1.4, 2 = 1.3, 3 = 9.1.0, -1 = none

    public static bool SetVibration(byte leftRumble, byte rightRumble)
    {
        var vib = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)(leftRumble * 257),
            wRightMotorSpeed = (ushort)(rightRumble * 257)
        };

        bool anySuccess = false;
        for (int i = 0; i < 4; i++)
        {
            try
            {
                if (TrySetState(i, ref vib) == 0) // ERROR_SUCCESS = 0
                {
                    anySuccess = true;
                }
            }
            catch
            {
                // Ignorar si XInput no está disponible
            }
        }

        return anySuccess;
    }

    private static int TrySetState(int userIndex, ref XINPUT_VIBRATION vib)
    {
        if (_activeDll == 0 || _activeDll == 1)
        {
            try
            {
                int res = XInputSetState14(userIndex, ref vib);
                _activeDll = 1;
                return res;
            }
            catch (DllNotFoundException)
            {
                _activeDll = 2;
            }
            catch (EntryPointNotFoundException)
            {
                _activeDll = 2;
            }
        }

        if (_activeDll == 2)
        {
            try
            {
                int res = XInputSetState13(userIndex, ref vib);
                return res;
            }
            catch (DllNotFoundException)
            {
                _activeDll = 3;
            }
            catch (EntryPointNotFoundException)
            {
                _activeDll = 3;
            }
        }

        if (_activeDll == 3)
        {
            try
            {
                return XInputSetState91(userIndex, ref vib);
            }
            catch
            {
                _activeDll = -1;
            }
        }

        return -1;
    }
}
