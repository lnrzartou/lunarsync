using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LunarSync.Release;

internal static class WindowsSigningKey
{
    internal static void Create(string folder)
    {
        Directory.CreateDirectory(folder);
        string privateFile=Path.Combine(folder,"lunarsync-private.dpapi"),publicFile=Path.Combine(folder,"lunarsync-public.pem");
        if(File.Exists(privateFile)||File.Exists(publicFile))throw new IOException("Une clé existe déjà. Aucun écrasement autorisé.");
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] clear=key.ExportPkcs8PrivateKey();
        try
        {
            byte[] encrypted=Protect(clear,false);
            using(var file=new FileStream(privateFile,FileMode.CreateNew,FileAccess.Write,FileShare.None))file.Write(encrypted);
            using var pub=new StreamWriter(new FileStream(publicFile,FileMode.CreateNew,FileAccess.Write));pub.Write(key.ExportSubjectPublicKeyInfoPem());
        }
        finally{CryptographicOperations.ZeroMemory(clear);}
    }
    internal static ECDsa Load(string file)
    {
        byte[] clear=Protect(File.ReadAllBytes(file),true);var key=ECDsa.Create();
        try{key.ImportPkcs8PrivateKey(clear,out int read);if(read!=clear.Length||key.KeySize!=256)throw new CryptographicException("Clé de publication invalide.");return key;}
        catch{key.Dispose();throw;}
        finally{CryptographicOperations.ZeroMemory(clear);}
    }
    private static byte[] Protect(byte[] input,bool decrypt)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Cette clé nécessite le compte Windows qui l’a créée.");
        var pin=GCHandle.Alloc(input,GCHandleType.Pinned);Blob result=default;
        try
        {
            var source=new Blob{Length=input.Length,Data=pin.AddrOfPinnedObject()};
            // Current-user DPAPI, no machine-wide decryption and no prompt UI.
            bool ok=decrypt?CryptUnprotectData(ref source,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out result)
                :CryptProtectData(ref source,"LunarSync publisher key",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out result);
            if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error(),"Impossible de protéger ou ouvrir la clé pour ce compte Windows.");
            var output=new byte[result.Length];Marshal.Copy(result.Data,output,0,output.Length);return output;
        }
        finally
        {
            if(result.Data!=IntPtr.Zero){for(int i=0;i<result.Length;i++)Marshal.WriteByte(result.Data,i,0);LocalFree(result.Data);}
            pin.Free();
        }
    }
    [StructLayout(LayoutKind.Sequential)]private struct Blob{internal int Length;internal IntPtr Data;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
    [DllImport("crypt32.dll",ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob output);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr memory);
}
