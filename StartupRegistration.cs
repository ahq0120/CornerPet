using System;using System.IO;using Microsoft.Win32;
internal sealed class StartupRegistration {
 internal const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
 readonly RegistryKey root;readonly string keyPath;internal readonly string Command;
 internal StartupRegistration(string executable,RegistryKey root=null,string keyPath=RunKey){this.root=root??Registry.CurrentUser;this.keyPath=keyPath;string full=Path.GetFullPath(executable);if(full.IndexOf('"')>=0)throw new ArgumentException("啟動路徑無效。");Command="\""+full+"\" --startup";}
 internal string RegisteredCommand {get{using(var key=root.OpenSubKey(keyPath))return key==null?null:key.GetValue("CornerPet",null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string;}}
 internal bool Enabled {get{return String.Equals(RegisteredCommand,Command,StringComparison.OrdinalIgnoreCase);}}
 internal void SetEnabled(bool enabled){if(enabled){string executable=Command.Substring(1,Command.IndexOf('"',1)-1);if(!File.Exists(executable))throw new FileNotFoundException("找不到桌寵程式，無法設定開機啟動。",executable);using(var key=root.CreateSubKey(keyPath)){key.SetValue("CornerPet",Command,RegistryValueKind.String);}}else{using(var key=root.OpenSubKey(keyPath,true))if(key!=null)key.DeleteValue("CornerPet",false);}}
}
