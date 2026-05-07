# Audio Mixer / Mixer Controller

Neubau mit 2 Top-Level-Ordnern:

- `Arduino/` -> MCU liest Potis und sendet Serial-Frames
- `App/` -> Windows-App empfängt Frames, mappt auf Audio-Devices, setzt Lautstärke

## Zielbild

- Arduino liest ADC-Werte
- App erkennt COM-Port
- App liest Frames robust
- App glättet Werte, rechnet auf Prozent
- App steuert Windows-Audio
- App speichert Config und Presets
- App zeigt Live-UI und Monitor-Fenster

## Technische Basis

- `App`: C# / .NET 8 / WPF
- `Arduino`: Standard Arduino Sketch
- Serial-Format: textbasiert, parserfreundlich, ohne Handshake-Zwang
