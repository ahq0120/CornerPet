using System;using System.Collections.Generic;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.IO;using System.Runtime.InteropServices;using System.Security.Cryptography;using System.Text;using System.Text.RegularExpressions;
public sealed class AssetInfo {public string Id,Name,Extension,CreatedUtc;public long Bytes;public int Width,Height,Frames;}
public sealed class AssetPage {public AssetInfo[] Items;public int Total,Page;}
internal sealed class Sqlite : IDisposable {
 IntPtr db;
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int sqlite3_open16(string path,out IntPtr handle);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr handle);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int sqlite3_prepare16_v2(IntPtr db,string sql,int bytes,out IntPtr statement,IntPtr tail);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern int sqlite3_bind_text16(IntPtr statement,int index,string value,int bytes,IntPtr destructor);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_int64(IntPtr statement,int index,long value);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr statement);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr statement);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr statement);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_type(IntPtr statement,int column);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern long sqlite3_column_int64(IntPtr statement,int column);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text16(IntPtr statement,int column);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg16(IntPtr db);
 internal Sqlite(string path){int code=sqlite3_open16(path,out db);if(code!=0){string error=Error();Dispose();throw new IOException(error);}}
 string Error(){return db==IntPtr.Zero?"SQLite 未開啟":Marshal.PtrToStringUni(sqlite3_errmsg16(db));}
 internal List<object[]> Query(string sql,params object[] values){if(db==IntPtr.Zero)throw new ObjectDisposedException("SQLite");IntPtr statement;int code=sqlite3_prepare16_v2(db,sql,-1,out statement,IntPtr.Zero);if(code!=0)throw new IOException(Error());
  try{for(int i=0;i<values.Length;i++){code=values[i] is string?sqlite3_bind_text16(statement,i+1,(string)values[i],-1,new IntPtr(-1)):sqlite3_bind_int64(statement,i+1,Convert.ToInt64(values[i]));if(code!=0)throw new IOException(Error());}
   var rows=new List<object[]>();while((code=sqlite3_step(statement))==100){var row=new object[sqlite3_column_count(statement)];for(int i=0;i<row.Length;i++)row[i]=sqlite3_column_type(statement,i)==1?(object)sqlite3_column_int64(statement,i):Marshal.PtrToStringUni(sqlite3_column_text16(statement,i));rows.Add(row);}if(code!=101)throw new IOException(Error());return rows;
  }finally{sqlite3_finalize(statement);}}
 public void Dispose(){if(db!=IntPtr.Zero){sqlite3_close(db);db=IntPtr.Zero;}}
}
internal sealed class LibraryStore : IDisposable {
 internal const long MaxBytes=20L*1024*1024;internal readonly string Folder;readonly object gate=new object();Sqlite db;
 internal LibraryStore(string folder){Folder=Path.GetFullPath(folder);Directory.CreateDirectory(Folder);Directory.CreateDirectory(Path.Combine(Folder,"files"));Directory.CreateDirectory(Path.Combine(Folder,"thumbs"));db=new Sqlite(Path.Combine(Folder,"catalog.sqlite"));db.Query("PRAGMA journal_mode=WAL");db.Query("PRAGMA synchronous=NORMAL");db.Query("PRAGMA cache_size=-1024");db.Query("CREATE TABLE IF NOT EXISTS assets (id TEXT PRIMARY KEY,name TEXT NOT NULL,ext TEXT NOT NULL,bytes INTEGER NOT NULL,width INTEGER NOT NULL,height INTEGER NOT NULL,frames INTEGER NOT NULL,created TEXT NOT NULL)");}
 internal static bool ValidId(string id){return id!=null&&Regex.IsMatch(id,"\\A[a-f0-9]{64}\\z");}
 internal static string Hash(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 static AssetInfo FromRow(object[] r){return new AssetInfo{Id=(string)r[0],Name=(string)r[1],Extension=(string)r[2],Bytes=(long)r[3],Width=(int)(long)r[4],Height=(int)(long)r[5],Frames=(int)(long)r[6],CreatedUtc=(string)r[7]};}
 internal AssetPage List(int page){lock(gate){page=Math.Max(0,Math.Min(100000,page));var rows=db.Query("SELECT id,name,ext,bytes,width,height,frames,created FROM assets ORDER BY created DESC,id LIMIT 12 OFFSET ?",page*12);return new AssetPage{Items=rows.ConvertAll(FromRow).ToArray(),Total=(int)(long)db.Query("SELECT COUNT(*) FROM assets")[0][0],Page=page};}}
 internal AssetInfo Get(string id){if(!ValidId(id))return null;lock(gate){var rows=db.Query("SELECT id,name,ext,bytes,width,height,frames,created FROM assets WHERE id=?",id);return rows.Count==0?null:FromRow(rows[0]);}}
 internal string FilePath(AssetInfo item){return Path.Combine(Folder,"files",item.Id+item.Extension);}
 internal string ThumbPath(AssetInfo item){return Path.Combine(Folder,"thumbs",item.Id+".png");}
 internal AssetInfo Import(string path,string name){lock(gate){long bytes=new FileInfo(path).Length;if(bytes<=0||bytes>MaxBytes)throw new InvalidDataException("素材上限為 20 MB。");string id=Hash(path);var existing=Get(id);if(existing!=null)return existing;
  AssetInfo item;string thumbTemp=Path.Combine(Folder,"thumbs",Guid.NewGuid().ToString("N")+".part");try{
   using(var image=Image.FromFile(path)){string ext=image.RawFormat.Guid==ImageFormat.Gif.Guid?".gif":image.RawFormat.Guid==ImageFormat.Png.Guid?".png":image.RawFormat.Guid==ImageFormat.Jpeg.Guid?".jpg":image.RawFormat.Guid==ImageFormat.Bmp.Guid?".bmp":null;
    if(ext==null||image.Width>4096||image.Height>4096)throw new InvalidDataException("只接受 GIF、PNG、JPEG、BMP，長寬上限 4096。");int frames=Array.IndexOf(image.FrameDimensionsList,FrameDimension.Time.Guid)>=0?image.GetFrameCount(FrameDimension.Time):1;if(frames>600||(long)image.Width*image.Height*frames>64000000)throw new InvalidDataException("動畫過大，請先縮小尺寸或減少影格。");
    string title=Path.GetFileNameWithoutExtension(name??"素材");title=Regex.Replace(title,"[\\x00-\\x1f]","");if(title.Length>120)title=title.Substring(0,120);if(String.IsNullOrWhiteSpace(title))title="素材";
    item=new AssetInfo{Id=id,Name=title,Extension=ext,Bytes=bytes,Width=image.Width,Height=image.Height,Frames=frames,CreatedUtc=DateTime.UtcNow.ToString("o")};
    using(var thumb=new Bitmap(160,120,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(thumb)){g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;double scale=Math.Min(160.0/image.Width,120.0/image.Height);int w=Math.Max(1,(int)(image.Width*scale)),h=Math.Max(1,(int)(image.Height*scale));g.DrawImage(image,(160-w)/2,(120-h)/2,w,h);thumb.Save(thumbTemp,ImageFormat.Png);}
   }
   string dest=FilePath(item);if(!File.Exists(dest))File.Copy(path,dest);string thumbPath=ThumbPath(item);if(File.Exists(thumbPath))File.Delete(thumbPath);File.Move(thumbTemp,thumbPath);
   db.Query("INSERT INTO assets(id,name,ext,bytes,width,height,frames,created) VALUES(?,?,?,?,?,?,?,?)",item.Id,item.Name,item.Extension,item.Bytes,item.Width,item.Height,item.Frames,item.CreatedUtc);return item;
  }catch(ArgumentException){throw new InvalidDataException("檔案不是有效的圖片。");}catch(OutOfMemoryException){throw new InvalidDataException("圖片無法解碼或尺寸過大。");}finally{if(File.Exists(thumbTemp))File.Delete(thumbTemp);}
 }}
 internal void Delete(string id){lock(gate){var item=Get(id);if(item==null)return;db.Query("DELETE FROM assets WHERE id=?",id);foreach(string path in new[]{FilePath(item),ThumbPath(item)})try{File.Delete(path);}catch(IOException){} }}
 public void Dispose(){lock(gate){if(db!=null){db.Dispose();db=null;}}}
}
