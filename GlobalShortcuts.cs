using System;using System.ComponentModel;using System.Runtime.InteropServices;using System.Threading;using System.Windows.Forms;
// Dedicated message-only window survives pet resizing/hiding/handle recreation.
// Hook callback only queues the action, never performs drawing or disk I/O.
internal sealed class GlobalShortcuts : NativeWindow,IDisposable {
 const int Dispatch=0x8001,Stop=0x8002;readonly Action<int> action;readonly bool[] held=new bool[2];readonly object keyGate=new object();readonly SynchronizationContext context;readonly ManualResetEventSlim ready=new ManualResetEventSlim();Thread thread;KeyboardProc callback;IntPtr hook;volatile bool disposed;bool threaded;
 internal bool ModeReady,MonitorReady;internal string Backend,Failure;
 delegate IntPtr KeyboardProc(int code,IntPtr message,IntPtr data);
 internal delegate IntPtr ForwardProc(IntPtr hook,int code,IntPtr message,IntPtr data);
 readonly ForwardProc forward=CallNextHookEx;
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int type,KeyboardProc proc,IntPtr module,uint thread);
 [DllImport("user32.dll",SetLastError=true)] static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 internal GlobalShortcuts(Action<int> action,bool install=true){this.action=action;context=SynchronizationContext.Current??new WindowsFormsSynchronizationContext();
  if(!install){CreateHandle(new CreateParams{Caption="CornerPet.Shortcuts.Test",Parent=new IntPtr(-3)});Backend="test";return;}
  threaded=true;thread=new Thread(Run){IsBackground=true,Name="CornerPet shortcuts"};thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!ready.Wait(5000)){disposed=true;throw new InvalidOperationException("快捷鍵啟動逾時");}
 }
 void Run(){try{CreateHandle(new CreateParams{Caption="CornerPet.Shortcuts",Parent=new IntPtr(-3)});callback=Keyboard;hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
   if(hook!=IntPtr.Zero){ModeReady=MonitorReady=true;Backend="shared-keyboard-hook";}else{int code=Marshal.GetLastWin32Error();Failure="鍵盤掛接失敗："+new Win32Exception(code).Message;Backend="unavailable";}
   ready.Set();if(!disposed)Application.Run();
  }catch(Exception ex){Failure=ex.Message;ModeReady=MonitorReady=false;ready.Set();}finally{Cleanup();}}
 IntPtr Keyboard(int code,IntPtr message,IntPtr data){return ObserveAndForward(code,message,data,forward);}
 // F7/F8 are shared: observe once per press, then ALWAYS continue the hook chain.
 // Never claim RegisterHotKey ownership or suppress either key-down or key-up.
 internal IntPtr ObserveAndForward(int code,IntPtr message,IntPtr data,ForwardProc next){try{if(code>=0&&!disposed)ProcessKey(Marshal.ReadInt32(data),message.ToInt32());}catch{}return next(hook,code,message,data);}
 internal bool ProcessKey(int vk,int message){int index=vk==(int)Keys.F7?0:vk==(int)Keys.F8?1:-1;if(index<0||disposed)return false;lock(keyGate){if(message==0x100||message==0x104){if(!held[index]){held[index]=true;PostMessage(Handle,Dispatch,new IntPtr(index+1),IntPtr.Zero);}return true;}if(message==0x101||message==0x105){held[index]=false;return true;}return false;}}
 protected override void WndProc(ref Message message){if(message.Msg==Stop){Application.ExitThread();return;}if(!disposed&&(message.Msg==Dispatch||message.Msg==0x312)){int id=message.WParam.ToInt32();if(id==1||id==2){if(threaded)context.Post(s=>{if(!disposed)action((int)s);},id);else action(id);return;}}base.WndProc(ref message);}
 void Cleanup(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}if(Handle!=IntPtr.Zero)DestroyHandle();callback=null;}
 public void Dispose(){if(disposed)return;disposed=true;if(threaded){if(Handle!=IntPtr.Zero)PostMessage(Handle,Stop,IntPtr.Zero,IntPtr.Zero);if(thread!=null&&Thread.CurrentThread!=thread)thread.Join(1000);}else Cleanup();if(thread==null||!thread.IsAlive)ready.Dispose();}
}
