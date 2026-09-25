using System.Windows;

namespace PsCloneTester.Services;

public class CircularityTester
{
    public const int SectorsCount = 72; // 360° / 5° = 72 sectores angulares
    private readonly double[] _sectorRadii = new double[SectorsCount];
    private readonly bool[] _sectorVisited = new bool[SectorsCount];
    private readonly List<Point> _samplePoints = new();
    private readonly object _lock = new();

    private double _prevX = 0;
    private double _prevY = 0;
    private bool _hasPrev = false;
    private double _cumulativeAngle = 0.0;

    public bool IsRunning { get; private set; } = false;
    public bool IsCompleted { get; private set; } = false;

    public double[] SectorRadii => (double[])_sectorRadii.Clone();
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

    public double AverageErrorPercent { get; private set; } = 0.0;
    public double MaxRadius { get; private set; } = 0.0;
    public double MinRadius { get; private set; } = 0.0;
    public double CoveragePercent { get; private set; } = 0.0;
    public double LapProgressPercent { get; private set; } = 0.0;
    public string RatingVerdict { get; private set; } = "Listo para iniciar (1 vuelta necesaria)";
    public string RatingColor { get; private set; } = "#8898AA";

    public void Start()
    {
        Reset();
        IsRunning = true;
    }

    public void Stop()
    {
        IsRunning = false;
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
            IsCompleted = false;
            AverageErrorPercent = 0;
            MaxRadius = 0;
            MinRadius = 0;
            CoveragePercent = 0;
            LapProgressPercent = 0;
            RatingVerdict = "Listo - Gire la palanca 1 vuelta (360°)";
            RatingColor = "#8898AA";
        }
    }

    public void AddSample(double x, double y)
    {
        if (!IsRunning || IsCompleted) return;

        double r = Math.Sqrt(x * x + y * y);

        // Si el stick está cerca del centro (zona muerta / reposo), no registrar como contorno exterior
        if (r < 0.50)
        {
            _hasPrev = false;
            return;
        }

        double angle = Math.Atan2(y, x);
        if (angle < 0) angle += 2.0 * Math.PI;

        lock (_lock)
        {
            if (_hasPrev)
            {
                double prevAngle = Math.Atan2(_prevY, _prevX);
                if (prevAngle < 0) prevAngle += 2.0 * Math.PI;

                double prevR = Math.Sqrt(_prevX * _prevX + _prevY * _prevY);

                // Calcular delta de ángulo más corto en el círculo
                double delta = angle - prevAngle;
                while (delta > Math.PI) delta -= 2.0 * Math.PI;
                while (delta < -Math.PI) delta += 2.0 * Math.PI;

                // Solo interpolar si es un movimiento continuo (< 120° entre frames)
                if (Math.Abs(delta) < (2.0 * Math.PI / 3.0))
                {
                    _cumulativeAngle += Math.Abs(delta);

                    // Pasos de interpolación para garantizar que NINGÚN sector quede vacío
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

            // Guardar puntos para scatter
            if (_samplePoints.Count < 500)
            {
                _samplePoints.Add(new Point(x, y));
            }
            else
            {
                _samplePoints.RemoveAt(0);
                _samplePoints.Add(new Point(x, y));
            }

            CalculateMetrics();

            // Auto-completar y bloquear el test en exactamente UNA vuelta completa (360° o cobertura >= 92%)
            if (!IsCompleted && (_cumulativeAngle >= (2.0 * Math.PI * 0.95) || CoveragePercent >= 92.0))
            {
                IsCompleted = true;
                IsRunning = false; // Detener automáticamente para que soltar el stick no altere los datos
                CalculateMetrics();
            }
        }
    }

    public double[] GetSmoothedContour(bool smoothAll = false)
    {
        lock (_lock)
        {
            int visitedCount = 0;
            for (int i = 0; i < SectorsCount; i++)
            {
                if (_sectorVisited[i] && _sectorRadii[i] > 0.3)
                    visitedCount++;
            }

            // Si casi no hay muestras, no dibujar nada
            if (visitedCount < 3)
            {
                return Array.Empty<double>();
            }

            double[] contour = new double[SectorsCount];

            // Si el test está en curso y no se pide suavizado completo, solo devolver sectores visitados
            // para que no cierre la figura en falso antes de dar la vuelta
            for (int i = 0; i < SectorsCount; i++)
            {
                if (_sectorVisited[i] && _sectorRadii[i] > 0.3)
                {
                    contour[i] = _sectorRadii[i];
                }
                else if (smoothAll || IsCompleted)
                {
                    // Interpolar entre vecinos para cerrar huecos al finalizar el test
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
        double sumError = 0;
        double maxR = 0;
        double minR = double.MaxValue;

        for (int i = 0; i < SectorsCount; i++)
        {
            if (_sectorVisited[i] && _sectorRadii[i] > 0.3)
            {
                double r = _sectorRadii[i];
                validCount++;
                sumError += Math.Abs(r - 1.0);
                if (r > maxR) maxR = r;
                if (r < minR) minR = r;
            }
        }

        CoveragePercent = Math.Round((validCount / (double)SectorsCount) * 100.0, 0);
        LapProgressPercent = Math.Min(100.0, Math.Round((_cumulativeAngle / (2.0 * Math.PI)) * 100.0, 0));

        if (IsCompleted)
        {
            CoveragePercent = 100.0;
            LapProgressPercent = 100.0;
        }

        if (validCount >= 8)
        {
            AverageErrorPercent = Math.Round((sumError / validCount) * 100.0, 1);
            MaxRadius = Math.Round(maxR, 2);
            MinRadius = Math.Round(minR, 2);

            string quality;
            if (AverageErrorPercent < 7.5)
            {
                quality = "Excelente (Precisión de Mando Original)";
                RatingColor = "#00E599";
            }
            else if (AverageErrorPercent < 14.0)
            {
                quality = "Buena (Calidad Estándar)";
                RatingColor = "#00C3FF";
            }
            else if (AverageErrorPercent < 20.0)
            {
                quality = "Aceptable (Típico Mando Imitación)";
                RatingColor = "#FFB300";
            }
            else
            {
                quality = "Deficiente / Cuadrado (Clon sin filtro circular)";
                RatingColor = "#FF3355";
            }

            if (IsCompleted || CoveragePercent >= 92.0 || LapProgressPercent >= 95.0)
            {
                RatingVerdict = $"✅ Test Completado (1 Vuelta) - {quality}";
            }
            else
            {
                RatingVerdict = $"Midiendo 1 vuelta... ({LapProgressPercent:F0}%) - {quality}";
            }
        }
        else
        {
            RatingVerdict = $"Gire el stick 1 vuelta ({LapProgressPercent:F0}%)...";
            RatingColor = "#00C3FF";
        }
    }
}
