using System;using System.Drawing;
internal sealed class DisplayInfo {internal string Name;internal Rectangle Work;internal bool Primary;}
internal static class ScreenPlacement {
 internal static int Target(string current,string previousSecondary,DisplayInfo[] screens){if(screens.Length<2)return -1;int primary=Array.FindIndex(screens,s=>s.Primary);if(primary<0)primary=0;if(screens[primary].Name!=current)return primary;int remembered=Array.FindIndex(screens,s=>!s.Primary&&s.Name==previousSecondary);if(remembered>=0)return remembered;return Array.FindIndex(screens,s=>!s.Primary);}
 internal static Point Map(Rectangle pet,Rectangle from,Rectangle to){double x=from.Width>pet.Width?(double)(pet.Left-from.Left)/(from.Width-pet.Width):0;double y=from.Height>pet.Height?(double)(pet.Top-from.Top)/(from.Height-pet.Height):0;x=Math.Max(0,Math.Min(1,x));y=Math.Max(0,Math.Min(1,y));return new Point(to.Left+(int)Math.Round(x*Math.Max(0,to.Width-pet.Width)),to.Top+(int)Math.Round(y*Math.Max(0,to.Height-pet.Height)));}
}
