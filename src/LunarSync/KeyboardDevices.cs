using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace LunarSync;

internal sealed record KeyboardDevice(string Id,string Name,string Detail,string? SuggestedLayout)
{ public override string ToString()=>Name; }

internal static class KeyboardDevices
{
    [StructLayout(LayoutKind.Sequential)]private struct RawDevice{public IntPtr Handle;public uint Type;}
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRawInputDeviceList([Out] RawDevice[]? devices,ref uint count,uint size);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,StringBuilder? data,ref uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("hid.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]
    [return:MarshalAs(UnmanagedType.U1)]private static extern bool HidD_GetProductString(SafeFileHandle device,StringBuilder buffer,uint bytes);
    internal static List<KeyboardDevice> Get()
    {
        uint count=0,size=(uint)Marshal.SizeOf<RawDevice>();
        if(GetRawInputDeviceList(null,ref count,size)==uint.MaxValue)return [];
        var devices=new RawDevice[count];
        uint found=GetRawInputDeviceList(devices,ref count,size);
        if(found==uint.MaxValue)return [];
        var result=new Dictionary<string,KeyboardDevice>(StringComparer.OrdinalIgnoreCase);
        foreach(var device in devices.Take((int)found).Where(d=>d.Type==1))
        {
            uint length=0;GetRawInputDeviceInfo(device.Handle,0x20000007,null,ref length);
            var name=new StringBuilder((int)length+1);
            if(GetRawInputDeviceInfo(device.Handle,0x20000007,name,ref length)==uint.MaxValue)continue;
            string path=name.ToString();if(path.Contains("RDP_",StringComparison.OrdinalIgnoreCase))continue;
            string id=path,label="Clavier HID",detail="Clavier connecté",container="";
            try
            {
                var parts=path.TrimStart('\\','?').Split('#');
                if(parts.Length>=3)
                {
                    using var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\"+parts[0]+"\\"+parts[1]+"\\"+parts[2]);
                    label=(key?.GetValue("FriendlyName")??key?.GetValue("DeviceDesc")??label).ToString()!;
                    if(label.Contains(';'))label=label[(label.LastIndexOf(';')+1)..];
                    container=key?.GetValue("ContainerID")?.ToString()??"";
                    detail=parts[1];
                    if(container.Length>0&&container!="{00000000-0000-0000-0000-000000000000}")id=container;
                }
            }catch{ }
            try
            {
                using var handle=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero);
                var product=new StringBuilder(256);
                if(!handle.IsInvalid&&HidD_GetProductString(handle,product,512)&&product.Length>0)label=product.ToString().Trim();
            }catch{ }
            if(path.Contains("VID_1038",StringComparison.OrdinalIgnoreCase)&&!label.Contains("SteelSeries",StringComparison.OrdinalIgnoreCase))label="SteelSeries · "+label;
            if(path.Contains("VID_046D",StringComparison.OrdinalIgnoreCase)&&!label.Contains("Logitech",StringComparison.OrdinalIgnoreCase))label="Logitech · "+label;
            string? format=label.Contains("TKL",StringComparison.OrdinalIgnoreCase)?"TKL":label.Contains("60%")?"60%":null;
            if(!result.ContainsKey(id))result[id]=new(id,label,detail,format);
        }
        return result.Values.OrderBy(d=>d.Name).ToList();
    }
}
