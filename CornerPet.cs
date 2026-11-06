using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

public class Preferences {
 public string ImagePath=""; public int X=Int32.MinValue,Y=0,Width=200; public bool Game=false;
 public static string Folder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CornerPet");}}
 public static Preferences Load(){try{using(var s=File.OpenRead(Path.Combine(Folder,"settings.xml")))return (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(s);}catch{return new Preferences();}}
 public void Save(){Directory.CreateDirectory(Folder);string path=Path.Combine(Folder,"settings.xml"),temp=path+".tmp";using(var s=File.Create(temp))new XmlSerializer(typeof(Preferences)).Serialize(s,this);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
}
internal sealed class Pet : Form {
 internal Preferences State; internal bool Game; internal int FrameCount; internal bool HotkeyReady,MonitorHotkeyReady; string previousSecondary;int modeKeyCount,screenKeyCount;
 GlobalShortcuts shortcuts; bool testMode; ImageAsset asset; RenderSurface surface; int frame; internal int DrawCount; internal long BufferBytes {get{return surface==null?0:surface.Bytes;}} internal bool Animating {get{return timer!=null&&timer.Enabled;}} internal bool DecoderLoaded {get{return asset!=null&&asset.Image!=null;}} Timer timer; NotifyIcon tray; PanelWindow panel; LibraryWindow library; bool dragging; Point grab; string warning="";
 protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x80|0x08000000;return p;}}
 protected override bool ShowWithoutActivation {get{return true;}}
 internal Pet(Preferences state, bool test=false){testMode=test;
  state.Width=Math.Max(64,Math.Min(600,state.Width));State=state;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;Width=state.Width;Height=state.Width;
  var area=Screen.PrimaryScreen.WorkingArea;Location=state.X==Int32.MinValue?new Point(area.Right-Width-32,area.Bottom-Height-24):new Point(state.X,state.Y);
  Clamp();
  var menu=new ContextMenuStrip(); menu.Items.Add("開啟控制台",null,(s,e)=>OpenPanel());menu.Items.Add("切換遊戲模式　F7",null,(s,e)=>SetGame(!Game));menu.Items.Add("切換螢幕　F8",null,(s,e)=>SwitchMonitor());menu.Items.Add("顯示／隱藏桌寵",null,(s,e)=>Visible=!Visible);menu.Items.Add("結束",null,(s,e)=>Close());
  ContextMenuStrip=menu;tray=new NotifyIcon{Icon=SystemIcons.Application,Text="CornerPet",ContextMenuStrip=menu,Visible=true};tray.DoubleClick+=(s,e)=>OpenPanel();
  MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left&&!Game){dragging=true;grab=e.Location;Capture=true;}};
  MouseMove+=(s,e)=>{if(dragging){Location=new Point(Cursor.Position.X-grab.X,Cursor.Position.Y-grab.Y);Draw();}};
  MouseUp+=(s,e)=>{if(dragging){dragging=false;Capture=false;Clamp();Remember();}};
  MouseDoubleClick+=(s,e)=>OpenPanel();
  timer=new Timer();timer.Tick+=(s,e)=>{if(!Visible||asset==null)return;frame=(frame+1)%FrameCount;asset.Image.SelectActiveFrame(FrameDimension.Time,frame);Draw();Schedule();};
  Shown+=(s,e)=>{if(!testMode){shortcuts=new GlobalShortcuts(id=>{if(id==1){modeKeyCount++;SetGame(!Game);}else{screenKeyCount++;SwitchMonitor();}ShortcutDiagnostics();});HotkeyReady=shortcuts.ModeReady;MonitorHotkeyReady=shortcuts.MonitorReady;ShortcutDiagnostics();}SetGame(state.Game);if(!String.IsNullOrEmpty(state.ImagePath)){try{LoadImage(state.ImagePath);}catch(Exception ex){warning="上次的圖片無法讀取："+ex.Message;}}Draw();Schedule();if(!testMode&&asset==null)OpenPanel();};
 }
 protected override void WndProc(ref Message m){if(m.Msg==0x312){int id=m.WParam.ToInt32();if(id==1){SetGame(!Game);return;}if(id==2){SwitchMonitor();return;}}if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
 internal void SetGame(bool game){Game=game;State.Game=game;int style=Native.GetWindowLong(Handle,-20);Native.SetWindowLong(Handle,-20,game?style|0x20:style&~0x20);tray.Text=game?"CornerPet · 遊戲模式（滑鼠穿透）":"CornerPet · 一般模式（可拖曳）";Remember();if(panel!=null&&!panel.IsDisposed)panel.RefreshStatus();}
 internal void LoadImage(string path){
  var candidate=ImageAsset.Open(path);var previous=asset;int oldFrame=frame,oldCount=FrameCount;string oldPath=State.ImagePath;int oldWidth=Width,oldHeight=Height;
  try {asset=candidate;FrameCount=asset.Count;frame=0;State.ImagePath=Path.GetFullPath(path);ResizePet(State.Width);if(!Visible)asset.Suspend();Schedule();}
  catch {asset=previous;frame=oldFrame;FrameCount=oldCount;State.ImagePath=oldPath;Width=oldWidth;Height=oldHeight;State.Width=oldWidth;candidate.Dispose();ReleaseSurface();Draw();Schedule();throw;}
  if(previous!=null)previous.Dispose();Remember();
 }
 internal Color RenderPixel(int x,int y){if(surface==null)throw new InvalidOperationException("No render surface");return surface.Sample(x,y);}
 internal bool HasAsset {get{return asset!=null;}}
 internal void ClearAsset(){timer.Stop();if(asset!=null)asset.Dispose();asset=null;State.ImagePath="";FrameCount=0;frame=0;Draw();Remember();}
 void Schedule(){if(timer==null)return;timer.Stop();if(!Visible||IsDisposed||asset==null||FrameCount<=1)return;timer.Interval=asset.Delays[frame];timer.Start();}
 void ReleaseSurface(){if(surface!=null){surface.Dispose();surface=null;}}
 protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(timer==null)return;if(!Visible){timer.Stop();ReleaseSurface();if(asset!=null)asset.Suspend();}else{if(asset!=null)asset.Resume(frame);Draw();Schedule();}}
 internal void ResizePet(int width){State.Width=Math.Max(64,Math.Min(600,width));Width=State.Width;Height=asset==null?Width:Math.Max(1,(int)Math.Round((double)Width*asset.Height/asset.Width));if(Height>900){Height=900;Width=(int)Math.Round(900.0*asset.Width/asset.Height);State.Width=Width;}Clamp();Draw();Remember();}
 internal void SwitchMonitor(){var all=Screen.AllScreens;var current=Screen.FromRectangle(Bounds);var displays=Array.ConvertAll(all,s=>new DisplayInfo{Name=s.DeviceName,Primary=s.Primary,Work=s.WorkingArea});int index=ScreenPlacement.Target(current.DeviceName,previousSecondary,displays);if(index<0)return;if(!current.Primary)previousSecondary=current.DeviceName;else previousSecondary=all[index].DeviceName;Location=ScreenPlacement.Map(Bounds,current.WorkingArea,all[index].WorkingArea);Draw();Remember();}
 void ShortcutDiagnostics(){try{Directory.CreateDirectory(Preferences.Folder);File.WriteAllText(Path.Combine(Preferences.Folder,"shortcuts-status.txt"),"version=0.3.4\nbackend="+shortcuts.Backend+"\nF7="+HotkeyReady+"\nF8="+MonitorHotkeyReady+"\nF7Actions="+modeKeyCount+"\nF8Actions="+screenKeyCount+"\nerror="+shortcuts.Failure);}catch{}}
 internal string ShortcutStatus {get{return (HotkeyReady?"F7 模式":"F7 無法監聽")+" / "+(MonitorHotkeyReady?"F8 螢幕":"F8 無法監聽");}}
 internal void Corner(bool right,bool bottom){var a=Screen.FromPoint(Cursor.Position).WorkingArea;Location=new Point(right?a.Right-Width-20:a.Left+20,bottom?a.Bottom-Height-20:a.Top+20);Draw();Remember();}
 void Clamp(){var a=Screen.FromRectangle(Bounds).WorkingArea;Location=new Point(Math.Max(a.Left,Math.Min(Left,a.Right-Width)),Math.Max(a.Top,Math.Min(Top,a.Bottom-Height)));}
 void Remember(){if(testMode)return;State.X=Left;State.Y=Top;try{State.Save();}catch(Exception ex){warning="設定未能儲存："+ex.Message;}}
 internal string Status {get{return (Game?"遊戲模式 · 滑鼠會穿透桌寵":"一般模式 · 按住角色即可拖曳")+"\n"+(HotkeyReady?"F7 切換模式 / F8 切換螢幕":"快捷鍵被其他程式佔用，請用系統匣切換")+(warning==""?"":"\n"+warning);}}
 internal void OpenPanel(){if(panel==null||panel.IsDisposed){panel=new PanelWindow(this);panel.FormClosed+=(s,e)=>panel=null;}panel.RefreshStatus();panel.Show();panel.Activate();}
 internal void OpenLibrary(){if(library==null||library.IsDisposed){library=new LibraryWindow(this);library.FormClosed+=(s,e)=>library=null;}library.Show();library.Activate();}
 internal void Draw(){if(!IsHandleCreated||IsDisposed||!Visible)return;
  if(asset==null){if(surface!=null){surface.Graphics.ResetTransform();surface.Graphics.Clear(Color.Transparent);surface.Present(Handle,Left,Top);ReleaseSurface();}return;}
  if(surface==null||surface.Width!=Width||surface.Height!=Height){ReleaseSurface();surface=new RenderSurface(Width,Height);}
  var g=surface.Graphics;g.ResetTransform();g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
  g.DrawImage(asset.Image,new Rectangle(0,0,Width,Height));surface.Present(Handle,Left,Top);DrawCount++;
 }
 protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();timer.Dispose();if(shortcuts!=null)shortcuts.Dispose();tray.Visible=false;tray.Dispose();if(panel!=null)panel.Dispose();if(library!=null)library.Dispose();if(asset!=null)asset.Dispose();ReleaseSurface();ContextMenuStrip.Dispose();Remember();base.OnFormClosed(e);}
}
internal sealed class PanelWindow : Form {
 readonly Pet pet;Label status,source,widthValue;Button mode;static readonly Font panelFont=new Font("Microsoft JhengHei UI",10),dataFont=new Font("Consolas",11);
 static readonly Color background=Color.FromArgb(27,30,34),surfaceColor=Color.FromArgb(39,43,48),lineColor=Color.FromArgb(65,71,78),textColor=Color.FromArgb(226,230,234),muted=Color.FromArgb(151,160,169),accent=Color.FromArgb(225,180,75);
 internal PanelWindow(Pet p){pet=p;Text="CornerPet | 控制台 v0.3.4";ClientSize=new Size(420,376);Font=panelFont;BackColor=background;ForeColor=textColor;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
  Label("模式",20,18,200,22,muted);
  status=Label("",20,44,214,24,textColor);
  Label(pet.ShortcutStatus,20,74,214,20,pet.HotkeyReady&&pet.MonitorHotkeyReady?muted:accent).Font=dataFont;
  mode=Button("",240,24,160,44,()=>pet.SetGame(!pet.Game));
  Rule(108);
  Label("素材",20,125,56,24,muted);source=Label("",86,125,314,24,textColor);source.AutoEllipsis=true;
  Button("匯入圖片",20,160,184,36,()=>{using(var d=new OpenFileDialog{Filter="圖片|*.gif;*.png;*.jpg;*.jpeg;*.bmp",Title="匯入桌寵素材"})if(d.ShowDialog()==DialogResult.OK)try{pet.LoadImage(d.FileName);RefreshStatus();}catch(Exception ex){MessageBox.Show(this,ex.Message,"匯入失敗",MessageBoxButtons.OK,MessageBoxIcon.Warning);}});

  Button("素材庫",216,160,184,36,()=>pet.OpenLibrary());
  Label("寬度",20,221,90,24,muted);widthValue=Label("",130,221,90,24,textColor);widthValue.Font=dataFont;
  Button("−",240,214,74,36,()=>{pet.ResizePet(pet.State.Width-20);RefreshStatus();});Button("＋",326,214,74,36,()=>{pet.ResizePet(pet.State.Width+20);RefreshStatus();});
  Label("位置",20,275,90,24,muted);Button("左下角",130,268,129,36,()=>pet.Corner(false,true));Button("右下角",271,268,129,36,()=>pet.Corner(true,true));
  Rule(320);Button("收起",20,332,184,36,()=>Close());Button("結束程式",216,332,184,36,()=>pet.Close());
  ActiveControl=mode;RefreshStatus();
 }
 Label Label(string text,int x,int y,int width,int height,Color color){var label=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=color,TextAlign=ContentAlignment.MiddleLeft};Controls.Add(label);return label;}
 void Rule(int y){Controls.Add(new Label{Location=new Point(20,y),Size=new Size(380,1),BackColor=lineColor});}
 Button Button(string text,int x,int y,int width,int height,Action action){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,height),FlatStyle=FlatStyle.Flat,BackColor=surfaceColor,ForeColor=textColor,UseVisualStyleBackColor=false,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=lineColor;b.FlatAppearance.MouseOverBackColor=Color.FromArgb(53,59,66);b.FlatAppearance.MouseDownBackColor=Color.FromArgb(65,72,80);b.Click+=(s,e)=>action();Controls.Add(b);return b;}
 internal void RefreshStatus(){status.Text=pet.Game?"遊戲 / 滑鼠穿透":"一般 / 可拖曳";status.ForeColor=pet.Game?accent:textColor;mode.Text=pet.Game?"切換一般模式":"啟用遊戲模式";mode.FlatAppearance.BorderColor=pet.Game?accent:lineColor;source.Text=pet.HasAsset?Path.GetFileName(pet.State.ImagePath):"未匯入素材";widthValue.Text=pet.State.Width+" px";}
}
internal static class Program {
 [STAThread] static int Main(string[] args){Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>0&&args[0]=="--self-test")return SelfTest.Run();
  if(args.Length>0&&args[0]=="--library-test")return LibraryTests.Run();
  bool first;using(var mutex=new System.Threading.Mutex(true,"Local\\CornerPet.Desktop.v1",out first)){if(!first){MessageBox.Show("CornerPet 正在執行，請先從系統匣結束舊程式，再重新開啟 v0.3.4。");return 0;}var pet=new Pet(Preferences.Load());if(args.Length>0&&args[0]=="--control-panel")pet.Shown+=(s,e)=>pet.OpenPanel();Application.Run(pet);}return 0;
 }
}
