# Control - Probador y Diagnóstico de Mandos PS5 / PS4 & Imitaciones 🎮

Aplicación profesional en **C# (.NET 10 / WPF)** diseñada para realizar un diagnóstico completo, medición de sensores y pruebas de hardware en controles **DualSense (PS5)**, **DualShock 4 (PS4)** y especialmente **mandos réplica / clon / imitación**.

---

## 🚀 Capacidades y Módulos de Prueba

### 1. 🎯 Medición de Acelerómetro y Giroscopio (IMU de 6 Ejes)
* **Horizonte Artificial 3D Interactivo**: Visualizador de actitud esférica que se inclina y rota en tiempo real según el movimiento físico del mando (ángulos de *Pitch* y *Roll* calculados con un filtro complementario de fusión sensorial).
* **Medición de Fuerza G (Acelerómetro)**: Lecturas en unidades físicas ($G$) y valores brutos de 16 bits para los ejes $X$, $Y$, $Z$ y cálculo de la magnitud total de aceleración.
* **Velocidad Angular (Giroscopio)**: Medición de rotación en grados por segundo (°/s) para *Pitch*, *Yaw* y *Roll*.
* **Osciloscopio Dinámico en Vivo**: Gráfico continuo de onda que permite evaluar la estabilidad de la señal, vibración involuntaria o temblores de los sensores.
* **Calibración a Cero (Tare)**: Botón de un solo clic para compensar desviaciones de fábrica (*drift*) y calibrar el plano horizontal.

### 2. ⚡ Gatillos Adaptativos (DualSense Adaptive Triggers)
* **Monitoreo Analógico Preciso**: Medidores de recorrido vertical en tiempo real para **L2** y **R2** (0 a 255 y porcentaje 0% a 100%).
* **Generador de Efectos Hápticos HID (Reportes 0x02 USB / 0x31 Bluetooth)**:
  * **Resistencia Continua (Rigid)**: Aplica una resistencia uniforme constante.
  * **Gatillo Pesado (Heavy Stop)**: Crea un tope mecánico resistente a cierta profundidad del recorrido.
  * **Pulsación Rítmica (Pulse)**: Emite tramos pulsantes de resistencia con frecuencia ajustable.
  * **Efecto Arco (Bow / Weapon)**: Tensión elástica progresiva simulando el tensado de una cuerda o gatillo de arma.
  * **Metralleta / Retroceso (Machine Gun / Galloping)**: Emulación de ráfagas automáticas y vibración en el gatillo.
  * **Calibración de Motores (Cycle)**: Ciclo de barrido para probar el mecanismo de engranajes.
* **Control Independiente o Vinculado**: Ajuste de fuerza (0-255), posición de inicio, posición final y frecuencia para L2 y R2 por separado.
* **Bucle Continuo (Keep Alive)**: Mantiene los efectos activos de forma ininterrumpida mientras se prueba el mando.

### 3. 👆 Pantalla Táctil (Touchpad / Trackpad)
* **Superficie Táctil Interactiva en Alta Resolución**: Representación gráfica proporcional a la zona táctil del mando (~1920 × 942).
* **Multi-Touch Dual**:
  * **Dedo 1 (Azul Neón)**: Estado activo, ID de contacto, coordenadas $X$ e $Y$.
  * **Dedo 2 (Naranja Neón)**: Seguimiento simultáneo del segundo punto de contacto.
* **Modo Trazo (Lienzo para Detección de Zonas Muertas)**: Dibuja la trayectoria continua del dedo sobre la pantalla para verificar linealidad, saltos bruscos o zonas ciegas comunes en chips clon.
* **Detección de Clic Físico**: Indicador visual reactivo cuando se presiona físicamente hacia abajo el trackpad.

### 4. ⭕ Test de Circularidad de Palancas Analógicas (Benchmark)
* **Benchmark de Precisión Radial**: Evalúa la geometría del movimiento del joystick respecto al círculo unitario ideal ($r = 1.0$).
* **Detección de Deformación en Esquinas (Corner Gating)**: Identifica si el joystick produce una trayectoria cuadrada con saturación en diagonales típica de mandos réplica de bajo costo.
* **Métricas en Tiempo Real**:
  * **Error Promedio (%)**: Cálculo acumulativo sobre 72 sectores angulares ($5^\circ$ cada uno).
  * **Veredicto de Calidad**: Excelente (<8%), Buena (8%-12%), Aceptable (12%-18%) o Deficiente (>18%).
  * **Cobertura Angular**: Muestra el porcentaje de los 360° explorados durante la prueba.
  * **Radios Máximo y Mínimo**: Registro de elongación y descalibración radial.
