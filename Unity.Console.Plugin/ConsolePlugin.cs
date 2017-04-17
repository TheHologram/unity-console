using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using IllusionPlugin;
using UnityEngine;

namespace Unity.Console.Plugin
{
    internal class ConsolePlugin : IPlugin
    {
        private Assembly consoleAssembly;
        private Thread runThread;
        static readonly string[] _filters = new string[0];
        static readonly string _rootPath = null;
        static readonly string _consolePath = null;
        static readonly string _iniPath = null;
        static bool _enable = false;

        static readonly KeyCode ShowKey = KeyCode.BackQuote;
        static readonly bool ShowKeyControl = true;
        static readonly bool ShowKeyAlt = false;
        static readonly bool ShowKeyShift = false;
        static bool _focusWindow = false;
        static bool _startHidden = false;

        static ConsolePlugin()
        {
            var exeAsm = Assembly.GetExecutingAssembly();
            if (File.Exists(exeAsm.Location))
            {
                _rootPath = Path.GetDirectoryName(exeAsm.Location);
                _consolePath = Path.Combine(_rootPath, "Console");
                _iniPath = Path.GetFullPath(Path.Combine(_consolePath, @"Console.ini"));
                if (File.Exists(_iniPath))
                {
                    _enable = GetPrivateProfileInt("System", "Enable", 0, _iniPath) != 0;
                    if (_enable)
                    {
                        var sb = new StringBuilder(4096);
                        if (0 < GetPrivateProfileString("Console", "Filter", "", sb, sb.Capacity, _iniPath))
                        {
                            _filters = sb.ToString().Split(",;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        }

                        sb.Length = 0;
                        try
                        {
                            if (0 < GetPrivateProfileString("Console", "ShowKey", "BackQuote", sb, sb.Capacity, _iniPath))
                                ShowKey = (KeyCode)Enum.Parse(typeof(KeyCode), sb.ToString());
                        }
                        catch
                        {
                        }
                        ShowKeyControl  = GetPrivateProfileInt("Console", "ShowKeyControl", 1, _iniPath) != 0;
                        ShowKeyAlt = GetPrivateProfileInt("Console", "ShowKeyAlt", 0, _iniPath) != 0;
                        ShowKeyShift = GetPrivateProfileInt("Console", "ShowKeyShift", 0, _iniPath) != 0;
                    }
                }
            }
        }


        public ConsolePlugin()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            this.Version = asm.GetName().Version.ToString();
        }

        public static bool Initialized { get; set; }

        public string[] Filter => _filters;

        public string Name => "UC.Console";

        public string Version { get; set; } = "0.1.0.0";

        public void OnApplicationQuit()
        {
            _enable = false;
            Shutdown();
        }

        public void OnApplicationStart()
        {
            try
            {
                if (!_enable) return;

                if (runThread != null) return;

                if (!string.IsNullOrEmpty(_iniPath) && File.Exists(_iniPath))
                {
                    string[] lines;
                    if (GetPrivateProfileSection("Preload.Assemblies", _iniPath, out lines))
                    {
                        foreach (var line in lines)
                        {
                            var asmname = line.Trim();
                            if (string.IsNullOrEmpty(asmname) || asmname.StartsWith(";") || asmname.StartsWith("#"))
                                continue;

                            AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(Path.GetFullPath(Path.Combine(_consolePath, asmname))));
                            //if (!cmdline.AddAssembly(asmname))
                            //    stdwriter.WriteLine("Error adding assembly: " + asmname);
                        }

                    }

                    var showAtStartup = GetPrivateProfileInt("Console", "ShowAtStartup", 0, _iniPath) != 0;
                    if (showAtStartup)
                    {
                        Startup(false,false);
                    }
                    else
                    {
                        var startHidden = GetPrivateProfileInt("Console", "StartHidden", 0, _iniPath) != 0;
                        if (startHidden)
                            Startup(false, true);
                    }
                }
            }
            catch
            {
            }
        }

