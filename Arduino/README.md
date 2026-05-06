# Arduino

Sketch: `MixerController/MixerController.ino`

Sendet Serial-Frames im Format:

```text
MIXER,<seq>,<ch1>,<ch2>,<ch3>,<ch4>
```

Kein Handshake zwingend. App kann Stream direkt lesen.
