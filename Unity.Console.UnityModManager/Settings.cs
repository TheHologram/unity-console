using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace Unity.Console.UnityModManager
{
    public enum ShowHide
    {
        Hide = 0,
        Show = 1,
    }
    public enum SettingType
    {
        Default = 0,
        IniSettings = 1,
    }



    public class Settings : UnityModManagerNet.UnityModManager.ModSettings, IDrawable
    {
        [Draw("Settings File", DrawType.ToggleGroup)] public SettingType File = SettingType.Default;
        [Draw("Show Console", VisibleOn = "File|Default")] public ShowHide ShowHide = ShowHide.Hide;
        [Draw("Hide/Show Key", VisibleOn = "File|Default")] public KeyBinding ShowKey = new KeyBinding { keyCode = KeyCode.F12 };

        [Draw("Monitor", VisibleOn = "File|Default")] public int Monitor = -1;
        [Draw("Monitor Width", VisibleOn = "File|Default")] public int MonitorWidth = -1;
        [Draw("Monitor Height", VisibleOn = "File|Default")] public int MonitorHeight = -1;

        [Draw("Start Wait Time (s)", VisibleOn = "File|Default")] public int StartWaitTime = 0;
        [Draw("Error Wait Time (s)", VisibleOn = "File|Default")] public int ErrorWaitTime = 10;
        public override void Save(UnityModManagerNet.UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
        }
    }

}