        private void Startup(bool focus, bool hidden)
        {
            _focusWindow = focus;
            _startHidden = hidden;
            if (runThread != null)
                return;
            var dllPath = Path.GetFullPath(Path.Combine(_consolePath, @"Unity.Console.dll"));
            if (File.Exists(dllPath))
            {
                consoleAssembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(dllPath));
                runThread = new Thread(RunThread) {IsBackground = true};
                runThread.Start();
            }
        }

        private void Close()
        {
            if (runThread == null)
                return;

            try
            {
                if (consoleAssembly == null) return;
                var progType = consoleAssembly.GetType("Unity.Console.Program", false, true);
                if (progType != null)
                {
                    var initMethod = progType.GetMethod("Close",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
                    initMethod.Invoke(null, new object[0]);
                }
                runThread?.Abort();
                runThread = null;
                //runThread?.Join(1000);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Exception: " + ex.Message);
                // ignored
            }
            finally
            {
            }
        }

        private void Shutdown()
        {
            if (runThread == null)
                return;

            try
            {
                if (consoleAssembly == null) return;
                var progType = consoleAssembly.GetType("Unity.Console.Program", false, true);
                if (progType != null)
                {
                    var initMethod = progType.GetMethod("Shutdown",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
                    initMethod.Invoke(null, new object[0]);
                }
                runThread?.Abort();
                runThread = null;
                //runThread?.Join(1000);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Exception: " + ex.Message);
                // ignored
            }
            finally
            {
            }
        }

        private void Toggle()
        {
            if (runThread == null)
            {
                Startup(true, false);
            }
            else
            {
                Close();
            }
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnUpdate()
        {
            if (!_enable)
                return;

            if (Input.GetKeyDown(ShowKey))
            {
                bool ControlDown = GetAsyncKeyState(0xA2) != 0 || GetAsyncKeyState(0xA3) != 0;
                bool AltDown = GetAsyncKeyState(0xA4) != 0 || GetAsyncKeyState(0xA5) != 0;
                bool ShiftDown = GetAsyncKeyState(0xA0) != 0 || GetAsyncKeyState(0xA1) != 0;
                //System.Console.WriteLine("OnUpdate {0} | {1} {2} {3} | {4} {5} {6}"
                //    , ShowKey
                //    , ControlDown, AltDown, ShiftDown
                //    , ShowKeyControl ^ ControlDown
                //    , ShowKeyAlt ^ AltDown
                //    , ShowKeyShift ^ ShiftDown
                //    );
                //System.Console.WriteLine("OnUpdate" + ControlDown + " " + AltDown + " " + ShiftDown);
                if (true
                    && !(ShowKeyControl ^ ControlDown)
                    && !(ShowKeyAlt ^ AltDown)
                    && !(ShowKeyShift ^ ShiftDown)
                    )
                {
                    Toggle();
                }
            }
        }

        private void RunThread()
        {
            try
            {
                if (consoleAssembly == null) return;

                var progType = consoleAssembly.GetType("Unity.Console.Program", false, true);
                if (progType != null)
                {
                    var initMethod = progType.GetMethod("Initialize",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
                    initMethod.Invoke(null, new object[] { _startHidden, _focusWindow });
                }
            }
            catch (Exception)
            {
                // ignored
            }
            finally
            {
                runThread = null;
            }
        }


        [DllImport("kernel32.dll")]
        static extern int GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            int nSize,
            string lpFileName);

        [DllImport("kernel32.dll")]
        static extern int GetPrivateProfileSection(string lpAppName, byte[] lpReturnedString, int nSize, string lpFileName);

        public static bool GetPrivateProfileSection(string appName, string fileName, out string[] section)
        {
            section = null;

            if (!System.IO.File.Exists(fileName))
                return false;

            int MAX_BUFFER = 32767;
            var bytes = new byte[MAX_BUFFER];
            int nbytes = GetPrivateProfileSection(appName, bytes, MAX_BUFFER, fileName);
            if ((nbytes == MAX_BUFFER - 2) || (nbytes == 0))
                return false;
            section = Encoding.ASCII.GetString(bytes, 0, nbytes).Trim('\0').Split('\0');
            return true;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

    }
}