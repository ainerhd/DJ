using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

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
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();

        enumerator.EnumAudioEndpoints(EDataFlow.eRender, DEVICE_STATE.DEVICE_STATE_ACTIVE, out var collection);
        collection.GetCount(out var count);

        enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var defaultDevice);
        defaultDevice.GetId(out var defaultIdPtr);
        var defaultId = Marshal.PtrToStringUni(defaultIdPtr) ?? string.Empty;
        Marshal.FreeCoTaskMem(defaultIdPtr);

        for (var index = 0; index < count; index++)
        {
            collection.Item(index, out var device);
            device.GetId(out var idPtr);
            var id = Marshal.PtrToStringUni(idPtr) ?? string.Empty;
            Marshal.FreeCoTaskMem(idPtr);

        var name = ReadFriendlyName(device);
            result.Add(new AudioDeviceInfo
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(name) ? id : name,
                IsDefault = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase)
            });
        }

        return result;
    }

    public void SetMasterVolume(string deviceId, double volumePercent)
    {
        volumePercent = Math.Clamp(volumePercent, 0, 100);
        var scalar = (float)(volumePercent / 100.0);

        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice device;

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device);
        }
        else
        {
            enumerator.GetDevice(deviceId, out device);
        }

        var iid = typeof(IAudioEndpointVolume).GUID;
        device.Activate(ref iid, CLSCTX.CLSCTX_ALL, IntPtr.Zero, out var endpointObj);
        var endpoint = (IAudioEndpointVolume)endpointObj;
        endpoint.SetMasterVolumeLevelScalar(scalar, Guid.Empty);
    }

    private static string ReadFriendlyName(IMMDevice device)
    {
        device.OpenPropertyStore(STGM.STGM_READ, out var store);
        store.GetValue(ref PropertyKeys.PKEY_Device_FriendlyName, out var value);
        var name = value.GetValue() as string ?? string.Empty;
        value.Dispose();
        Marshal.ReleaseComObject(store);
        return name;
    }
}

internal enum EDataFlow
{
    eRender,
    eCapture,
    eAll
}

internal enum ERole
{
    eConsole,
    eMultimedia,
    eCommunications
}

[Flags]
internal enum DEVICE_STATE
{
    DEVICE_STATE_ACTIVE = 0x00000001
}

[Flags]
internal enum CLSCTX
{
    CLSCTX_ALL = 23
}

internal enum STGM
{
    STGM_READ = 0x00000000
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumerator
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(EDataFlow dataFlow, DEVICE_STATE dwStateMask, out IMMDeviceCollection ppDevices);
    int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
}

[ComImport]
[Guid("0BD7A1BE-7A1A-44DB-8397-C0A4A7EF4F3D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceCollection
{
    int GetCount(out int pcDevices);
    int Item(int nDevice, out IMMDevice ppDevice);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    int OpenPropertyStore(STGM stgmAccess, out IPropertyStore ppProperties);
    int GetId(out IntPtr ppstrId);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    int GetCount(out int cProps);
    int GetAt(int iProp, out PROPERTYKEY pkey);
    int GetValue(ref PROPERTYKEY key, out PropVariant pv);
    int SetValue(ref PROPERTYKEY key, ref PropVariant pv);
    int Commit();
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(IntPtr pNotify);
    int UnregisterControlChangeNotify(IntPtr pNotify);
    int GetChannelCount(out int pnChannelCount);
    int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
    int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
    int GetMasterVolumeLevel(out float pfLevelDB);
    int GetMasterVolumeLevelScalar(out float pfLevel);
    int SetChannelVolumeLevel(int nChannel, float fLevelDB, Guid pguidEventContext);
    int SetChannelVolumeLevelScalar(int nChannel, float fLevel, Guid pguidEventContext);
    int GetChannelVolumeLevel(int nChannel, out float pfLevelDB);
    int GetChannelVolumeLevelScalar(int nChannel, out float pfLevel);
    int SetMute(bool bMute, Guid pguidEventContext);
    int GetMute(out bool pbMute);
    int GetVolumeStepInfo(out int pnStep, out int pnStepCount);
    int VolumeStepUp(Guid pguidEventContext);
    int VolumeStepDown(Guid pguidEventContext);
    int QueryHardwareSupport(out int pdwHardwareSupportMask);
    int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY
{
    public Guid fmtid;
    public int pid;
}

internal static class PropertyKeys
{
    public static PROPERTYKEY PKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14
    };
}

[StructLayout(LayoutKind.Explicit)]
internal struct PropVariant : IDisposable
{
    [FieldOffset(0)] private ushort vt;
    [FieldOffset(8)] private IntPtr pointerValue;

    public object? GetValue() => vt == 31 ? Marshal.PtrToStringUni(pointerValue) : null;

    public void Dispose()
    {
        PropVariantClear(ref this);
        GC.SuppressFinalize(this);
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);
}
