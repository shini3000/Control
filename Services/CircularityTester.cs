using System.Windows;

namespace PsCloneTester.Services;

public class CircularityTester
{
    public const int SectorsCount = 64; // 64 sectores angulares (360° / 64 = 5.625° por sector)
    private readonly double[] _sectorRadii = new double[SectorsCount];
    private readonly bool[] _sectorVisited = new bool[SectorsCount];
    private readonly List<Point> _samplePoints = new();
    private readonly object _lock = new();

    private double _prevX = 0;
    private double _prevY = 0;
    private bool _hasPrev = false;
    private double _cumulativeAngle = 0.0;

    // Reposo / Drift de Centrado
    private double _restSumX = 0;
    private double _restSumY = 0;
    private int _restCount = 0;

    public bool IsRunning { get; private set; } = false;
    public bool IsCompleted { get; private set; } = false;
    public bool HasMinimumLap { get; private set; } = false;

    public double[] SectorRadii
    {
        get
        {
            lock (_lock)
            {
                return (double[])_sectorRadii.Clone();
            }
        }
    }

    public List<Point> SamplePoints
    {
        get
        {
            lock (_lock)
            {
                return new List<Point>(_samplePoints);
            }
        }
    }

    // Métricas según estándar de la industria (Gamepad-Tester RMSE)
    public double AverageErrorPercent { get; private set; } = 0.0; // RMSE
    public double MaxRadius { get; private set; } = 0.0;
    public double MinRadius { get; private set; } = 0.0;
    public double CoveragePercent { get; private set; } = 0.0;
    public double LapProgressPercent { get; private set; } = 0.0;
    public double LapCount => Math.Round(_cumulativeAngle / (2.0 * Math.PI), 1);

    // Diagnósticos reales de hardware
    public double CornerGatingPercent { get; private set; } = 0.0; // Diferencia porcentual diagonal vs cardinal
    public double RestDriftPercent { get; private set; } = 0.0;     // Distancia del centro en reposo (%)
    public double RestX { get; private set; } = 0.0;
    public double RestY { get; private set; } = 0.0;

    public string QualityTag { get; private set; } = "LISTO";
    public string StatusDescription { get; private set; } = "Listo: Pulse Iniciar y gire el stick despacio al borde.";
    public string RatingColor { get; private set; } = "#00C3FF";
    public string ProfileType { get; private set; } = "Pendiente de Medición";
    public string CornerGatingText { get; private set; } = "--";
    public string CornerGatingColor { get; private set; } = "#8898AA";
    public string RestDriftText { get; private set; } = "X: 0.00, Y: 0.00";

    public void Start()
    {
        Reset();
        IsRunning = true;
        StatusDescription = "Gire el stick 1 o 2 vueltas rozando el borde exterior...";
        QualityTag = "MIDIENDO";
        RatingColor = "#00C3FF";
    }

    public void Stop()
    {
        lock (_lock)
        {
            IsRunning = false;
            IsCompleted = true;
            CalculateMetrics();
            if (CoveragePercent < 50.0)
            {
                StatusDescription = $"Test detenido con poca cobertura ({CoveragePercent:F0}%). Se recomienda completar 360°.";
            }
            else
            {
                StatusDescription = $"Test completado con {CoveragePercent:F0}% de cobertura. Calidad: {QualityTag}.";
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_sectorRadii, 0, SectorsCount);
            Array.Clear(_sectorVisited, 0, SectorsCount);
            _samplePoints.Clear();
            _hasPrev = false;
            _prevX = 0;
            _prevY = 0;
            _cumulativeAngle = 0.0;
            _restSumX = 0;
            _restSumY = 0;
            _restCount = 0;

            IsRunning = false;
            IsCompleted = false;
            HasMinimumLap = false;

            AverageErrorPercent = 0.0;
            MaxRadius = 0.0;
            MinRadius = 0.0;
            CoveragePercent = 0.0;
            LapProgressPercent = 0.0;
            CornerGatingPercent = 0.0;
            RestDriftPercent = 0.0;
            RestX = 0.0;
            RestY = 0.0;

            QualityTag = "LISTO";
            StatusDescription = "Listo: Pulse Iniciar y gire el stick despacio al borde.";
            RatingColor = "#00C3FF";
            ProfileType = "Pendiente de Medición";
            CornerGatingText = "--";
            CornerGatingColor = "#8898AA";
            RestDriftText = "X: 0.00, Y: 0.00";
        }
    }