* **Visualizador Gráfico con Scatter y Polígono**: Renderiza el círculo de referencia ideal, el contorno medido y el cursor dinámico de posición.

### 5. 🎮 Diagnóstico General y Motores de Vibración
* **Palancas Analógicas (Sticks L y R)**: Controles circulares interactivos con indicador de zona muerta y clics **L3** y **R3**.
* **Cruceta Digital (D-Pad)**: Detección independiente de Arriba, Abajo, Izquierda, Derecha y diagonales.
* **Botones de Acción**: Cuadrado (□), Cruz (✕), Círculo (○) y Triángulo (△) con retroalimentación visual al presionar.
* **Bumpers y Gatillos Digitales**: L1, R1, L2 (interruptor digital), R2 (interruptor digital).
* **Botones Especiales**: Share / Create, Options, Botón PS y Silenciador de Micrófono (Mic Mute).
* **Test de Vibración Robusto (HID + XInput Multi-Protocolo)**:
  * Motor izquierdo (pesado / bajas frecuencias) y motor derecho (ligero / altas frecuencias).
  * Soporta flag oficial `0x04` (`DS_OUTPUT_VALID_FLAG2_COMPATIBLE_VIBRATION2`) y fallback a DualShock 4 report 0x05 para clones.
  * Compatibilidad integrada con XInput para mandos réplica que conmutan a modo PC en Windows.
  * Botones de "📳 Probar 1.5s", "⚡ Continuo" y "🛑 Apagar".
* **Barra de Luz LED RGB**: Selector de color interactivo con sliders RGB y paleta de colores rápidos (Azul PlayStation, Rojo, Verde, Cian, Morado, Naranja, Blanco y Apagar).

### 6. 🔍 Inspector Hexadecimal de Paquetes Raw HID
* Volcado de bytes en hexadecimal en tiempo real de los reportes entrantes.
* Medidor de frecuencia de sondeo (*Polling Rate*) en Hertz (**Hz**) y contador acumulado de paquetes.

---

## 🛠️ Cómo Compilar y Ejecutar

### Requisitos
* Windows 10 o Windows 11 (64 bits).
* .NET SDK 8 o .NET SDK 10 instalado.

### Ejecución directa desde terminal
```powershell
# Compilar el proyecto
dotnet build

# Ejecutar la aplicación con interfaz gráfica
dotnet run

# O ejecutar el escáner rápido por consola
./bin/Debug/net10.0-windows/Control.exe --scan
```

### Ejecutar desde el Explorador de Archivos
Basta con hacer doble clic en:
`bin\Debug\net10.0-windows\Control.exe`

---

## 💡 Guía para Mandos Clon / Réplica

1. **Conexión recomendada**:
   * Para probar los **gatillos adaptativos** de réplicas de PS5, es muy recomendable conectar el mando por **cable USB**, ya que muchos firmwares clon desactivan los reportes hápticos extendidos por Bluetooth en Windows.
2. **Detección del mando**:
   * Si su mando no aparece en la lista desplegable inicial, marque la casilla **"Ver Todos"** y pulse **"🔄 Refrescar"**. Esto mostrará cualquier dispositivo HID conectado para que pueda seleccionarlo manualmente.
3. **Selector de Protocolo**:
   * **Auto-detect**: Detecta automáticamente si el reporte coincide con DualSense o DualShock 4.
   * **DualSense**: Seleccione este modo si su mando es una copia de PS5 o tiene forma de DualSense.
   * **DualShock 4**: Seleccione este modo si es una réplica de PS4.
   * **Generic HID**: Para mandos que reportan datos en formato DirectInput estándar.
4. **¿Cómo saber si el clon tiene gatillos adaptativos reales?**:
   * Configure el efecto en **"Resistencia Continua (Rigid)"** con fuerza en **220** o más y pulse **"Enviar Efecto"**.
   * Si al apretar el gatillo siente resistencia mecánica variable en el recorrido del dedo, el mando cuenta con **motores servo reales**.
   * Si en lugar de resistencia mecánica el mando comienza a vibrar, el chip de la imitación emula la señal redirigiéndola a los motores de vibración normales.