using NAudio.CoreAudioApi;

namespace AudioMixerController.App;

public sealed class WindowsAudioVolumeService : IAudioVolumeService
{
    private readonly LogStore _log;

    public WindowsAudioVolumeService(LogStore log)
    {
        _log = log;
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        var result = new List<AudioDeviceInfo>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var defaultId = defaultDevice.ID;

            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                try
                {
                    result.Add(new AudioDeviceInfo
                    {
                        Id = device.ID,
                        Name = string.IsNullOrWhiteSpace(device.FriendlyName) ? device.ID : device.FriendlyName,
                        IsDefault = string.Equals(device.ID, defaultId, StringComparison.OrdinalIgnoreCase)
                    });
                }
                catch (Exception ex)
                {
                    _log.Add($"Device entry error: {ex.Message}", true);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Add($"Device scan error: {ex.Message}", true);
        }

        return result;
    }

    public void SetMasterVolume(string deviceId, double volumePercent)
    {
        volumePercent = Math.Clamp(volumePercent, 0, 100);
        var scalar = (float)(volumePercent / 100.0);

        using var enumerator = new MMDeviceEnumerator();
        using var device = string.IsNullOrWhiteSpace(deviceId)
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : enumerator.GetDevice(deviceId);

        device.AudioEndpointVolume.MasterVolumeLevelScalar = scalar;
    }

    public void SetMasterMute(string deviceId, bool isMuted)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = string.IsNullOrWhiteSpace(deviceId)
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : enumerator.GetDevice(deviceId);

        device.AudioEndpointVolume.Mute = isMuted;
    }
}