    public void AddSample(double x, double y)
    {
        if (!IsRunning) return;

        double r = Math.Sqrt(x * x + y * y);

        lock (_lock)
        {
            // Registrar punto de reposo si está cerca del centro (< 0.25)
            if (r < 0.25)
            {
                _restSumX += x;
                _restSumY += y;
                _restCount++;
                RestX = Math.Round(_restSumX / _restCount, 3);
                RestY = Math.Round(_restSumY / _restCount, 3);
                RestDriftPercent = Math.Round(Math.Sqrt(RestX * RestX + RestY * RestY) * 100.0, 1);
                RestDriftText = $"X: {RestX:+0.000;-0.000;0.000}, Y: {RestY:+0.000;-0.000;0.000} ({RestDriftPercent:F1}%)";
                _hasPrev = false;
                return;
            }

            // A partir de r >= 0.35 se considera movimiento activo hacia el borde
            double angle = Math.Atan2(y, x);
            if (angle < 0) angle += 2.0 * Math.PI;

            if (_hasPrev)
            {
                double prevAngle = Math.Atan2(_prevY, _prevX);
                if (prevAngle < 0) prevAngle += 2.0 * Math.PI;
                double prevR = Math.Sqrt(_prevX * _prevX + _prevY * _prevY);

                double delta = angle - prevAngle;
                while (delta > Math.PI) delta -= 2.0 * Math.PI;
                while (delta < -Math.PI) delta += 2.0 * Math.PI;

                if (Math.Abs(delta) < (2.0 * Math.PI / 3.0)) // < 120° entre muestras
                {
                    _cumulativeAngle += Math.Abs(delta);

                    int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (2.0 * Math.PI / SectorsCount)) * 4);
                    for (int s = 0; s <= steps; s++)
                    {
                        double t = (double)s / steps;
                        double interpAngle = prevAngle + delta * t;
                        while (interpAngle < 0) interpAngle += 2.0 * Math.PI;
                        while (interpAngle >= 2.0 * Math.PI) interpAngle -= 2.0 * Math.PI;

                        double interpR = prevR + (r - prevR) * t;
                        int sec = (int)(interpAngle / (2.0 * Math.PI / SectorsCount)) % SectorsCount;

                        if (interpR > _sectorRadii[sec])
                        {
                            _sectorRadii[sec] = interpR;
                        }
                        _sectorVisited[sec] = true;
                    }
                }
                else
                {
                    int sec = (int)(angle / (2.0 * Math.PI / SectorsCount)) % SectorsCount;
                    if (r > _sectorRadii[sec]) _sectorRadii[sec] = r;
                    _sectorVisited[sec] = true;
                }
            }
            else
            {
                int sec = (int)(angle / (2.0 * Math.PI / SectorsCount)) % SectorsCount;
                if (r > _sectorRadii[sec]) _sectorRadii[sec] = r;
                _sectorVisited[sec] = true;
            }

            _prevX = x;
            _prevY = y;
            _hasPrev = true;

            // Historial de puntos para la nube de muestras
            if (_samplePoints.Count < 600)
            {
                _samplePoints.Add(new Point(x, y));
            }
            else
            {
                _samplePoints.RemoveAt(0);
                _samplePoints.Add(new Point(x, y));
            }

            CalculateMetrics();

