using System.Diagnostics;
using PsCloneTester.Models;

namespace PsCloneTester.Services;

public enum ProtocolMode
{
    AutoDetect,
    DualSense,
    DualShock4,
    Xbox,
    SwitchPro,
    GenericHid
}

public class ControllerManager : IDisposable
{
    private readonly HidDeviceService _hidService;
    private readonly ImuProcessor _imuProcessor;
    private readonly ControllerState _state = new();

    private ProtocolMode _protocolMode = ProtocolMode.AutoDetect;
    private readonly object _stateLock = new();

    // Estado XInput
    private DeviceInfo? _currentXInputDevice;
    private Thread? _xinputPollThread;
    private CancellationTokenSource? _xinputCts;
    private bool _isXInputConnected = false;

    // Estadísticas de paquetes y frecuencia
    private ulong _packetCount = 0;
    private int _recentPackets = 0;
    private readonly Stopwatch _hzStopwatch = Stopwatch.StartNew();

    // Bucle de envío periódico de salida (Keep Alive para gatillos y luces)
    private Thread? _outputThread;
    private CancellationTokenSource? _outputCts;
    private bool _isOutputLoopRunning = false;

    // Configuración actual de salida
    public TriggerConfig LeftTrigger { get; } = new();
    public TriggerConfig RightTrigger { get; } = new();
    public byte LeftMotorRumble { get; set; } = 0;
    public byte RightMotorRumble { get; set; } = 0;
    public byte LedR { get; set; } = 0;
    public byte LedG { get; set; } = 0;
    public byte LedB { get; set; } = 255; // Azul PlayStation puro por defecto
    public bool AutoSendOutputs { get; set; } = true;

    public bool IsConnected => (_currentXInputDevice != null && _isXInputConnected) || _hidService.IsConnected;
    public DeviceInfo? CurrentDevice => _currentXInputDevice ?? _hidService.CurrentDevice;
    public ControllerState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public ProtocolMode SelectedProtocol
    {
        get => _protocolMode;
        set => _protocolMode = value;
    }

    public event Action<ControllerState>? StateUpdated;
    public event Action<string>? LogMessage;
    public event Action? Connected;
    public event Action? Disconnected;

    public ControllerManager()
    {
        _hidService = new HidDeviceService();
        _imuProcessor = new ImuProcessor();

        _hidService.InputReportReceived += OnInputReportReceived;
        _hidService.LogMessage += msg => LogMessage?.Invoke(msg);
        _hidService.DeviceDisconnected += () =>
        {
            StopOutputLoop();
            Disconnected?.Invoke();
        };
    }

    public List<DeviceInfo> GetAvailableControllers(bool includeAll = false)
    {
        var list = _hidService.EnumerateDevices(includeAll);

        // Enumerar ranuras XInput activas (Mandos de Xbox USB / Inalámbricos y PC Gamepads)
        try
        {
            var xSlots = XInputService.GetConnectedSlots();
            foreach (int slot in xSlots)
            {
                list.Insert(0, new DeviceInfo
                {
                    DevicePath = $"XINPUT_SLOT_{slot}",
                    VendorId = 0x045E,
                    ProductId = 0x028E,
                    ProductName = $"Mando Xbox Compatible (Slot {slot + 1})",
                    Manufacturer = "Microsoft (XInput)",
                    DetectedType = ControllerType.XboxOne,
                    Connection = ConnectionType.XInput,
                    XInputUserIndex = slot
                });
            }
        }
        catch
        {
            // Ignorar si XInput no está presente en el sistema
        }

        return list;
    }

