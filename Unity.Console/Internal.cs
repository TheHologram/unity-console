using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Scripting;
using Microsoft.Scripting.Runtime;

namespace Unity.Console
{
    public static class Internal
    {
        public static bool GetPrivateProfileSection(string appName, string fileName, out string[] section)
        {
            section = null;

            if (!File.Exists(fileName))
                return false;

            int MAX_BUFFER = 32767;
            var bytes = new byte[MAX_BUFFER];
            int nbytes = GetPrivateProfileSection(appName, bytes, MAX_BUFFER, fileName);
            if ((nbytes == MAX_BUFFER - 2) || (nbytes == 0))
                return false;
            section = Encoding.ASCII.GetString(bytes, 0, nbytes).Trim('\0').Split('\0');
            return true;
        }

        internal static string[] GetPrivateProfileSection(string lpAppName, string lpFileName)
        {
            return GetPrivateProfileSection(lpAppName, lpFileName, out var section) ? section : new string[0];
        }

        internal static string GetScriptFromSection(string lpAppName, string lpFileName)
        {
            return GetScriptFromLines(GetPrivateProfileSection(lpAppName, lpFileName));
        }

        internal static string GetScriptFromLines(string[] lines)
        {
            if (lines == null || lines.Length == 0)
                return null;

            var trimmedlines = lines.Where(x => !x.TrimStart().StartsWith("#") && !x.TrimStart().StartsWith(";")).SkipWhile(string.IsNullOrEmpty).ToArray();
            if (trimmedlines.Length > 0)
            {
                return string.Join("\r\n", trimmedlines);
            }

            return null;
        }

        internal delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RectStruct
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        internal const int CCHDEVICENAME = 32;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MonitorInfoEx
        {
            public int Size;
            public RectStruct Monitor;
            public RectStruct WorkArea;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string DeviceName;
        }

        public class DisplayInfo
        {
            public string Availability { get; set; }
            public string ScreenHeight { get; set; }
            public string ScreenWidth { get; set; }
            public RectStruct MonitorArea { get; set; }
            public RectStruct WorkArea { get; set; }
        }

        public class DisplayInfoCollection : List<DisplayInfo>
        {
        }

        public static DisplayInfoCollection GetDisplays()
        {
            DisplayInfoCollection col = new DisplayInfoCollection();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData)
                {
                    var mi = new MonitorInfoEx();
                    mi.Size = Marshal.SizeOf(mi);
                    var success = GetMonitorInfo(hMonitor, ref mi);
                    if (success)
                    {
                        var di = new DisplayInfo
                        {
                            ScreenWidth = (mi.Monitor.right - mi.Monitor.left).ToString(),
                            ScreenHeight = (mi.Monitor.bottom - mi.Monitor.top).ToString(), MonitorArea = mi.Monitor,
                            WorkArea = mi.WorkArea, Availability = mi.Flags.ToString()
                        };
                        col.Add(di);
                        //DebugLog($"Monitor: {di.ScreenWidth}x{di.ScreenHeight} {di.WorkArea} {di.Availability} ");
                    }

                    return true;
                }, IntPtr.Zero);
            return col;
        }

        [DllImport("user32.dll")]
        internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum,
            IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RectStruct lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("kernel32.dll")]
        internal static extern int GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);

        [DllImport("kernel32.dll")]
        internal static extern int GetPrivateProfileSection(string lpAppName, byte[] lpReturnedString, int nSize, string lpFileName);


        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            int nSize,
            string lpFileName);

        internal static string GetPrivateProfileString(string lpAppName,string lpKeyName,string lpDefault,string lpFileName)
        {
            var sb = new StringBuilder(4096) { Length = 0, Capacity = 4096 };
            return 0 < Internal.GetPrivateProfileString(lpAppName, lpKeyName, lpDefault, sb, sb.Capacity, lpFileName) ? sb.ToString().Trim() : lpDefault;
        }

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetFocus();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AllocConsole();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        internal static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        internal static extern bool SetConsoleTitle(string lpConsoleTitle);

        [DllImport("kernel32.dll")]
        internal static extern bool SetConsoleCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        internal static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        internal const int SW_RESTORE = 9;
        internal const int SW_SHOW = 5;

        [DllImport("user32.dll")]
        internal static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        internal static extern bool DrawMenuBar(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        internal const Int32 MF_GRAYED = 0x1;
        internal const Int32 SC_CLOSE = 0xF060;
    }
}