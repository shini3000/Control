using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using PsCloneTester.Models;
using PsCloneTester.Native;

namespace PsCloneTester.Services;

public class HidDeviceService : IDisposable
{
    private SafeFileHandle? _deviceHandle;
    private SafeFileHandle? _writeHandle;
    private Thread? _readThread;
    private CancellationTokenSource? _cts;
    private readonly object _writeLock = new();

    public bool IsConnected => _deviceHandle != null && !_deviceHandle.IsInvalid && !_deviceHandle.IsClosed;
    public DeviceInfo? CurrentDevice { get; private set; }

    public event Action<byte[]>? InputReportReceived;
    public event Action<string>? LogMessage;
    public event Action? DeviceDisconnected;

    public List<DeviceInfo> EnumerateDevices(bool includeAllHid = false)
    {
        var list = new List<DeviceInfo>();

        HidNative.HidD_GetHidGuid(out Guid hidGuid);
        IntPtr deviceInfoSet = HidNative.SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            HidNative.DIGCF_PRESENT | HidNative.DIGCF_DEVICEINTERFACE);

        if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == (IntPtr)(-1))
        {
            LogMessage?.Invoke("No se pudo obtener la lista de dispositivos HID de Windows.");
            return list;
        }

        try
        {
            var interfaceData = new HidNative.SP_DEVICE_INTERFACE_DATA();
            interfaceData.cbSize = (uint)Marshal.SizeOf<HidNative.SP_DEVICE_INTERFACE_DATA>();

            uint index = 0;
            while (HidNative.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index++, ref interfaceData))
            {
                uint requiredSize = 0;
                HidNative.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);

                if (requiredSize == 0) continue;

                IntPtr detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    Marshal.WriteInt32(detailDataBuffer, IntPtr.Size == 8 ? 8 : 5);

                    if (HidNative.SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref interfaceData, detailDataBuffer, requiredSize, ref requiredSize, IntPtr.Zero))
                    {
                        IntPtr pDevicePath = (IntPtr)((long)detailDataBuffer + 4);
                        string? devicePath = Marshal.PtrToStringAuto(pDevicePath);

                        if (!string.IsNullOrEmpty(devicePath))
                        {
                            var info = QueryDeviceInfo(devicePath, includeAllHid);
                            if (info != null)
                            {
                                list.Add(info);
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailDataBuffer);
                }
            }
        }
        finally
        {
            HidNative.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        // Ordenar priorizando mandos Sony o clones reconocidos
        return list.OrderByDescending(d => d.DetectedType != ControllerType.GenericHid && d.DetectedType != ControllerType.Unknown)
                   .ThenBy(d => d.DisplayTitle)
                   .ToList();
    }

    private DeviceInfo? QueryDeviceInfo(string devicePath, bool includeAllHid)
    {
        using SafeFileHandle handle = HidNative.CreateFile(
            devicePath,
            0,
            HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
            IntPtr.Zero,
            HidNative.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid) return null;

        var attrs = new HidNative.HIDD_ATTRIBUTES();
        attrs.Size = (uint)Marshal.SizeOf<HidNative.HIDD_ATTRIBUTES>();
        if (!HidNative.HidD_GetAttributes(handle, ref attrs)) return null;

        var devInfo = new DeviceInfo
        {
            DevicePath = devicePath,
            VendorId = attrs.VendorID,
            ProductId = attrs.ProductID,
            VersionNumber = attrs.VersionNumber
        };

        byte[] strBuf = new byte[256];
        if (HidNative.HidD_GetProductString(handle, strBuf, (uint)strBuf.Length))
        {
            devInfo.ProductName = Encoding.Unicode.GetString(strBuf).TrimEnd('\0');
        }

        if (HidNative.HidD_GetManufacturerString(handle, strBuf, (uint)strBuf.Length))
        {
            devInfo.Manufacturer = Encoding.Unicode.GetString(strBuf).TrimEnd('\0');
        }

        if (HidNative.HidD_GetSerialNumberString(handle, strBuf, (uint)strBuf.Length))
        {
            devInfo.SerialNumber = Encoding.Unicode.GetString(strBuf).TrimEnd('\0');
        }

        if (HidNative.HidD_GetPreparsedData(handle, out IntPtr preparsedData))
        {
            try
            {
                if (HidNative.HidP_GetCaps(preparsedData, out HidNative.HIDP_CAPS caps) >= 0)
                {
                    devInfo.InputReportLength = caps.InputReportByteLength;
                    devInfo.OutputReportLength = caps.OutputReportByteLength;
                    devInfo.UsagePage = caps.UsagePage;
                    devInfo.Usage = caps.Usage;
                }
            }
            finally
            {
                HidNative.HidD_FreePreparsedData(preparsedData);
            }
        }

        string lowerPath = devicePath.ToLowerInvariant();
        if (lowerPath.Contains("bth") || lowerPath.Contains("bluetooth") || lowerPath.Contains("{e0cbf06c"))
        {
            devInfo.Connection = ConnectionType.Bluetooth;
        }
        else
        {
            devInfo.Connection = ConnectionType.USB;
        }

        devInfo.DetectedType = ClassifyController(devInfo);

        // Filtrar teclados y ratones obvios salvo que se solicite includeAllHid o sea Sony
        bool isKeyboardOrMouse = (devInfo.UsagePage == 0x01 && (devInfo.Usage == 0x06 || devInfo.Usage == 0x02)) ||
                                 devInfo.ProductName.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) ||
                                 devInfo.ProductName.Contains("Teclado", StringComparison.OrdinalIgnoreCase) ||
                                 devInfo.ProductName.Contains("Mouse", StringComparison.OrdinalIgnoreCase) ||
                                 lowerPath.EndsWith("\\kbd");

        if (!includeAllHid && isKeyboardOrMouse && devInfo.VendorId != 0x054C)
        {
            return null;
        }

        bool isLikelyGamepad = devInfo.DetectedType != ControllerType.Unknown || includeAllHid;
        return isLikelyGamepad ? devInfo : null;
    }

    private ControllerType ClassifyController(DeviceInfo dev)
    {
        // Sony oficial
        if (dev.VendorId == 0x054C)
        {
            return dev.ProductId switch
            {
                0x0CE6 => ControllerType.DualSense,
                0x0DF2 => ControllerType.DualSenseEdge,
                0x05C4 => ControllerType.DualShock4V1,
                0x09CC => ControllerType.DualShock4V2,
                0x0BA0 => ControllerType.DualShock4V1,
                _ => ControllerType.ClonePs5
            };
        }

        // Microsoft Xbox oficial
        if (dev.VendorId == 0x045E)
        {
            return dev.ProductId switch
            {
                0x028E or 0x028F => ControllerType.Xbox360,
                0x02D1 or 0x02DD or 0x02E3 or 0x02EA or 0x0B00 => ControllerType.XboxOne,
                0x0B12 or 0x0B13 or 0x0B20 or 0x0B22 => ControllerType.XboxSeries,
                _ => ControllerType.XboxOne
            };
        }

        // Nintendo oficial
        if (dev.VendorId == 0x057E)
        {
            return dev.ProductId switch
            {
                0x2009 => ControllerType.SwitchPro,
                0x2006 or 0x2007 => ControllerType.SwitchJoyCon,
                _ => ControllerType.SwitchPro
            };
        }

        // Fabricantes reconocidos de mandos (Razer, Logitech, 8BitDo, Thrustmaster, PowerA, PDP/Victrix, Nacon, Scuf, SteelSeries, GameSir, Flydigi)
        if (dev.VendorId is 0x1532 or 0x046D or 0x2DC8 or 0x044F or 0x24C6 or 0x20D6 or 0x0E6F or 0x146B or 0x2E95 or 0x1038 or 0x3537 or 0x2F24 or 0x0738 or 0x0079)
        {
            return ControllerType.GenericHid;
        }

        string prod = dev.ProductName.ToLowerInvariant();
        if (prod.Contains("xbox"))
        {
            return prod.Contains("series") ? ControllerType.XboxSeries : ControllerType.XboxOne;
        }

        if (prod.Contains("switch") || prod.Contains("pro controller"))
        {
            return ControllerType.SwitchPro;
        }

        if (prod.Contains("joy-con"))
        {
            return ControllerType.SwitchJoyCon;
        }

        if (prod.Contains("dualsense") || prod.Contains("ps5"))
        {
            return ControllerType.ClonePs5;
        }

        if (prod.Contains("dualshock") || prod.Contains("ps4") || (prod.Contains("wireless controller") && dev.VendorId != 0x045E))
        {
            return ControllerType.ClonePs4;
        }

        if (prod.Contains("razer") || prod.Contains("wolverine") || prod.Contains("raiju") ||
            prod.Contains("gamesir") || prod.Contains("flydigi") || prod.Contains("8bitdo") ||
            prod.Contains("powera") || prod.Contains("victrix") || prod.Contains("scuf") ||
            prod.Contains("nacon") || prod.Contains("thrustmaster") ||
            prod.Contains("gamepad") || prod.Contains("controller") || prod.Contains("joystick"))
        {
            return ControllerType.GenericHid;
        }

        if (dev.UsagePage == 0x01 && (dev.Usage == 0x04 || dev.Usage == 0x05))
        {
            return ControllerType.GenericHid;
        }

        return ControllerType.Unknown;
    }

    public bool Connect(DeviceInfo device)
    {
        Disconnect();

        LogMessage?.Invoke($"Conectando a {device.DisplayTitle}...");

        // Intentar abrir con permisos completos de Lectura/Escritura compartida
        SafeFileHandle handle = HidNative.CreateFile(
            device.DevicePath,
            HidNative.GENERIC_READ | HidNative.GENERIC_WRITE,
            HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
            IntPtr.Zero,
            HidNative.OPEN_EXISTING,
            HidNative.FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            int err = Marshal.GetLastWin32Error();
            LogMessage?.Invoke($"Aviso: No se pudo abrir con escritura exclusiva (Error Win32: {err}). Intentando modo lectura...");

            // Reintentar en modo lectura
            handle = HidNative.CreateFile(
                device.DevicePath,
                HidNative.GENERIC_READ,
                HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
                IntPtr.Zero,
                HidNative.OPEN_EXISTING,
                HidNative.FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                int err2 = Marshal.GetLastWin32Error();
                LogMessage?.Invoke($"Error fatal: No se pudo abrir el dispositivo HID (Error: {err2}). Verifique que no esté bloqueado.");
                return false;
            }
        }

        _deviceHandle = handle;
        CurrentDevice = device;

        // Intentar abrir también un handle sincrónico dedicado exclusivamente a escritura
        try
        {
            _writeHandle = HidNative.CreateFile(
                device.DevicePath,
                HidNative.GENERIC_WRITE,
                HidNative.FILE_SHARE_READ | HidNative.FILE_SHARE_WRITE,
                IntPtr.Zero,
                HidNative.OPEN_EXISTING,
                0, // Sincrónico
                IntPtr.Zero);
            if (_writeHandle.IsInvalid)
            {
                _writeHandle.Dispose();
                _writeHandle = null;
            }
        }
        catch
        {
            _writeHandle = null;
        }

        _cts = new CancellationTokenSource();
        _readThread = new Thread(() => ReadLoop(_cts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
            Name = "HidReadThread"
        };
        _readThread.Start();

        LogMessage?.Invoke($"Conectado con éxito a {device.ProductName} [{device.Connection}].");
        return true;
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_writeHandle != null && !_writeHandle.IsClosed)
        {
            try
            {
                _writeHandle.Close();
                _writeHandle.Dispose();
            }
            catch { }
            _writeHandle = null;
        }

        if (_deviceHandle != null && !_deviceHandle.IsClosed)
        {
            try
            {
                HidNative.CancelIoEx(_deviceHandle, IntPtr.Zero);
            }
            catch { }

            _deviceHandle.Close();
            _deviceHandle.Dispose();
            _deviceHandle = null;
        }

        if (_readThread != null && _readThread.IsAlive)
        {
            _readThread.Join(200);
            _readThread = null;
        }

        CurrentDevice = null;
    }

    private void ReadLoop(CancellationToken token)
    {
        int bufferSize = Math.Max(CurrentDevice?.InputReportLength ?? 64, 64);
        byte[] buffer = new byte[bufferSize];

        // Abrir FileStream sobre el SafeFileHandle en modo asíncrono
        FileStream? stream = null;
        try
        {
            stream = new FileStream(_deviceHandle!, FileAccess.Read, bufferSize, isAsync: true);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"Error iniciando Stream de lectura: {ex.Message}");
            return;
        }

        while (!token.IsCancellationRequested && IsConnected)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    byte[] report = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, report, 0, bytesRead);
                    InputReportReceived?.Invoke(report);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    LogMessage?.Invoke($"Dispositivo desconectado o error de lectura: {ex.Message}");
                    DeviceDisconnected?.Invoke();
                }
                break;
            }
        }

        stream?.Dispose();
    }

    public bool SendOutputReport(byte[] report)
    {
        if (!IsConnected || _deviceHandle == null || report == null || report.Length == 0)
            return false;

        lock (_writeLock)
        {
            try
            {
                // Método 1: Intentar con el buffer con el tamaño reportado en descriptores HID
                int expectedLen = CurrentDevice?.OutputReportLength ?? 0;
                if (expectedLen > 0 && expectedLen != report.Length)
                {
                    byte[] padded = new byte[expectedLen];
                    int copyLen = Math.Min(report.Length, expectedLen);
                    Buffer.BlockCopy(report, 0, padded, 0, copyLen);
                    if (TryWriteBuffer(padded)) return true;
                }

                // Método 2: Intentar con el buffer original
                if (TryWriteBuffer(report)) return true;

                return false;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Error enviando Output Report: {ex.Message}");
                return false;
            }
        }
    }

    private bool TryWriteBuffer(byte[] buffer)
    {
        // Método 1: Intentar mediante handle sincrónico dedicado
        if (_writeHandle != null && !_writeHandle.IsInvalid)
        {
            if (HidNative.WriteFile(_writeHandle, buffer, (uint)buffer.Length, out uint written, IntPtr.Zero) && written > 0)
            {
                return true;
            }
        }

        // Método 2: Intentar mediante handle overlapped con NativeOverlapped y evento Win32
        if (_deviceHandle != null && !_deviceHandle.IsInvalid)
        {
            IntPtr hEvent = HidNative.CreateEvent(IntPtr.Zero, true, false, null);
            if (hEvent != IntPtr.Zero)
            {
                try
                {
                    NativeOverlapped ov = new NativeOverlapped { EventHandle = hEvent };
                    bool ok = HidNative.WriteFile(_deviceHandle, buffer, (uint)buffer.Length, out uint written, ref ov);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err == 997) // ERROR_IO_PENDING
                        {
                            ok = HidNative.GetOverlappedResult(_deviceHandle, ref ov, out written, true);
                        }
                    }
                    if (ok && written > 0) return true;
                }
                catch
                {
                    // Fallback siguiente
                }
                finally
                {
                    HidNative.CloseHandle(hEvent);
                }
            }

            // Método 3: Fallback a HidD_SetOutputReport (Control endpoint)
            if (HidNative.HidD_SetOutputReport(_deviceHandle, buffer, (uint)buffer.Length))
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
