using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using LunarSync.Core;

namespace LunarSync;

internal sealed class NativeInput : IInputOutput,IDisposable
{
    private const int WH_KEYBOARD_LL=13,WH_MOUSE_LL=14;
    private readonly HookProc keyboardProc,mouseProc;
    private IntPtr keyboardHook,mouseHook;
    internal Func<int,bool,bool>? OnInput;
    internal Action? EmergencyStop;
    internal bool CaptureMode {get;set;}
    internal Action<int>? Captured;
    private readonly HashSet<int> physicalKeys=[];
    private readonly HashSet<int> capturedKeys=[];
    private readonly HashSet<int> blockedKeys=[];
    public NativeInput()
    {
        keyboardProc=Keyboard;mouseProc=Mouse;
        var module=GetModuleHandle(null);
        keyboardHook=SetWindowsHookEx(WH_KEYBOARD_LL,keyboardProc,module,0);
        mouseHook=SetWindowsHookEx(WH_MOUSE_LL,mouseProc,module,0);
        if(keyboardHook==IntPtr.Zero||mouseHook==IntPtr.Zero){Dispose();throw new Win32Exception(Marshal.GetLastWin32Error());}
    }
    private IntPtr Keyboard(int code,IntPtr message,IntPtr data)
    {
        if(code<0)return CallNextHookEx(keyboardHook,code,message,data);
        var info=Marshal.PtrToStructure<KeyboardData>(data);
        if((info.Flags&0x10)!=0)return CallNextHookEx(keyboardHook,code,message,data);
        int key=Keys.Normalize((int)info.VkCode);
        bool down=message.ToInt64() is 0x100 or 0x104;
        bool up=message.ToInt64() is 0x101 or 0x105;
        if(!down&&!up)return CallNextHookEx(keyboardHook,code,message,data);
        if(down)physicalKeys.Add(key);else physicalKeys.Remove(key);
        if(up&&capturedKeys.Remove(key))return new IntPtr(1);
        if(up&&blockedKeys.Remove(key)){OnInput?.Invoke(key,false);return new IntPtr(1);}
        if(CaptureMode&&down)
        {
            capturedKeys.Add(key);
            if(Keys.IsAllowed(key)){CaptureMode=false;Captured?.Invoke(key);}
            return new IntPtr(1);
        }
        if(down&&key==123&&physicalKeys.Contains(17)&&physicalKeys.Contains(18))
        {EmergencyStop?.Invoke();blockedKeys.Add(key);return new IntPtr(1);}
        try
        {
            if(OnInput?.Invoke(key,down)==true){if(down)blockedKeys.Add(key);return new IntPtr(1);}
        }
        catch { EmergencyStop?.Invoke(); }
        return CallNextHookEx(keyboardHook,code,message,data);
    }
    private IntPtr Mouse(int code,IntPtr message,IntPtr data)
    {
        if(code<0)return CallNextHookEx(mouseHook,code,message,data);
        var info=Marshal.PtrToStructure<MouseData>(data);
        if((info.Flags&1)!=0)return CallNextHookEx(mouseHook,code,message,data);
        int msg=(int)message,key=0;bool down=false;
        if(msg is 0x201 or 0x202){key=1;down=msg==0x201;}
        if(msg is 0x20B or 0x20C){key=(info.ButtonData>>16)==1?5:6;down=msg==0x20B;}
        if(key==0)return CallNextHookEx(mouseHook,code,message,data);
        if(!down&&capturedKeys.Remove(key))return new IntPtr(1);
        if(!down&&blockedKeys.Remove(key)){OnInput?.Invoke(key,false);return new IntPtr(1);}
        if(CaptureMode&&down&&key is 5 or 6){CaptureMode=false;capturedKeys.Add(key);Captured?.Invoke(key);return new IntPtr(1);}
        try {if(OnInput?.Invoke(key,down)==true){if(down)blockedKeys.Add(key);return new IntPtr(1);}} catch{EmergencyStop?.Invoke();}
        return CallNextHookEx(mouseHook,code,message,data);
    }
    public void SetKey(int key,bool down)
    {
        Input input=new();
        if(key<=6)
        {
            input.Type=0;
            input.Data.Mouse.Flags=key==1?(down?0x2u:0x4u):(down?0x80u:0x100u);
            input.Data.Mouse.MouseData=key==5?1u:key==6?2u:0u;
        }
        else
        {
            input.Type=1;
            input.Data.Keyboard.Vk=(ushort)key;
            input.Data.Keyboard.Flags=down?0u:2u;
            if(key is >=33 and <=46)input.Data.Keyboard.Flags|=1;
        }
        if(SendInput(1,[input],Marshal.SizeOf<Input>())!=1)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows a refusé l’envoi de la touche.");
    }
    public void Dispose(){if(keyboardHook!=IntPtr.Zero)UnhookWindowsHookEx(keyboardHook);if(mouseHook!=IntPtr.Zero)UnhookWindowsHookEx(mouseHook);keyboardHook=mouseHook=IntPtr.Zero;}
    private delegate IntPtr HookProc(int code,IntPtr wParam,IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData{public uint VkCode,ScanCode,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct MouseData{public int X,Y;public uint ButtonData,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct Input{public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion{[FieldOffset(0)]public MouseInput Mouse;[FieldOffset(0)]public KeyboardInput Keyboard;}
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput{public int X,Y;public uint MouseData,Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput{public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll",SetLastError=true)]private static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr module,uint thread);
    [DllImport("user32.dll")]private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
}

internal sealed class EngineRunner : IDisposable
{
    private readonly CancellationTokenSource stop=new();
    private readonly AutoResetEvent wake=new(false);
    private readonly Thread thread;
    private readonly MacroEngine engine;
    internal event Action<string>? Failed;
    internal EngineRunner(MacroEngine engine)
    {
        this.engine=engine;engine.Changed+=Wake;
        thread=new Thread(()=>
        {
            using var timer=new TimerWait();
            while(!stop.IsCancellationRequested)
            {
                try{engine.Tick();}catch(Exception ex){try{engine.StopAll();}catch{} Failed?.Invoke(ex.Message);}
                double delay=engine.DelayToNext();
                if(timer.Available)
                {
                    long due=-(long)(delay*10000);SetWaitableTimer(timer.SafeWaitHandle.DangerousGetHandle(),ref due,0,IntPtr.Zero,IntPtr.Zero,false);
                    WaitHandle.WaitAny([stop.Token.WaitHandle,wake,timer]);
                }
                else WaitHandle.WaitAny([stop.Token.WaitHandle,wake],(int)Math.Ceiling(delay));
            }
        }){IsBackground=true,Name="LunarSync input scheduler"};thread.Start();
    }
    private void Wake()=>wake.Set();
    public void Dispose(){engine.Changed-=Wake;stop.Cancel();thread.Join(1500);wake.Dispose();stop.Dispose();}
    private sealed class TimerWait : WaitHandle
    {
        internal bool Available=>!SafeWaitHandle.IsInvalid;
        internal TimerWait(){SafeWaitHandle=new Microsoft.Win32.SafeHandles.SafeWaitHandle(CreateWaitableTimerEx(IntPtr.Zero,null,2,0x1F0003),true);}
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateWaitableTimerEx(IntPtr attributes,string? name,uint flags,uint access);
    [DllImport("kernel32.dll")]private static extern bool SetWaitableTimer(IntPtr timer,ref long due,int period,IntPtr callback,IntPtr argument,bool resume);
}
