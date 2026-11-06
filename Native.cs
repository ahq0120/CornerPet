using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
internal static class Native {
 [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; public Point(int x,int y){X=x;Y=y;} }
 [StructLayout(LayoutKind.Sequential)] internal struct Size { public int W,H; public Size(int w,int h){W=w;H=h;} }
 [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct Blend { public byte Op,Flags,Alpha,Format; }
 [DllImport("user32.dll")] internal static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dc,ref Point p,ref Size s,IntPtr src,ref Point origin,int key,ref Blend blend,int flags);
 [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr h);
 [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr h,IntPtr dc);
 [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
 [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo { public uint HeaderSize; public int Width,Height; public ushort Planes,BitCount; public uint Compression,ImageSize; public int XPels,YPels; public uint ColorsUsed,ColorsImportant; }
 [DllImport("gdi32.dll",SetLastError=true)] internal static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
 [DllImport("user32.dll")] internal static extern int GetWindowLong(IntPtr h,int n);
 [DllImport("user32.dll")] internal static extern int SetWindowLong(IntPtr h,int n,int value);
 [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
 [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
}
// One display-sized, premultiplied BGRA buffer shared by GDI+ and UpdateLayeredWindow.
// Never hold a decoded Bitmap per animation frame.
internal sealed class RenderSurface : IDisposable {
 IntPtr dc,handle,previous; Bitmap bitmap; internal Graphics Graphics; internal int Width,Height;
 internal static int Created,Alive;
 internal long Bytes {get{return (long)Width*Height*4;}}
 internal Color Sample(int x,int y){Graphics.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);return bitmap.GetPixel(x,y);}
 internal RenderSurface(int width,int height){Width=width;Height=height;
  try {dc=Native.CreateCompatibleDC(IntPtr.Zero);if(dc==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
   var info=new Native.BitmapInfo{HeaderSize=(uint)Marshal.SizeOf(typeof(Native.BitmapInfo)),Width=width,Height=-height,Planes=1,BitCount=32};IntPtr bits;
   handle=Native.CreateDIBSection(dc,ref info,0,out bits,IntPtr.Zero,0);if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
   previous=Native.SelectObject(dc,handle);if(previous==IntPtr.Zero||previous==new IntPtr(-1))throw new System.ComponentModel.Win32Exception();
   bitmap=new Bitmap(width,height,width*4,PixelFormat.Format32bppPArgb,bits);Graphics=Graphics.FromImage(bitmap);Created++;Alive++;
  }catch{Dispose();throw;}
 }
 internal void Present(IntPtr window,int x,int y){Graphics.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);IntPtr screen=Native.GetDC(IntPtr.Zero);
  try {var point=new Native.Point(x,y);var size=new Native.Size(Width,Height);var origin=new Native.Point(0,0);var blend=new Native.Blend{Alpha=255,Format=1};if(!Native.UpdateLayeredWindow(window,screen,ref point,ref size,dc,ref origin,0,ref blend,2))throw new System.ComponentModel.Win32Exception();}
  finally{Native.ReleaseDC(IntPtr.Zero,screen);}
 }
 public void Dispose(){if(Graphics!=null){Graphics.Dispose();Graphics=null;Alive--;}if(bitmap!=null){bitmap.Dispose();bitmap=null;}if(previous!=IntPtr.Zero&&previous!=new IntPtr(-1)&&dc!=IntPtr.Zero){Native.SelectObject(dc,previous);previous=IntPtr.Zero;}if(handle!=IntPtr.Zero){Native.DeleteObject(handle);handle=IntPtr.Zero;}if(dc!=IntPtr.Zero){Native.DeleteDC(dc);dc=IntPtr.Zero;}}
}
