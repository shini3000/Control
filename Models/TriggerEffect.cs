namespace PsCloneTester.Models;

public enum TriggerEffectMode
{
    Off = 0x00,
    Rigid = 0x01,        // Resistencia continua estándar
    Pulse = 0x02,        // Resistencia con pulsaciones
    Rigid_A = 0x21,
    Rigid_B = 0x05,
    Rigid_AB = 0x25,
    Pulse_A = 0x22,
    Pulse_B = 0x04,
    Pulse_AB = 0x26,
    Bow = 0x20,          // Simulación de arco tensado
    Galloping = 0x06,    // Ametralladora / traqueteo
    HardStop = 0x29,     // Muro duro (mapea a 0x01 en hardware)
    Calibration = 0xFC   // Ciclo de calibración interna de los motores
}

public class TriggerConfig
{
    public TriggerEffectMode Mode { get; set; } = TriggerEffectMode.Off;
    public byte StartPosition { get; set; } = 0;       // Inicio del efecto (0 - 255)
    public byte EndPosition { get; set; } = 255;       // Fin del efecto
    public byte Force { get; set; } = 255;             // Fuerza de resistencia (0 - 255)
    public byte Frequency { get; set; } = 10;          // Frecuencia para modos pulsantes / vibración

    // Parámetros crudos para el paquete DualSense (7 bytes de fuerza)
    public byte[] GetForcesPayload()
    {
        byte[] forces = new byte[7];

        switch (Mode)
        {
            case TriggerEffectMode.Off:
                Array.Clear(forces, 0, 7);
                break;

            case TriggerEffectMode.Rigid:
                forces[0] = StartPosition;
                forces[1] = Force;
                break;

            case TriggerEffectMode.Pulse:
                forces[0] = StartPosition;
                forces[1] = Force;
                forces[2] = Frequency;
                break;

            case TriggerEffectMode.Bow:
                forces[0] = StartPosition;
                forces[1] = EndPosition;
                forces[2] = Force;
                forces[3] = (byte)Math.Clamp(Force / 2, 0, 255);
                break;

            case TriggerEffectMode.Galloping:
                forces[0] = StartPosition;
                forces[1] = EndPosition;
                forces[2] = Frequency;
                forces[3] = Force;
                break;

            case TriggerEffectMode.HardStop:
                forces[0] = StartPosition; // e.g., tope a mitad de recorrido
                forces[1] = 255;           // Máxima resistencia
                break;

            case TriggerEffectMode.Calibration:
                forces[0] = 0xFC;
                break;

            default:
                forces[0] = StartPosition;
                forces[1] = Force;
                forces[2] = Frequency;
                break;
        }

        return forces;
    }
}
