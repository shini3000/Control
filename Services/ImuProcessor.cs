using PsCloneTester.Models;

namespace PsCloneTester.Services;

public class ImuProcessor
{
    private short _accelOffsetX = 0;
    private short _accelOffsetY = 0;
    private short _accelOffsetZ = 0; // Default 1G downwards

    private short _gyroOffsetPitch = 0;
    private short _gyroOffsetYaw = 0;
    private short _gyroOffsetRoll = 0;

    private double _estimatedPitch = 0;
    private double _estimatedRoll = 0;
    private double _estimatedYaw = 0;

    private DateTime _lastUpdate = DateTime.UtcNow;
    private const double Alpha = 0.96; // Complementary filter constant

    public bool IsCalibrated { get; private set; }

    public void Calibrate(short rawAx, short rawAy, short rawAz, short rawGx, short rawGy, short rawGz)
    {
        _accelOffsetX = rawAx;
        _accelOffsetY = rawAy;
        // Z axis usually holds 1G (~8192 LSB) when lying flat
        _accelOffsetZ = (short)(rawAz - 8192);

        _gyroOffsetPitch = rawGx;
        _gyroOffsetYaw = rawGy;
        _gyroOffsetRoll = rawGz;

        _estimatedPitch = 0;
        _estimatedRoll = 0;
        _estimatedYaw = 0;
        IsCalibrated = true;
    }

    public void ResetCalibration()
    {
        _accelOffsetX = 0;
        _accelOffsetY = 0;
        _accelOffsetZ = 0;
        _gyroOffsetPitch = 0;
        _gyroOffsetYaw = 0;
        _gyroOffsetRoll = 0;
        IsCalibrated = false;
    }

    public void Process(ControllerState state)
    {
        // Aplicar offsets de calibración
        short ax = (short)(state.RawAccelX - _accelOffsetX);
        short ay = (short)(state.RawAccelY - _accelOffsetY);
        short az = (short)(state.RawAccelZ - _accelOffsetZ);

        short gx = (short)(state.RawGyroPitch - _gyroOffsetPitch);
        short gy = (short)(state.RawGyroYaw - _gyroOffsetYaw);
        short gz = (short)(state.RawGyroRoll - _gyroOffsetRoll);

        DateTime now = DateTime.UtcNow;
        double dt = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        if (dt <= 0 || dt > 0.2)
        {
            dt = 0.004; // Asumir ~250 Hz por defecto
        }

        // Conversión a unidades físicas
        double axG = ax / 8192.0;
        double ayG = ay / 8192.0;
        double azG = az / 8192.0;

        double gxDegS = gx / 16.4;
        double gyDegS = gy / 16.4;
        double gzDegS = gz / 16.4;

        // Inclinación por gravedad del acelerómetro
        double norm = Math.Sqrt(axG * axG + ayG * ayG + azG * azG);
        if (norm > 0.1)
        {
            double pitchAcc = Math.Atan2(ayG, Math.Sqrt(axG * axG + azG * azG)) * (180.0 / Math.PI);
            double rollAcc = Math.Atan2(-axG, azG) * (180.0 / Math.PI);

            // Filtro complementario (combina agilidad del giróscopo con estabilidad del acelerómetro)
            _estimatedPitch = Alpha * (_estimatedPitch + gxDegS * dt) + (1.0 - Alpha) * pitchAcc;
            _estimatedRoll = Alpha * (_estimatedRoll + gzDegS * dt) + (1.0 - Alpha) * rollAcc;
        }
        else
        {
            _estimatedPitch += gxDegS * dt;
            _estimatedRoll += gzDegS * dt;
        }

        _estimatedYaw += gyDegS * dt;

        // Restringir rangos
        state.EstimatedPitchDeg = Math.Clamp(_estimatedPitch, -90.0, 90.0);
        state.EstimatedRollDeg = Math.Clamp(_estimatedRoll, -180.0, 180.0);
        state.EstimatedYawDeg = (_estimatedYaw % 360.0);
    }
}
