using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PsCloneTester.Models;
using PsCloneTester.Services;

namespace PsCloneTester.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ControllerManager _manager;
    private readonly DispatcherTimer _uiTimer;

    // Dispositivos y conexión
    public ObservableCollection<DeviceInfo> Devices { get; } = new();
    private DeviceInfo? _selectedDevice;
    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(ConnectionBadgeBrush));
            }
        }
    }

    public string ConnectionStatusText => IsConnected
        ? $"Conectado: {_manager.CurrentDevice?.ProductName ?? "Mando"}"
        : "Desconectado - Seleccione un dispositivo y pulse Conectar";

    public Brush ConnectionBadgeBrush => IsConnected
        ? new SolidColorBrush(Color.FromRgb(0, 220, 130)) // Verde activo
        : new SolidColorBrush(Color.FromRgb(150, 150, 160)); // Gris

    private ProtocolMode _selectedProtocol = ProtocolMode.AutoDetect;
    public ProtocolMode SelectedProtocol
    {
        get => _selectedProtocol;
        set
        {
            if (SetProperty(ref _selectedProtocol, value))
            {
                _manager.SelectedProtocol = value;
            }
        }
    }

    public ObservableCollection<ProtocolMode> ProtocolOptions { get; } = new()
    {
        ProtocolMode.AutoDetect,
        ProtocolMode.DualSense,
        ProtocolMode.DualShock4,
        ProtocolMode.GenericHid
    };

    // Estadísticas
    private double _pollingRateHz;
    public double PollingRateHz
    {
        get => _pollingRateHz;
        set => SetProperty(ref _pollingRateHz, value);
    }

    // Navegación Sidebar estilo Wpf.Ui
    private int _selectedNavigationIndex = 0;
    public int SelectedNavigationIndex
    {
        get => _selectedNavigationIndex;
        set
        {
            if (SetProperty(ref _selectedNavigationIndex, value))
            {
                OnPropertyChanged(nameof(ActivePageTitle));
                OnPropertyChanged(nameof(ActivePageCategory));
            }
        }
    }

    public string ActivePageTitle => SelectedNavigationIndex switch
    {
        0 => "Prueba",
        1 => "Test de Circularidad",
        2 => "Gatillos Adaptativos",
        3 => "Sensores e IMU",
        4 => "Pantalla Táctil",
        5 => "Vibración & Iluminación",
        6 => "Dispositivos & Hex",
        7 => "Ayuda & Diagnóstico",
        _ => "Prueba"
    };

    public string ActivePageCategory => SelectedNavigationIndex switch
    {
        0 => "PRUEBA DE MANDO",
        1 => "BENCHMARK RADIAL",
        2 => "SERVOMOTORES HÁPTICOS",
        3 => "TELEMETRÍA DE MOVIMIENTO",
        4 => "SUPERFICIE MULTI-TOUCH",
        5 => "MOTORES Y COLOR RGB",
        6 => "CONECTIVIDAD & PAQUETES",
        7 => "MANUAL Y GUÍA DE CLONES",
        _ => "PRUEBA DE MANDO"
    };

    private string _lastPressedButtonText = "TRIÁNGULO listo";
    public string LastPressedButtonText
    {
        get => _lastPressedButtonText;
        set => SetProperty(ref _lastPressedButtonText, value);
    }

    public bool IsLeftStickCentered => Math.Abs(State.LeftStickX) < 0.08 && Math.Abs(State.LeftStickY) < 0.08;
    public bool IsRightStickCentered => Math.Abs(State.RightStickX) < 0.08 && Math.Abs(State.RightStickY) < 0.08;
    public string LeftStickStatusText => IsLeftStickCentered ? "CENTRADO" : "ACTIVO";
    public string RightStickStatusText => IsRightStickCentered ? "CENTRADO" : "ACTIVO";
    public Brush LeftStickStatusBrush => IsLeftStickCentered
        ? new SolidColorBrush(Color.FromRgb(120, 135, 155))
        : new SolidColorBrush(Color.FromRgb(0, 229, 153));
    public Brush RightStickStatusBrush => IsRightStickCentered
        ? new SolidColorBrush(Color.FromRgb(120, 135, 155))
        : new SolidColorBrush(Color.FromRgb(0, 229, 153));

    public double LeftStickMagnitude => Math.Clamp(Math.Sqrt(State.LeftStickX * State.LeftStickX + State.LeftStickY * State.LeftStickY) * 100.0, 0, 100);
    public double RightStickMagnitude => Math.Clamp(Math.Sqrt(State.RightStickX * State.RightStickX + State.RightStickY * State.RightStickY) * 100.0, 0, 100);

    public double LatencyMs => PollingRateHz > 0 ? Math.Round(1000.0 / PollingRateHz, 1) : 0.0;
    public string LatencyText => PollingRateHz > 0 ? $"{LatencyMs:F1} ms" : "-- ms";

    public string CurrentDeviceDisplayName => _manager.CurrentDevice?.ProductName ?? (_manager.IsConnected ? "Mando Conectado" : "DualSense Wireless Controller");
    public string CurrentDeviceConnectionText => _manager.IsConnected
        ? (_manager.CurrentDevice?.Connection == ConnectionType.Bluetooth ? "Conectado por Bluetooth" : "Conectado por USB")
        : "Desconectado";

    private ulong _packetCount;
    public ulong PacketCount
    {
        get => _packetCount;
        set => SetProperty(ref _packetCount, value);
    }

    // Estado del Mando (Copia reactiva para UI)
    private ControllerState _state = new();
    public ControllerState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    // Botones de acción principales (Propiedades directas y reactivas para la UI)
    public bool IsTrianglePressed => State.Triangle;
    public bool IsCirclePressed => State.Circle;
    public bool IsCrossPressed => State.Cross;
    public bool IsSquarePressed => State.Square;

    // Historial para gráfico de osciloscopio IMU
    public List<double> AccelHistoryX { get; } = new(100);
    public List<double> AccelHistoryY { get; } = new(100);
    public List<double> AccelHistoryZ { get; } = new(100);

    // Puntos de trazo para lienzo del touchpad
    public List<Point> TouchTrailPoints { get; } = new();
    private bool _enableTouchTrail = true;
    public bool EnableTouchTrail
    {
        get => _enableTouchTrail;
        set => SetProperty(ref _enableTouchTrail, value);
    }

    // Gatillos Adaptativos
    private string _selectedTriggerPreset = "Resistencia Continua";
    public string SelectedTriggerPreset
    {
        get => _selectedTriggerPreset;
        set
        {
            if (SetProperty(ref _selectedTriggerPreset, value))
            {
                ApplyPresetConfig(value);
            }
        }
    }

    public ObservableCollection<string> TriggerPresets { get; } = new()
    {
        "Desactivado (Normal)",
        "Resistencia Continua (Rigid)",
        "Gatillo Pesado (Heavy Stop)",
        "Pulsación Rítmica (Pulse)",
        "Arco Tensado (Bow)",
        "Metralleta / Retroceso (Machine Gun)",
        "Calibración de Motores (Cycle)"
    };

    private bool _applyToLeft = true;
    public bool ApplyToLeft
    {
        get => _applyToLeft;
        set => SetProperty(ref _applyToLeft, value);
    }

    private bool _applyToRight = true;
    public bool ApplyToRight
    {
        get => _applyToRight;
        set => SetProperty(ref _applyToRight, value);
    }

    private byte _triggerStartPos = 0;
    public byte TriggerStartPos
    {
        get => _triggerStartPos;
        set
        {
            if (SetProperty(ref _triggerStartPos, value))
            {
                UpdateCurrentTriggerParams();
            }
        }
    }

    private byte _triggerEndPos = 255;
    public byte TriggerEndPos
    {
        get => _triggerEndPos;
        set
        {
            if (SetProperty(ref _triggerEndPos, value))
            {
                UpdateCurrentTriggerParams();
            }
        }
    }

    private byte _triggerForce = 200;
    public byte TriggerForce
    {
        get => _triggerForce;
        set
        {
            if (SetProperty(ref _triggerForce, value))
            {
                UpdateCurrentTriggerParams();
            }
        }
    }

    private byte _triggerFrequency = 15;
    public byte TriggerFrequency
    {
        get => _triggerFrequency;
        set
        {
            if (SetProperty(ref _triggerFrequency, value))
            {
                UpdateCurrentTriggerParams();
            }
        }
    }

    private bool _autoSendOutputs = true;
    public bool AutoSendOutputs
    {
        get => _autoSendOutputs;
        set
        {
            if (SetProperty(ref _autoSendOutputs, value))
            {
                _manager.AutoSendOutputs = value;
                if (value && IsConnected)
                    _manager.StartOutputLoop();
                else
                    _manager.StopOutputLoop();
            }
        }
    }

    // Vibración (Rumble)
    private byte _leftHeavyRumble = 0;
    public byte LeftHeavyRumble
    {
        get => _leftHeavyRumble;
        set
        {
            if (SetProperty(ref _leftHeavyRumble, value))
            {
                _manager.LeftMotorRumble = value;
                _manager.SendOutputNow();
            }
        }
    }

    private byte _rightLightRumble = 0;
    public byte RightLightRumble
    {
        get => _rightLightRumble;
        set
        {
            if (SetProperty(ref _rightLightRumble, value))
            {
                _manager.RightMotorRumble = value;
                _manager.SendOutputNow();
            }
        }
    }

    // Barra de Luz LED
    private byte _ledR = 0;
    public byte LedR
    {
        get => _ledR;
        set
        {
            if (SetProperty(ref _ledR, value))
            {
                _manager.LedR = value;
                OnPropertyChanged(nameof(CurrentLedBrush));
                _manager.SendOutputNow();
            }
        }
    }

    private byte _ledG = 0;
    public byte LedG
    {
        get => _ledG;
        set
        {
            if (SetProperty(ref _ledG, value))
            {
                _manager.LedG = value;
                OnPropertyChanged(nameof(CurrentLedBrush));
                _manager.SendOutputNow();
            }
        }
    }

    private byte _ledB = 255;
    public byte LedB
    {
        get => _ledB;
        set
        {
            if (SetProperty(ref _ledB, value))
            {
                _manager.LedB = value;
                OnPropertyChanged(nameof(CurrentLedBrush));
                _manager.SendOutputNow();
            }
        }
    }

    public Brush CurrentLedBrush => new SolidColorBrush(Color.FromRgb(LedR, LedG, LedB));

    // Hex Inspector
    private string _rawHexView = "Sin datos de paquetes";
    public string RawHexView
    {
        get => _rawHexView;
        set => SetProperty(ref _rawHexView, value);
    }

    // Logs del sistema
    public ObservableCollection<string> Logs { get; } = new();

    // Test de Circularidad de Palancas Analógicas
    public CircularityTester CircularityTester { get; } = new();

    private string _selectedCircularityStick = "Stick Izquierdo (L)";
    public string SelectedCircularityStick
    {
        get => _selectedCircularityStick;
        set
        {
            if (SetProperty(ref _selectedCircularityStick, value))
            {
                CircularityTester.Reset();
            }
        }
    }

    public ObservableCollection<string> CircularityStickOptions { get; } = new()
    {
        "Stick Izquierdo (L)",
        "Stick Derecho (R)"
    };

    public bool IsCircularityRunning => CircularityTester.IsRunning;
    public bool IsCircularityCompleted => CircularityTester.IsCompleted;
    public double[] CircularitySectorRadii => CircularityTester.GetSmoothedContour();
    public List<Point> CircularitySamplePoints => CircularityTester.SamplePoints;
    public double CircularityAverageError => CircularityTester.AverageErrorPercent;
    public double CircularityMaxRadius => CircularityTester.MaxRadius;
    public double CircularityMinRadius => CircularityTester.MinRadius;
    public double CircularityCoverage => CircularityTester.CoveragePercent;
    public double CircularityLapProgress => CircularityTester.LapProgressPercent;
    public string CircularityVerdict => CircularityTester.StatusDescription;
    public string CircularityQualityTag => CircularityTester.QualityTag;
    public string CircularityStatusDescription => CircularityTester.StatusDescription;
    public string CircularityCornerGatingText => CircularityTester.CornerGatingText;
    public Brush CircularityCornerGatingBrush => (Brush)new BrushConverter().ConvertFromString(CircularityTester.CornerGatingColor)!;
    public string CircularityRestDriftText => CircularityTester.RestDriftText;
    public string CircularityProfileType => CircularityTester.ProfileType;
    public Brush CircularityRatingBrush => (Brush)new BrushConverter().ConvertFromString(CircularityTester.RatingColor)!;

    public double CurrentTestStickX => SelectedCircularityStick.Contains("Izquierdo") ? State.LeftStickX : State.RightStickX;
    public double CurrentTestStickY => SelectedCircularityStick.Contains("Izquierdo") ? State.LeftStickY : State.RightStickY;

    // Comandos
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand CalibrateSensorsCommand { get; }
    public RelayCommand ResetCalibrationCommand { get; }
    public RelayCommand ApplyTriggerEffectCommand { get; }
    public RelayCommand StopTriggerEffectCommand { get; }
    public RelayCommand TestRumblePulseCommand { get; }
    public RelayCommand TestRumbleContinuousCommand { get; }
    public RelayCommand StopRumbleCommand { get; }
    public RelayCommand ClearTouchpadCanvasCommand { get; }
    public RelayCommand SetLedPresetCommand { get; }
    public RelayCommand StartCircularityTestCommand { get; }
    public RelayCommand StopCircularityTestCommand { get; }
    public RelayCommand ResetCircularityTestCommand { get; }
    public RelayCommand NavigateCommand { get; }

    public MainViewModel()
    {
        _manager = new ControllerManager();

        _manager.LogMessage += msg =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
                if (Logs.Count > 100) Logs.RemoveAt(Logs.Count - 1);
            });
        };

        _manager.Connected += () =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsConnected = true;
                UpdateCurrentTriggerParams();
            });
        };

        _manager.Disconnected += () =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                IsConnected = false;
            });
        };

        // Procesar muestras de circularidad a máxima frecuencia de hardware (250Hz - 1000Hz)
        _manager.StateUpdated += state =>
        {
            if (CircularityTester.IsRunning && !CircularityTester.IsCompleted)
            {
                double sx = SelectedCircularityStick.Contains("Izquierdo") ? state.LeftStickX : state.RightStickX;
                double sy = SelectedCircularityStick.Contains("Izquierdo") ? state.LeftStickY : state.RightStickY;
                CircularityTester.AddSample(sx, sy);
            }
        };

        // Comandos
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        ConnectCommand = new RelayCommand(Connect, () => SelectedDevice != null && !IsConnected);
        DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
        CalibrateSensorsCommand = new RelayCommand(() => _manager.CalibrateSensors(), () => IsConnected);
        ResetCalibrationCommand = new RelayCommand(() => _manager.ResetSensorCalibration(), () => IsConnected);
        ApplyTriggerEffectCommand = new RelayCommand(ApplyCurrentTriggerEffect, () => IsConnected);
        StopTriggerEffectCommand = new RelayCommand(StopAllTriggerEffects, () => IsConnected);
        TestRumblePulseCommand = new RelayCommand(TestRumblePulse);
        TestRumbleContinuousCommand = new RelayCommand(TestRumbleContinuous);
        StopRumbleCommand = new RelayCommand(StopRumble);
        ClearTouchpadCanvasCommand = new RelayCommand(() => TouchTrailPoints.Clear());
        SetLedPresetCommand = new RelayCommand(param => SetLedPreset(param as string));
        StartCircularityTestCommand = new RelayCommand(StartCircularityTest);
        StopCircularityTestCommand = new RelayCommand(StopCircularityTest);
        ResetCircularityTestCommand = new RelayCommand(ResetCircularityTest);
        NavigateCommand = new RelayCommand(param =>
        {
            if (int.TryParse(param?.ToString(), out int idx))
                SelectedNavigationIndex = idx;
        });

        // Temporizador UI para refresco a 60 FPS
        _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _uiTimer.Tick += OnUiTimerTick;
        _uiTimer.Start();

        // Cargar lista inicial
        RefreshDevices();
    }

    private bool _showAllHidDevices = false;
    public bool ShowAllHidDevices
    {
        get => _showAllHidDevices;
        set
        {
            if (SetProperty(ref _showAllHidDevices, value))
            {
                RefreshDevices();
            }
        }
    }

    public void RefreshDevices()
    {
        Devices.Clear();
        var list = _manager.GetAvailableControllers(_showAllHidDevices);
        foreach (var dev in list)
        {
            Devices.Add(dev);
        }

        if (SelectedDevice == null && Devices.Count > 0)
        {
            SelectedDevice = Devices[0];
        }

        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Se encontraron {Devices.Count} mandos / dispositivos HID.");
    }

    private void Connect()
    {
        if (SelectedDevice == null) return;
        _manager.Connect(SelectedDevice);
    }

    private void Disconnect()
    {
        _manager.Disconnect();
    }

    private void OnUiTimerTick(object? sender, EventArgs e)
    {
        if (!IsConnected) return;

        var snapshot = _manager.CurrentState;
        State = snapshot;
        PollingRateHz = snapshot.PollingRateHz;
        PacketCount = snapshot.PacketNumber;

        // Historial de acelerómetro
        if (AccelHistoryX.Count > 100) AccelHistoryX.RemoveAt(0);
        if (AccelHistoryY.Count > 100) AccelHistoryY.RemoveAt(0);
        if (AccelHistoryZ.Count > 100) AccelHistoryZ.RemoveAt(0);

        AccelHistoryX.Add(snapshot.AccelX_G);
        AccelHistoryY.Add(snapshot.AccelY_G);
        AccelHistoryZ.Add(snapshot.AccelZ_G);

        // Trazo de Touchpad
        if (EnableTouchTrail && snapshot.Touch1.IsActive)
        {
            TouchTrailPoints.Add(new Point(snapshot.Touch1.RawX, snapshot.Touch1.RawY));
            if (TouchTrailPoints.Count > 400)
            {
                TouchTrailPoints.RemoveAt(0);
            }
        }

        // Hex Viewer
        if (snapshot.RawBytes.Length > 0)
        {
            RawHexView = FormatHexDump(snapshot.RawBytes);
        }

        // Test de circularidad
        if (CircularityTester.IsRunning || CircularityTester.IsCompleted)
        {
            OnPropertyChanged(nameof(CircularitySectorRadii));
            OnPropertyChanged(nameof(CircularitySamplePoints));
            OnPropertyChanged(nameof(CircularityAverageError));
            OnPropertyChanged(nameof(CircularityMaxRadius));
            OnPropertyChanged(nameof(CircularityMinRadius));
            OnPropertyChanged(nameof(CircularityCoverage));
            OnPropertyChanged(nameof(CircularityLapProgress));
            OnPropertyChanged(nameof(IsCircularityCompleted));
            OnPropertyChanged(nameof(IsCircularityRunning));
            OnPropertyChanged(nameof(CircularityVerdict));
            OnPropertyChanged(nameof(CircularityRatingBrush));
            OnPropertyChanged(nameof(CircularityQualityTag));
            OnPropertyChanged(nameof(CircularityStatusDescription));
            OnPropertyChanged(nameof(CircularityCornerGatingText));
            OnPropertyChanged(nameof(CircularityCornerGatingBrush));
            OnPropertyChanged(nameof(CircularityRestDriftText));
            OnPropertyChanged(nameof(CircularityProfileType));
        }

        // Detección de última pulsación para el panel de botones
        if (snapshot.Triangle) LastPressedButtonText = "TRIÁNGULO detectado";
        else if (snapshot.Circle) LastPressedButtonText = "CÍRCULO detectado";
        else if (snapshot.Cross) LastPressedButtonText = "CRUZ (X) detectada";
        else if (snapshot.Square) LastPressedButtonText = "CUADRADO detectado";
        else if (snapshot.L1) LastPressedButtonText = "L1 presionado";
        else if (snapshot.R1) LastPressedButtonText = "R1 presionado";
        else if (snapshot.L2Button) LastPressedButtonText = "Gatillo L2 fondo";
        else if (snapshot.R2Button) LastPressedButtonText = "Gatillo R2 fondo";
        else if (snapshot.L3) LastPressedButtonText = "L3 (Stick Izquierdo)";
        else if (snapshot.R3) LastPressedButtonText = "R3 (Stick Derecho)";
        else if (snapshot.DpadUp) LastPressedButtonText = "D-PAD ARRIBA";
        else if (snapshot.DpadDown) LastPressedButtonText = "D-PAD ABAJO";
        else if (snapshot.DpadLeft) LastPressedButtonText = "D-PAD IZQUIERDA";
        else if (snapshot.DpadRight) LastPressedButtonText = "D-PAD DERECHA";
        else if (snapshot.Share) LastPressedButtonText = "SHARE / CREATE";
        else if (snapshot.Options) LastPressedButtonText = "OPTIONS";
        else if (snapshot.PsButton) LastPressedButtonText = "BOTÓN PS";
        else if (snapshot.TouchpadButton) LastPressedButtonText = "CLIC TOUCHPAD";

        OnPropertyChanged(nameof(IsTrianglePressed));
        OnPropertyChanged(nameof(IsCirclePressed));
        OnPropertyChanged(nameof(IsCrossPressed));
        OnPropertyChanged(nameof(IsSquarePressed));

        OnPropertyChanged(nameof(CurrentTestStickX));
        OnPropertyChanged(nameof(CurrentTestStickY));
        OnPropertyChanged(nameof(IsLeftStickCentered));
        OnPropertyChanged(nameof(IsRightStickCentered));
        OnPropertyChanged(nameof(LeftStickStatusText));
        OnPropertyChanged(nameof(RightStickStatusText));
        OnPropertyChanged(nameof(LeftStickStatusBrush));
        OnPropertyChanged(nameof(RightStickStatusBrush));
        OnPropertyChanged(nameof(LeftStickMagnitude));
        OnPropertyChanged(nameof(RightStickMagnitude));
        OnPropertyChanged(nameof(LatencyMs));
        OnPropertyChanged(nameof(LatencyText));
        OnPropertyChanged(nameof(CurrentDeviceDisplayName));
        OnPropertyChanged(nameof(CurrentDeviceConnectionText));
        OnPropertyChanged(nameof(State));
    }

    private string FormatHexDump(byte[] bytes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- REPORTE HID (Longitud: {bytes.Length} bytes) ---");
        for (int i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"{i:X4}: ");
            int chunk = Math.Min(16, bytes.Length - i);
            for (int j = 0; j < chunk; j++)
            {
                sb.Append($"{bytes[i + j]:X2} ");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private void ApplyPresetConfig(string preset)
    {
        switch (preset)
        {
            case "Desactivado (Normal)":
                TriggerStartPos = 0;
                TriggerEndPos = 255;
                TriggerForce = 0;
                break;

            case "Resistencia Continua (Rigid)":
                TriggerStartPos = 0;
                TriggerEndPos = 255;
                TriggerForce = 220;
                break;

            case "Gatillo Pesado (Heavy Stop)":
                TriggerStartPos = 120; // Se frena en seco a la mitad
                TriggerEndPos = 255;
                TriggerForce = 255;
                break;

            case "Pulsación Rítmica (Pulse)":
                TriggerStartPos = 20;
                TriggerEndPos = 255;
                TriggerForce = 230;
                TriggerFrequency = 20;
                break;

            case "Arco Tensado (Bow)":
                TriggerStartPos = 20;
                TriggerEndPos = 200;
                TriggerForce = 210;
                break;

            case "Metralleta / Retroceso (Machine Gun)":
                TriggerStartPos = 10;
                TriggerEndPos = 220;
                TriggerForce = 255;
                TriggerFrequency = 25;
                break;

            case "Calibración de Motores (Cycle)":
                TriggerStartPos = 0;
                TriggerForce = 255;
                break;
        }

        UpdateCurrentTriggerParams();
    }

    private void UpdateCurrentTriggerParams()
    {
        TriggerEffectMode mode = SelectedTriggerPreset switch
        {
            "Desactivado (Normal)" => TriggerEffectMode.Off,
            "Resistencia Continua (Rigid)" => TriggerEffectMode.Rigid,
            "Gatillo Pesado (Heavy Stop)" => TriggerEffectMode.HardStop,
            "Pulsación Rítmica (Pulse)" => TriggerEffectMode.Pulse,
            "Arco Tensado (Bow)" => TriggerEffectMode.Bow,
            "Metralleta / Retroceso (Machine Gun)" => TriggerEffectMode.Galloping,
            "Calibración de Motores (Cycle)" => TriggerEffectMode.Calibration,
            _ => TriggerEffectMode.Rigid
        };

        if (ApplyToLeft)
        {
            _manager.LeftTrigger.Mode = mode;
            _manager.LeftTrigger.StartPosition = TriggerStartPos;
            _manager.LeftTrigger.EndPosition = TriggerEndPos;
            _manager.LeftTrigger.Force = TriggerForce;
            _manager.LeftTrigger.Frequency = TriggerFrequency;
        }
        else
        {
            _manager.LeftTrigger.Mode = TriggerEffectMode.Off;
        }

        if (ApplyToRight)
        {
            _manager.RightTrigger.Mode = mode;
            _manager.RightTrigger.StartPosition = TriggerStartPos;
            _manager.RightTrigger.EndPosition = TriggerEndPos;
            _manager.RightTrigger.Force = TriggerForce;
            _manager.RightTrigger.Frequency = TriggerFrequency;
        }
        else
        {
            _manager.RightTrigger.Mode = TriggerEffectMode.Off;
        }

        if (IsConnected)
        {
            _manager.SendOutputNow();
        }
    }

    private void ApplyCurrentTriggerEffect()
    {
        UpdateCurrentTriggerParams();
        _manager.SendOutputNow();
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Efecto '{SelectedTriggerPreset}' transmitido a los gatillos.");
    }

    private void StopAllTriggerEffects()
    {
        _manager.LeftTrigger.Mode = TriggerEffectMode.Off;
        _manager.RightTrigger.Mode = TriggerEffectMode.Off;
        _manager.SendOutputNow();
        SelectedTriggerPreset = "Desactivado (Normal)";
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Efectos de gatillos desactivados.");
    }

    private async void TestRumblePulse()
    {
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 📳 Probando vibración (HID + XInput)...");
        LeftHeavyRumble = 255;
        RightLightRumble = 255;
        _manager.SendOutputNow();
        await Task.Delay(1200);
        LeftHeavyRumble = 0;
        RightLightRumble = 0;
        _manager.SendOutputNow();
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 📳 Test de vibración completado.");
    }

    private void TestRumbleContinuous()
    {
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ⚡ Vibración continua activada (Máxima potencia)...");
        LeftHeavyRumble = 255;
        RightLightRumble = 255;
        _manager.SendOutputNow();
    }

    private void StopRumble()
    {
        LeftHeavyRumble = 0;
        RightLightRumble = 0;
        _manager.SendOutputNow();
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🛑 Motores de vibración apagados.");
    }

    private void StartCircularityTest()
    {
        CircularityTester.Start();
        OnPropertyChanged(nameof(IsCircularityRunning));
        OnPropertyChanged(nameof(IsCircularityCompleted));
        OnPropertyChanged(nameof(CircularityLapProgress));
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] 🔄 Test de Circularidad iniciado para {SelectedCircularityStick}. Gire 1 vuelta (360°).");
    }

    private void StopCircularityTest()
    {
        CircularityTester.Stop();
        OnPropertyChanged(nameof(IsCircularityRunning));
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ⏹ Test de Circularidad detenido. Error: {CircularityAverageError:F1}% - {CircularityVerdict}");
    }

    private void ResetCircularityTest()
    {
        CircularityTester.Reset();
        OnPropertyChanged(nameof(CircularitySectorRadii));
        OnPropertyChanged(nameof(CircularitySamplePoints));
        OnPropertyChanged(nameof(CircularityAverageError));
        OnPropertyChanged(nameof(CircularityMaxRadius));
        OnPropertyChanged(nameof(CircularityMinRadius));
        OnPropertyChanged(nameof(CircularityCoverage));
        OnPropertyChanged(nameof(CircularityLapProgress));
        OnPropertyChanged(nameof(IsCircularityCompleted));
        OnPropertyChanged(nameof(CircularityVerdict));
        OnPropertyChanged(nameof(CircularityRatingBrush));
        OnPropertyChanged(nameof(CircularityQualityTag));
        OnPropertyChanged(nameof(CircularityStatusDescription));
        OnPropertyChanged(nameof(CircularityCornerGatingText));
        OnPropertyChanged(nameof(CircularityCornerGatingBrush));
        OnPropertyChanged(nameof(CircularityRestDriftText));
        OnPropertyChanged(nameof(CircularityProfileType));
        OnPropertyChanged(nameof(IsCircularityRunning));
    }

    private void SetLedPreset(string? color)
    {
        switch (color?.ToLowerInvariant())
        {
            case "blue":
            case "azul":
                _ledR = 0; _ledG = 0; _ledB = 255;
                break;
            case "red":
            case "rojo":
                _ledR = 255; _ledG = 0; _ledB = 0;
                break;
            case "green":
            case "verde":
                _ledR = 0; _ledG = 255; _ledB = 0;
                break;
            case "cyan":
            case "cian":
                _ledR = 0; _ledG = 255; _ledB = 255;
                break;
            case "purple":
            case "morado":
                _ledR = 255; _ledG = 0; _ledB = 255;
                break;
            case "yellow":
            case "amarillo":
                _ledR = 255; _ledG = 255; _ledB = 0;
                break;
            case "orange":
            case "naranja":
                _ledR = 255; _ledG = 80; _ledB = 0;
                break;
            case "white":
            case "blanco":
                _ledR = 255; _ledG = 255; _ledB = 255;
                break;
            case "off":
            case "apagar":
                _ledR = 0; _ledG = 0; _ledB = 0;
                break;
        }

        _manager.LedR = _ledR;
        _manager.LedG = _ledG;
        _manager.LedB = _ledB;

        OnPropertyChanged(nameof(LedR));
        OnPropertyChanged(nameof(LedG));
        OnPropertyChanged(nameof(LedB));
        OnPropertyChanged(nameof(CurrentLedBrush));

        _manager.SendOutputNow();
    }
}
