namespace AudioMixerController.App;

public interface IAudioVolumeService
{
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();
    void SetMasterVolume(string deviceId, double volumePercent);
}
