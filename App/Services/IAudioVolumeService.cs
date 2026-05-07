namespace AudioMixerController.App;

public interface IAudioVolumeService
{
    IReadOnlyList<AudioDeviceInfo> GetOutputDevices();
    void SetMasterVolume(string deviceId, double volumePercent);
    void SetMasterMute(string deviceId, bool isMuted);
}
