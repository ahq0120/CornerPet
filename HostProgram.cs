using System;using System.Diagnostics;using System.Drawing;using System.IO;using System.Linq;using System.Net;using System.Net.NetworkInformation;using System.Net.Sockets;using System.Security.Cryptography;using System.Threading.Tasks;using System.Windows.Forms;using System.Xml.Serialization;
public sealed class HostOptions {
 public string Bind="127.0.0.1",ProtectedKey="";public int Port=34827;
 internal static string PathName {get{return Path.Combine(Preferences.Folder,"library-host.xml");}}
 internal static HostOptions Load(){try{using(var f=File.OpenRead(PathName))return (HostOptions)new XmlSerializer(typeof(HostOptions)).Deserialize(f);}catch{return new HostOptions();}}
 internal void Save(){Directory.CreateDirectory(Preferences.Folder);string temporary=PathName+".tmp";using(var f=File.Create(temporary))new XmlSerializer(typeof(HostOptions)).Serialize(f,this);if(File.Exists(PathName))File.Replace(temporary,PathName,null);else File.Move(temporary,PathName);}
 internal string Key {get{return new LibraryOptions{ProtectedKey=ProtectedKey}.Key;}set{var options=new LibraryOptions();options.Key=value;ProtectedKey=options.ProtectedKey;}}
}
internal static class HostFolders {
 internal static ProcessStartInfo Prepare(string folder){
  string full=System.IO.Path.GetFullPath(folder);Directory.CreateDirectory(full);
  if(!Directory.Exists(full))throw new IOException("素材資料夾無法建立："+full);
  string explorer=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe");
  return new ProcessStartInfo(explorer,"\""+full+"\""){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Normal};
 }
 internal static void Open(IWin32Window owner,string folder){try{Process.Start(Prepare(folder));}catch(Exception ex){MessageBox.Show(owner,"無法開啟素材資料夾：\n"+folder+"\n\n"+ex.Message,"資料夾開啟失敗",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
}
internal sealed class HostWindow : Form {
 ComboBox bind;TextBox port,key;Button start,stop,refresh,remove,add;Label status,address;ListView assets;LibraryServer server;bool importing;int page;Label pages;Button previous,next;readonly string folder=System.IO.Path.Combine(Preferences.Folder,"SharedLibrary");
 internal HostWindow(){LibraryStyle.Form(this,"CornerPet | 素材庫主機",640,548);var settings=HostOptions.Load();
  LibraryStyle.Label(this,"監聽位址",16,16,90);bind=new ComboBox{Location=new Point(108,16),Width=242,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=LibraryStyle.Surface,ForeColor=LibraryStyle.Ink};bind.Items.Add("127.0.0.1");foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())if(adapter.OperationalStatus==OperationalStatus.Up)foreach(var entry in adapter.GetIPProperties().UnicastAddresses)if(entry.Address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(entry.Address)&&!bind.Items.Contains(entry.Address.ToString()))bind.Items.Add(entry.Address.ToString());Controls.Add(bind);string preferred=bind.Items.Cast<string>().FirstOrDefault(ip=>ip.StartsWith("26."));bind.SelectedItem=bind.Items.Contains(settings.Bind)&&settings.Bind!="127.0.0.1"?settings.Bind:preferred??"127.0.0.1";
  LibraryStyle.Label(this,"連接埠",368,16,72);port=LibraryStyle.Input(this,settings.Port.ToString(),446,16,178);
  LibraryStyle.Label(this,"共享密碼",16,56,90);string secret=settings.Key;if(String.IsNullOrEmpty(secret)){byte[] random=new byte[24];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(random);secret=Convert.ToBase64String(random);}key=LibraryStyle.Input(this,secret,108,56,400,true);var show=new CheckBox{Text="顯示",Location=new Point(530,56),Size=new Size(94,28)};show.CheckedChanged+=(s,e)=>key.UseSystemPasswordChar=!show.Checked;Controls.Add(show);
  start=LibraryStyle.Button(this,"啟動服務",16,102,140,(s,e)=>Start());stop=LibraryStyle.Button(this,"停止服務",168,102,140,(s,e)=>Stop());stop.Enabled=false;status=LibraryStyle.Label(this,"已停止",324,108,300);
  address=LibraryStyle.Label(this,"選擇 Radmin VPN 的 26.x 位址供朋友連線。",16,149,608);
  LibraryStyle.Button(this,"複製位址",16,184,140,(s,e)=>{if(server!=null)Clipboard.SetText(server.Address);});LibraryStyle.Button(this,"開啟素材資料夾",168,184,184,(s,e)=>HostFolders.Open(this,folder));
  assets=new ListView{Location=new Point(16,236),Size=new Size(608,206),View=View.Details,FullRowSelect=true,MultiSelect=false,BackColor=LibraryStyle.Surface,ForeColor=LibraryStyle.Ink,HideSelection=false};assets.Columns.Add("素材",340);assets.Columns.Add("尺寸",120);assets.Columns.Add("KB",100);Controls.Add(assets);
  add=LibraryStyle.Button(this,"加入圖片",16,454,116,async(s,e)=>await Import());refresh=LibraryStyle.Button(this,"重新整理",144,454,116,(s,e)=>Reload());remove=LibraryStyle.Button(this,"刪除選取",272,454,116,(s,e)=>{if(server==null||assets.SelectedItems.Count==0)return;var item=(AssetInfo)assets.SelectedItems[0].Tag;if(MessageBox.Show(this,"刪除「"+item.Name+"」？已下載到朋友電腦的副本不會刪除。","刪除素材",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes){try{server.Store.Delete(item.Id);Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message,"刪除失敗");}}});
  previous=LibraryStyle.Button(this,"‹",400,454,48,(s,e)=>{if(page>0){page--;Reload();}});next=LibraryStyle.Button(this,"›",460,454,48,(s,e)=>{page++;Reload();});pages=LibraryStyle.Label(this,"",520,459,104);
  LibraryStyle.Label(this,"單檔 20 MB。關閉本視窗會停止服務，素材會保留。",16,508,608);SetControls(false);
 }
 void Start(){try{int value;if(!Int32.TryParse(port.Text,out value)||value<1024||value>65535)throw new ArgumentException("連接埠需介於 1024～65535。");server=new LibraryServer(IPAddress.Parse((string)bind.SelectedItem),value,key.Text,folder);var settings=new HostOptions{Bind=(string)bind.SelectedItem,Port=value};settings.Key=key.Text;settings.Save();status.Text="服務運作中";status.ForeColor=LibraryStyle.Accent;address.Text=server.Address+(bind.SelectedItem.ToString()=="127.0.0.1"?"  （僅限本機；朋友需改用 VPN 位址）":"");SetControls(true);Reload();}catch(Exception ex){if(server!=null){server.Dispose();server=null;}SetControls(false);MessageBox.Show(this,ex.Message,"啟動失敗");}}
 void Stop(){if(server!=null){server.Dispose();server=null;}status.Text="已停止";status.ForeColor=LibraryStyle.Muted;assets.Items.Clear();SetControls(false);}
 void SetControls(bool running){start.Enabled=!running;stop.Enabled=running;bind.Enabled=port.Enabled=key.Enabled=!running;add.Enabled=refresh.Enabled=remove.Enabled=running&&!importing;previous.Enabled=next.Enabled=false;}
 void Reload(){if(server==null)return;var result=server.Store.List(page);if(page>0&&result.Items.Length==0){page--;Reload();return;}assets.Items.Clear();foreach(var item in result.Items){var row=new ListViewItem(item.Name){Tag=item};row.SubItems.Add(item.Width+"×"+item.Height);row.SubItems.Add((item.Bytes/1024).ToString());assets.Items.Add(row);}pages.Text=(page+1)+" / "+Math.Max(1,(result.Total+11)/12);previous.Enabled=page>0;next.Enabled=(page+1)*12<result.Total;}
 async Task Import(){if(server==null||importing)return;using(var picker=new OpenFileDialog{Filter="圖片|*.gif;*.png;*.jpg;*.jpeg;*.bmp",Multiselect=true,Title="加入共享素材"}){if(picker.ShowDialog(this)!=DialogResult.OK)return;importing=true;SetControls(true);stop.Enabled=false;try{var store=server.Store;foreach(string path in picker.FileNames){await Task.Run(()=>store.Import(path,System.IO.Path.GetFileName(path)));}page=0;Reload();}catch(Exception ex){if(!IsDisposed)MessageBox.Show(this,ex.Message,"加入失敗");}finally{importing=false;if(!IsDisposed)SetControls(server!=null);}}}
 protected override void OnFormClosed(FormClosedEventArgs e){Stop();base.OnFormClosed(e);}
}
internal static class HostProgram {
 [STAThread] static int Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\CornerPet.LibraryHost.v1",out created)){if(!created){MessageBox.Show("素材庫主機已開啟。");return 0;}try{Application.Run(new HostWindow());}catch(Exception ex){MessageBox.Show(ex.Message,"主機錯誤");return 1;}}return 0;}
}
