using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

// A private disk snapshot keeps the original editable/deletable without retaining
// a file-sized managed byte array. The decoder's input remains open as GDI+ requires.
internal sealed class ImageAsset : IDisposable {
 internal Image Image; internal int Width,Height,Count; internal int[] Delays;
 FileStream snapshot;
 internal string SnapshotPath {get{return snapshot==null?null:snapshot.Name;}}
 internal static ImageAsset Open(string path){var asset=new ImageAsset();try{
  using(var original=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
   if(original.Length>50L*1024*1024)throw new Exception("圖片上限為 50 MB。");
   string folder=Path.Combine(Path.GetTempPath(),"CornerPet");Directory.CreateDirectory(folder);
   asset.snapshot=new FileStream(Path.Combine(folder,Guid.NewGuid().ToString("N")+".image"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read|FileShare.Delete,4096,FileOptions.DeleteOnClose|FileOptions.RandomAccess);
   original.CopyTo(asset.snapshot,32768);
  }
  asset.Resume(0);asset.Width=asset.Image.Width;asset.Height=asset.Image.Height;
  if(asset.Width>4096||asset.Height>4096)throw new Exception("圖片尺寸上限為 4096 × 4096。");
  asset.Count=Array.IndexOf(asset.Image.FrameDimensionsList,FrameDimension.Time.Guid)>=0?asset.Image.GetFrameCount(FrameDimension.Time):1;
  asset.Delays=new int[asset.Count];for(int i=0;i<asset.Count;i++)asset.Delays[i]=100;
  try{byte[] raw=asset.Image.GetPropertyItem(0x5100).Value;for(int i=0;i<asset.Count&&i*4+4<=raw.Length;i++){long ms=(long)BitConverter.ToUInt32(raw,i*4)*10;asset.Delays[i]=(int)Math.Max(20,Math.Min(Int32.MaxValue,ms));}}catch(ArgumentException){}
  return asset;
 }catch{asset.Dispose();throw;}}
 internal void Suspend(){if(Image!=null){Image.Dispose();Image=null;}}
 internal void Resume(int frame){if(Image!=null)return;snapshot.Position=0;Image=Image.FromStream(snapshot,true,true);if(Count>1)Image.SelectActiveFrame(FrameDimension.Time,frame);}
 public void Dispose(){Suspend();if(snapshot!=null){snapshot.Dispose();snapshot=null;}}
}
