using System.Diagnostics;
using PsCloneTester.Models;

namespace PsCloneTester.Services;

public enum ProtocolMode
{
    AutoDetect,
    DualSense,
    DualShock4,
    GenericHid
}

public class ControllerManager : IDisposable
{
    private readonly HidDeviceService _hidService;
    private readonly ImuProcessor _imuProcessor;
    private readonly ControllerState _state = new();

    private ProtocolMode _protocolMode = ProtocolMode.AutoDetect;
    private readonly object _stateLock = new();

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

    public bool IsConnected => _hidService.IsConnected;
    public DeviceInfo? CurrentDevice => _hidService.CurrentDevice;
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
        return _hidService.EnumerateDevices(includeAll);
    }

    public bool Connect(DeviceInfo device)
    {
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
        _hidService.Disconnect();
        Disconnected?.Invoke();
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
                    DualSenseProtocol.ParseInputReport(rawReport, _state, isBluetooth);
                    break;

                case ProtocolMode.DualShock4:
                    DualShock4Protocol.ParseInputReport(rawReport, _state, isBluetooth);
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

        if (_hidService.CurrentDevice == null)
            return ProtocolMode.DualSense;

        var type = _hidService.CurrentDevice.DetectedType;
        if (type is ControllerType.DualSense or ControllerType.DualSenseEdge or ControllerType.ClonePs5)
            return ProtocolMode.DualSense;

        if (type is ControllerType.DualShock4V1 or ControllerType.DualShock4V2 or ControllerType.ClonePs4)
            return ProtocolMode.DualShock4;

        return ProtocolMode.DualSense; // Probar DualSense por defecto para imitaciones modernas
    }

    public void SendOutputNow()
    {
        // 1. Fallback a XInput (por si el clon está en modo PC/XInput en Windows)
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

            bool sent = _hidService.SendOutputReport(outDs);

            // Si hay vibración activa o es un clon, intentar también formato DS4 (muchos clones copian VID/PID de Sony pero su firmware de motor es DS4)
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
        else // GenericHid o Auto
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
            Name = "HidOutputLoop"
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
        // Enviar reportes de control a 50Hz (~20ms) para mantener los gatillos adaptativos y luces activos
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
        _hidService.Dispose();
    }
}
