// Audio Mixer Controller
// Format: MIXER,<seq>,<ch1>,<ch2>,<ch3>,<ch4>,<ch5>

const int numInputs = 5;
const int inputPins[] = { A0, A1, A2, A3, A4 };
const unsigned long frameIntervalMs = 10;

unsigned long lastFrameAt = 0;
unsigned long sequence = 0;

char serialBuf[64];
int serialBufPos = 0;

void setup() {
  Serial.begin(115200);

  for (int i = 0; i < numInputs; i++) {
    pinMode(inputPins[i], INPUT);
  }

  delay(1000);
  Serial.println("MIXER,0,0,0,0,0,0");
}

void loop() {
  handleSerialInput();

  const unsigned long now = millis();
  if (now - lastFrameAt < frameIntervalMs) {
    return;
  }

  lastFrameAt = now;
  sendAnalogValues();
}

void handleSerialInput() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\n') {
      serialBuf[serialBufPos] = '\0';
      serialBufPos = 0;
      if (strcmp(serialBuf, "HELLO_MIXER") == 0) {
        Serial.println("MIXER_READY");
      }
    } else if (serialBufPos < 63) {
      serialBuf[serialBufPos++] = c;
    }
  }
}

void sendAnalogValues() {
  sequence++;

  Serial.print("MIXER,");
  Serial.print(sequence);

  for (int i = 0; i < numInputs; i++) {
    int sensorValue = analogRead(inputPins[i]);
    Serial.print(',');
    Serial.print(sensorValue);
  }

  Serial.println();
}