    public bool Connect(DeviceInfo device)
    {
        Disconnect();

        if (device.IsXInput)
        {
            _currentXInputDevice = device;
            _isXInputConnected = true;
            _packetCount = 0;
            _recentPackets = 0;
            _hzStopwatch.Restart();

            _xinputCts = new CancellationTokenSource();
            _xinputPollThread = new Thread(() => XInputPollLoop(device.XInputUserIndex, _xinputCts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
                Name = "XInputPollThread"
            };
            _xinputPollThread.Start();

            LogMessage?.Invoke($"Conectado con éxito a {device.ProductName} [XInput Slot {device.XInputUserIndex + 1}].");

            if (AutoSendOutputs)
            {
                StartOutputLoop();
            }

            Connected?.Invoke();
            return true;
        }

        bool success = _hidService.Connect(device);
        if (success)
        {
            _packetCount = 0;
            _recentPackets = 0;
            _hzStopwatch.Restart();

            if (AutoSendOutputs)
            {
                StartOutputLoop();
            }

            Connected?.Invoke();
        }
        return success;
    }

    public void Disconnect()
    {
        StopOutputLoop();

        if (_currentXInputDevice != null)
        {
            _isXInputConnected = false;
            _xinputCts?.Cancel();
            _xinputCts?.Dispose();
            _xinputCts = null;

            if (_xinputPollThread != null && _xinputPollThread.IsAlive)
            {
                _xinputPollThread.Join(150);
                _xinputPollThread = null;
            }
            _currentXInputDevice = null;
        }

        _hidService.Disconnect();
        Disconnected?.Invoke();
    }

    private void XInputPollLoop(int slot, CancellationToken token)
    {
        while (!token.IsCancellationRequested && _isXInputConnected)
        {
            if (XInputService.TryGetState(slot, out var xstate))
            {
                lock (_stateLock)
                {
                    _packetCount++;
                    _recentPackets++;

                    if (_hzStopwatch.ElapsedMilliseconds >= 500)
                    {
                        _state.PollingRateHz = Math.Round(_recentPackets / (_hzStopwatch.ElapsedMilliseconds / 1000.0), 1);
                        _recentPackets = 0;
                        _hzStopwatch.Restart();
                    }

                    _state.PacketNumber = _packetCount;
                    _state.Timestamp = DateTime.UtcNow;

                    XInputService.ParseXInputState(ref xstate, _state, slot);

                    StateUpdated?.Invoke(_state);
                }

                // Sondeo rápido a ~250 Hz (4 ms) para respuesta ultra baja de latencia
                Thread.Sleep(4);
            }
            else
            {
                // Dispositivo desconectado
                LogMessage?.Invoke($"El mando Xbox en Slot {slot + 1} se ha desconectado.");
                _isXInputConnected = false;
                Disconnected?.Invoke();
                break;
            }
        }
    }

    public void CalibrateSensors()
    {
        lock (_stateLock)
        {
            _imuProcessor.Calibrate(
                _state.RawAccelX, _state.RawAccelY, _state.RawAccelZ,
                _state.RawGyroPitch, _state.RawGyroYaw, _state.RawGyroRoll);
        }
        LogMessage?.Invoke("Sensores IMU (Acelerómetro y Giróscopo) calibrados a cero.");
    }

    public void ResetSensorCalibration()
    {
        _imuProcessor.ResetCalibration();
        LogMessage?.Invoke("Calibración de sensores restablecida.");
    }

    private void OnInputReportReceived(byte[] rawReport)
    {
        lock (_stateLock)
        {
            _packetCount++;
            _recentPackets++;

            if (_hzStopwatch.ElapsedMilliseconds >= 500)
            {
                _state.PollingRateHz = Math.Round(_recentPackets / (_hzStopwatch.ElapsedMilliseconds / 1000.0), 1);
                _recentPackets = 0;
                _hzStopwatch.Restart();
            }

            _state.PacketNumber = _packetCount;
            _state.Timestamp = DateTime.UtcNow;

            bool isBluetooth = _hidService.CurrentDevice?.Connection == ConnectionType.Bluetooth;
            ProtocolMode activeProto = ResolveProtocol();

            switch (activeProto)
            {
                case ProtocolMode.DualSense:
                    if (!DualSenseProtocol.ParseInputReport(rawReport, _state, isBluetooth))
                    {
                        // Fallback inteligente si el clon no cumple el tamaño de reporte completo de DualSense
                        GenericHidProtocol.ParseInputReport(rawReport, _state);
                    }
                    break;

                case ProtocolMode.DualShock4:
                    if (!DualShock4Protocol.ParseInputReport(rawReport, _state, isBluetooth))
                    {
                        GenericHidProtocol.ParseInputReport(rawReport, _state);
                    }
                    break;

                case ProtocolMode.Xbox:
                    if (!XboxBluetoothProtocol.ParseInputReport(rawReport, _state))
                    {
                        GenericHidProtocol.ParseInputReport(rawReport, _state);
                    }
                    break;

                case ProtocolMode.SwitchPro:
                    if (!SwitchProProtocol.ParseInputReport(rawReport, _state))
                    {
                        GenericHidProtocol.ParseInputReport(rawReport, _state);
                    }
                    break;

                case ProtocolMode.GenericHid:
                default:
                    GenericHidProtocol.ParseInputReport(rawReport, _state);
                    break;
            }

            // Procesar IMU (Acelerómetro y Giroscopio) con filtro de actitud
            _imuProcessor.Process(_state);

            StateUpdated?.Invoke(_state);
        }
    }

    private ProtocolMode ResolveProtocol()
    {
        if (_protocolMode != ProtocolMode.AutoDetect)
            return _protocolMode;

        if (CurrentDevice == null)
            return ProtocolMode.DualSense;

        if (CurrentDevice.IsXInput || CurrentDevice.DetectedType is ControllerType.Xbox360 or ControllerType.XboxOne or ControllerType.XboxSeries)
            return ProtocolMode.Xbox;

        if (CurrentDevice.DetectedType is ControllerType.SwitchPro or ControllerType.SwitchJoyCon)
            return ProtocolMode.SwitchPro;

        if (CurrentDevice.DetectedType is ControllerType.DualSense or ControllerType.DualSenseEdge or ControllerType.ClonePs5)
            return ProtocolMode.DualSense;

        if (CurrentDevice.DetectedType is ControllerType.DualShock4V1 or ControllerType.DualShock4V2 or ControllerType.ClonePs4)
            return ProtocolMode.DualShock4;

        if (CurrentDevice.InputReportLength < 40)
            return ProtocolMode.GenericHid;

        return ProtocolMode.GenericHid;
    }

    public void SendOutputNow()
    {
        // 1. Manejo específico para mandos conectados mediante XInput
        if (_currentXInputDevice != null && _isXInputConnected)
        {
            XInputService.SetVibration(LeftMotorRumble, RightMotorRumble, _currentXInputDevice.XInputUserIndex);
            return;
        }

        // Fallback a XInput por si el mando en modo HID también responde a XInput
        if (LeftMotorRumble > 0 || RightMotorRumble > 0)
        {
            XInputService.SetVibration(LeftMotorRumble, RightMotorRumble);
        }
        else
        {
            XInputService.SetVibration(0, 0);
        }

        if (!IsConnected || _hidService.CurrentDevice == null) return;

        bool isBt = _hidService.CurrentDevice.Connection == ConnectionType.Bluetooth;
        ProtocolMode activeProto = ResolveProtocol();

        if (activeProto == ProtocolMode.DualSense)
        {
            byte[] outDs = DualSenseProtocol.BuildOutputReport(
                isBt,
                LeftTrigger,
                RightTrigger,
                LeftMotorRumble,
                RightMotorRumble,
                LedR, LedG, LedB);

            _hidService.SendOutputReport(outDs);

            // Si hay vibración activa o es un clon, enviar también formato DS4 (compatibilidad con chips clones mixtos)
            if (LeftMotorRumble > 0 || RightMotorRumble > 0 ||
                _hidService.CurrentDevice.DetectedType == ControllerType.ClonePs5 ||
                _hidService.CurrentDevice.DetectedType == ControllerType.ClonePs4 ||
                _hidService.CurrentDevice.DetectedType == ControllerType.GenericHid)
            {
                byte[] outDs4 = DualShock4Protocol.BuildOutputReport(
                    isBt, LeftMotorRumble, RightMotorRumble, LedR, LedG, LedB);
                _hidService.SendOutputReport(outDs4);
            }
        }
        else if (activeProto == ProtocolMode.DualShock4)
        {
            byte[] outDs4 = DualShock4Protocol.BuildOutputReport(
                isBt,
                LeftMotorRumble,
                RightMotorRumble,
                LedR, LedG, LedB);

            _hidService.SendOutputReport(outDs4);
        }
        else // Xbox, SwitchPro o GenericHid
        {
            byte[] outDs = DualSenseProtocol.BuildOutputReport(
                isBt, LeftTrigger, RightTrigger, LeftMotorRumble, RightMotorRumble, LedR, LedG, LedB);
            _hidService.SendOutputReport(outDs);

            byte[] outDs4 = DualShock4Protocol.BuildOutputReport(
                isBt, LeftMotorRumble, RightMotorRumble, LedR, LedG, LedB);
            _hidService.SendOutputReport(outDs4);
        }
    }

    public void StartOutputLoop()
    {
        StopOutputLoop();

        _outputCts = new CancellationTokenSource();
        _isOutputLoopRunning = true;
        _outputThread = new Thread(() => OutputWorker(_outputCts.Token))
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal,
            Name = "OutputKeepAliveLoop"
        };
        _outputThread.Start();
    }

    public void StopOutputLoop()
    {
        _isOutputLoopRunning = false;
        _outputCts?.Cancel();
        _outputCts?.Dispose();
        _outputCts = null;

        if (_outputThread != null && _outputThread.IsAlive)
        {
            _outputThread.Join(150);
            _outputThread = null;
        }
    }

    private void OutputWorker(CancellationToken token)
    {
        while (!token.IsCancellationRequested && IsConnected && _isOutputLoopRunning)
        {
            try
            {
                SendOutputNow();
                Thread.Sleep(20);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                Thread.Sleep(50);
            }
        }
    }

    public void Dispose()
    {
        StopOutputLoop();
        Disconnect();
        _hidService.Dispose();
    }
}