            // Marca que la vuelta mínima está cubierta, pero NO detiene forzadamente la prueba
            // para permitir al usuario dar 2 o 3 vueltas suaves perfeccionando el contorno
            if (!HasMinimumLap && (_cumulativeAngle >= (2.0 * Math.PI * 0.95) || CoveragePercent >= 92.0))
            {
                HasMinimumLap = true;
            }
        }
    }

    public double[] GetSmoothedContour()
    {
        lock (_lock)
        {
            int visitedCount = 0;
            for (int i = 0; i < SectorsCount; i++)
            {
                if (_sectorVisited[i] && _sectorRadii[i] > 0.3)
                    visitedCount++;
            }

            if (visitedCount < 3)
            {
                return Array.Empty<double>();
            }

            double[] contour = new double[SectorsCount];

            for (int i = 0; i < SectorsCount; i++)
            {
                if (_sectorVisited[i] && _sectorRadii[i] > 0.3)
                {
                    contour[i] = _sectorRadii[i];
                }
                else if (HasMinimumLap || IsCompleted || visitedCount >= (SectorsCount * 0.70))
                {
                    // Interpolar entre sectores visitados adyacentes para cerrar huecos
                    int prevIdx = -1;
                    int distPrev = 0;
                    for (int d = 1; d < SectorsCount; d++)
                    {
                        int check = (i - d + SectorsCount) % SectorsCount;
                        if (_sectorVisited[check] && _sectorRadii[check] > 0.3)
                        {
                            prevIdx = check;
                            distPrev = d;
                            break;
                        }
                    }

                    int nextIdx = -1;
                    int distNext = 0;
                    for (int d = 1; d < SectorsCount; d++)
                    {
                        int check = (i + d) % SectorsCount;
                        if (_sectorVisited[check] && _sectorRadii[check] > 0.3)
                        {
                            nextIdx = check;
                            distNext = d;
                            break;
                        }
                    }

                    if (prevIdx >= 0 && nextIdx >= 0)
                    {
                        double weight = (double)distPrev / (distPrev + distNext);
                        contour[i] = _sectorRadii[prevIdx] * (1.0 - weight) + _sectorRadii[nextIdx] * weight;
                    }
                    else if (prevIdx >= 0)
                    {
                        contour[i] = _sectorRadii[prevIdx];
                    }
                    else if (nextIdx >= 0)
                    {
                        contour[i] = _sectorRadii[nextIdx];
                    }
                    else
                    {
                        contour[i] = 1.0;
                    }
                }
                else
                {
                    contour[i] = 0.0;
                }
            }

            return contour;
        }
    }

    private void CalculateMetrics()
    {
        int validCount = 0;
        double sumSqError = 0.0;
        double maxR = 0.0;
        double minR = double.MaxValue;

        // Sectores cardinales (0°, 90°, 180°, 270°) y diagonales (45°, 135°, 225°, 315°)
        double sumCard = 0.0;
        int countCard = 0;
        double sumDiag = 0.0;
        int countDiag = 0;

        for (int i = 0; i < SectorsCount; i++)
        {
            if (_sectorVisited[i] && _sectorRadii[i] > 0.3)
            {
                double r = _sectorRadii[i];
                validCount++;

                // FÓRMULA OFICIAL GAMEPAD-TESTER: Root Mean Square Error (RMSE)
                double diff = r - 1.0;
                sumSqError += diff * diff;

                if (r > maxR) maxR = r;
                if (r < minR) minR = r;

                // Identificar si el sector está en ejes cardinales o diagonales
                // En 64 sectores: paso = 5.625°
                // Cardinales: 0 (0°), 16 (90°), 32 (180°), 48 (270°) +/- 1 sector
                // Diagonales: 8 (45°), 24 (135°), 40 (225°), 56 (315°) +/- 1 sector
                int secDistCard = Math.Min(Math.Min(Math.Abs(i - 0), Math.Abs(i - 64)),
                                  Math.Min(Math.Abs(i - 16),
                                  Math.Min(Math.Abs(i - 32), Math.Abs(i - 48))));
                int secDistDiag = Math.Min(Math.Abs(i - 8),
                                  Math.Min(Math.Abs(i - 24),
                                  Math.Min(Math.Abs(i - 40), Math.Abs(i - 56))));

                if (secDistCard <= 1)
                {
                    sumCard += r;
                    countCard++;
                }
                else if (secDistDiag <= 1)
                {
                    sumDiag += r;
                    countDiag++;
                }
            }
        }

        CoveragePercent = Math.Round((validCount / (double)SectorsCount) * 100.0, 0);
        LapProgressPercent = Math.Min(100.0, Math.Round((_cumulativeAngle / (2.0 * Math.PI)) * 100.0, 0));

        if (validCount >= 6)
        {
            // RMSE oficial de Gamepad-Tester
            AverageErrorPercent = Math.Round(Math.Sqrt(sumSqError / validCount) * 100.0, 1);
            MaxRadius = Math.Round(maxR, 2);
            MinRadius = Math.Round(minR, 2);

            // Medición de Deformación en Diagonales (Corner Gating / Squaring)
            if (countCard > 0 && countDiag > 0)
            {
                double cardAvg = sumCard / countCard;
                double diagAvg = sumDiag / countDiag;
                CornerGatingPercent = Math.Round(((diagAvg - cardAvg) / cardAvg) * 100.0, 1);

                if (CornerGatingPercent <= 6.0)
                {
                    CornerGatingText = $"{CornerGatingPercent:+0.0;-0.0;0.0}% (Circular OEM)";
                    CornerGatingColor = "#00E599";
                }
                else if (CornerGatingPercent <= 14.0)
                {
                    CornerGatingText = $"{CornerGatingPercent:+0.0;-0.0;0.0}% (Octogonal)";
                    CornerGatingColor = "#00C3FF";
                }
                else
                {
                    CornerGatingText = $"{CornerGatingPercent:+0.0;-0.0;0.0}% (Cuadrado / Clon)";
                    CornerGatingColor = "#FF3355";
                }
            }

            // Calificación según criterios oficiales
            if (AverageErrorPercent < 8.0)
            {
                QualityTag = "EXCELENTE";
                RatingColor = "#00E599";
                ProfileType = "Filtro Circular Original (Hall Effect / OEM)";
            }
            else if (AverageErrorPercent < 14.0)
            {
                QualityTag = "BUENA";
                RatingColor = "#00C3FF";
                ProfileType = "Mando Estándar (Calibración Normal)";
            }
            else if (AverageErrorPercent < 20.0)
            {
                QualityTag = "ACEPTABLE";
                RatingColor = "#FFB300";
                ProfileType = "Típico Clon con Ligera Deformación";
            }
            else
            {
                QualityTag = "DEFICIENTE";
                RatingColor = "#FF3355";
                ProfileType = "Salida Cuadrada (Clon sin filtro circular)";
            }

            if (!IsRunning)
            {
                StatusDescription = $"Test finalizado ({CoveragePercent:F0}% cobertura). Calidad: {QualityTag}.";
            }
            else if (HasMinimumLap)
            {
                StatusDescription = $"✅ Vuelta completa ({LapCount:F1} vueltas). Siga girando o pulse Detener.";
            }
            else
            {
                StatusDescription = $"Midiendo en vivo: {CoveragePercent:F0}% cubierto ({LapCount:F1} vueltas)...";
            }
        }
    }
}
