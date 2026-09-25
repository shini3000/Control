using System.Runtime.InteropServices;
using PsCloneTester.Models;

namespace PsCloneTester.Services;

public static class XInputService
{
    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    public const int ERROR_SUCCESS = 0;
    public const int ERROR_DEVICE_NOT_CONNECTED = 1167;

    // Constantes de botones XInput
    public const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    public const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    public const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    public const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    public const ushort XINPUT_GAMEPAD_START = 0x0010;
    public const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    public const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    public const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    public const ushort XINPUT_GAMEPAD_A = 0x1000;
    public const ushort XINPUT_GAMEPAD_B = 0x2000;
    public const ushort XINPUT_GAMEPAD_X = 0x4000;
    public const ushort XINPUT_GAMEPAD_Y = 0x8000;

    public const byte BATTERY_DEVTYPE_GAMEPAD = 0x00;
    public const byte BATTERY_TYPE_DISCONNECTED = 0x00;
    public const byte BATTERY_TYPE_WIRED = 0x01;
    public const byte BATTERY_TYPE_ALKALINE = 0x02;
    public const byte BATTERY_TYPE_NIMH = 0x03;
    public const byte BATTERY_TYPE_UNKNOWN = 0xFF;

    public const byte BATTERY_LEVEL_EMPTY = 0x00;
    public const byte BATTERY_LEVEL_LOW = 0x01;
    public const byte BATTERY_LEVEL_MEDIUM = 0x02;
    public const byte BATTERY_LEVEL_FULL = 0x03;

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState14(int dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState13(int dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState91(int dwUserIndex, out XINPUT_STATE pState);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState14(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState13(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState91(int dwUserIndex, ref XINPUT_VIBRATION pVibration);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern int XInputGetBatteryInformation14(int dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern int XInputGetBatteryInformation13(int dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

    private static int _activeDll = 0; // 0 = not checked, 1 = 1.4, 2 = 1.3, 3 = 9.1.0, -1 = none

    public static List<int> GetConnectedSlots()
    {
        var slots = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            if (TryGetState(i, out _))
            {
                slots.Add(i);
            }
        }
        return slots;
    }

    public static bool TryGetState(int userIndex, out XINPUT_STATE state)
    {
        state = default;
        if (userIndex < 0 || userIndex > 3) return false;

        if (_activeDll == 0 || _activeDll == 1)
        {
            try
            {
                int res = XInputGetState14(userIndex, out state);
                _activeDll = 1;
                return res == ERROR_SUCCESS;
            }
            catch (DllNotFoundException) { _activeDll = 2; }
            catch (EntryPointNotFoundException) { _activeDll = 2; }
        }

        if (_activeDll == 2)
        {
            try
            {
                int res = XInputGetState13(userIndex, out state);
                return res == ERROR_SUCCESS;
            }
            catch (DllNotFoundException) { _activeDll = 3; }
            catch (EntryPointNotFoundException) { _activeDll = 3; }
        }

        if (_activeDll == 3)
        {
            try
            {
                int res = XInputGetState91(userIndex, out state);
                return res == ERROR_SUCCESS;
            }
            catch { _activeDll = -1; }
        }

        return false;
    }

    public static bool TryGetBattery(int userIndex, out byte batteryType, out byte batteryLevel)
    {
        batteryType = BATTERY_TYPE_UNKNOWN;
        batteryLevel = BATTERY_LEVEL_FULL;

        if (userIndex < 0 || userIndex > 3) return false;

        try
        {
            XINPUT_BATTERY_INFORMATION bat;
            int res = -1;
            if (_activeDll == 1)
            {
                res = XInputGetBatteryInformation14(userIndex, BATTERY_DEVTYPE_GAMEPAD, out bat);
            }
            else
            {
                res = XInputGetBatteryInformation13(userIndex, BATTERY_DEVTYPE_GAMEPAD, out bat);
            }

            if (res == ERROR_SUCCESS)
            {
                batteryType = bat.BatteryType;
                batteryLevel = bat.BatteryLevel;
                return true;
            }
        }
        catch
        {
            // La función de batería no está disponible en xinput9_1_0
        }

        return false;
    }

    public static bool SetVibration(byte leftRumble, byte rightRumble, int specificUserIndex = -1)
    {
        var vib = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)(leftRumble * 257),
            wRightMotorSpeed = (ushort)(rightRumble * 257)
        };

        if (specificUserIndex >= 0 && specificUserIndex < 4)
        {
            return TrySetState(specificUserIndex, ref vib) == ERROR_SUCCESS;
        }

        bool anySuccess = false;
        for (int i = 0; i < 4; i++)
        {
            try
            {
                if (TrySetState(i, ref vib) == ERROR_SUCCESS)
                {
                    anySuccess = true;
                }
            }
            catch
            {
                // Ignorar si el slot no está disponible
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
            catch (DllNotFoundException) { _activeDll = 2; }
            catch (EntryPointNotFoundException) { _activeDll = 2; }
        }

        if (_activeDll == 2)
        {
            try
            {
                int res = XInputSetState13(userIndex, ref vib);
                return res;
            }
            catch (DllNotFoundException) { _activeDll = 3; }
            catch (EntryPointNotFoundException) { _activeDll = 3; }
        }

        if (_activeDll == 3)
        {
            try
            {
                return XInputSetState91(userIndex, ref vib);
            }
            catch { _activeDll = -1; }
        }

        return -1;
    }

    public static void ParseXInputState(ref XINPUT_STATE xstate, ControllerState state, int userIndex = -1)
    {
        // 1. Convertir Joysticks analógicos (-32768 a 32767) al sistema 0..255 (Centro = 128)
        // Y se invierte matemáticamente para corresponder al estándar de pantalla donde arriba es valor bajo
        state.RawLX = (byte)Math.Clamp(128 + (xstate.Gamepad.sThumbLX / 256), 0, 255);
        state.RawLY = (byte)Math.Clamp(128 - (xstate.Gamepad.sThumbLY / 256), 0, 255);
        state.RawRX = (byte)Math.Clamp(128 + (xstate.Gamepad.sThumbRX / 256), 0, 255);
        state.RawRY = (byte)Math.Clamp(128 - (xstate.Gamepad.sThumbRY / 256), 0, 255);

        // 2. Gatillos analógicos (0 a 255)
        state.RawL2 = xstate.Gamepad.bLeftTrigger;
        state.RawR2 = xstate.Gamepad.bRightTrigger;

        // 3. Botones digitales
        ushort btns = xstate.Gamepad.wButtons;

        state.DpadUp = (btns & XINPUT_GAMEPAD_DPAD_UP) != 0;
        state.DpadDown = (btns & XINPUT_GAMEPAD_DPAD_DOWN) != 0;
        state.DpadLeft = (btns & XINPUT_GAMEPAD_DPAD_LEFT) != 0;
        state.DpadRight = (btns & XINPUT_GAMEPAD_DPAD_RIGHT) != 0;

        // Equivalencias principales:
        // A -> Cruz (✕), B -> Círculo (○), X -> Cuadrado (□), Y -> Triángulo (△)
        state.Cross = (btns & XINPUT_GAMEPAD_A) != 0;
        state.Circle = (btns & XINPUT_GAMEPAD_B) != 0;
        state.Square = (btns & XINPUT_GAMEPAD_X) != 0;
        state.Triangle = (btns & XINPUT_GAMEPAD_Y) != 0;

        // Bumpers y Gatillos
        state.L1 = (btns & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0;
        state.R1 = (btns & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0;
        state.L2Button = state.RawL2 > 30;
        state.R2Button = state.RawR2 > 30;

        // Clics de Joystick
        state.L3 = (btns & XINPUT_GAMEPAD_LEFT_THUMB) != 0;
        state.R3 = (btns & XINPUT_GAMEPAD_RIGHT_THUMB) != 0;

        // Sistema: Back / View -> Share, Start / Menu -> Options
        state.Share = (btns & XINPUT_GAMEPAD_BACK) != 0;
        state.Options = (btns & XINPUT_GAMEPAD_START) != 0;

        // Batería si se consulta periódicamente
        if (userIndex >= 0)
        {
            if (TryGetBattery(userIndex, out byte batType, out byte batLevel))
            {
                state.IsCableConnected = (batType == BATTERY_TYPE_WIRED);
                state.BatteryPercentage = batLevel switch
                {
                    BATTERY_LEVEL_EMPTY => 10,
                    BATTERY_LEVEL_LOW => 35,
                    BATTERY_LEVEL_MEDIUM => 70,
                    BATTERY_LEVEL_FULL => 100,
                    _ => 100
                };
                state.BatteryState = state.IsCableConnected ? BatteryStatus.Charging : BatteryStatus.Discharging;
            }
        }

        // Generar volcado binario simulado de 16 bytes para el inspector hexadecimal
        byte[] raw = new byte[16];
        raw[0] = (byte)(btns & 0xFF);
        raw[1] = (byte)((btns >> 8) & 0xFF);
        raw[2] = state.RawL2;
        raw[3] = state.RawR2;
        raw[4] = (byte)(xstate.Gamepad.sThumbLX & 0xFF);
        raw[5] = (byte)((xstate.Gamepad.sThumbLX >> 8) & 0xFF);
        raw[6] = (byte)(xstate.Gamepad.sThumbLY & 0xFF);
        raw[7] = (byte)((xstate.Gamepad.sThumbLY >> 8) & 0xFF);
        raw[8] = (byte)(xstate.Gamepad.sThumbRX & 0xFF);
        raw[9] = (byte)((xstate.Gamepad.sThumbRX >> 8) & 0xFF);
        raw[10] = (byte)(xstate.Gamepad.sThumbRY & 0xFF);
        raw[11] = (byte)((xstate.Gamepad.sThumbRY >> 8) & 0xFF);
        raw[12] = (byte)(xstate.dwPacketNumber & 0xFF);
        raw[13] = (byte)((xstate.dwPacketNumber >> 8) & 0xFF);
        raw[14] = (byte)((xstate.dwPacketNumber >> 16) & 0xFF);
        raw[15] = (byte)((xstate.dwPacketNumber >> 24) & 0xFF);
        state.RawBytes = raw;
    }
}
