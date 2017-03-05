using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using IllusionPlugin;

namespace Unity.Console.Plugin
{
    internal class ConsolePlugin : IEnhancedPlugin, IPlugin
    {
        private Assembly consoleAssembly;
        private Thread runThread;
        static readonly string[] _filters = new string[0];
        static readonly string _rootPath = null;
        static readonly string _consolePath = null;
        static readonly string _iniPath = null;
        static bool _enable = false;

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
            try
            {
                _enable = false;
                if (consoleAssembly == null) return;
                var progType = consoleAssembly.GetType("Unity.Console.Program", false, true);
                if (progType != null)
                {
                    var initMethod = progType.GetMethod("Shutdown",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
                    initMethod.Invoke(null, new object[0]);
                }
                runThread?.Abort();
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
            //Unity.Console.Program.Shutdown();
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

                    var dllPath = Path.GetFullPath(Path.Combine(_consolePath, @"Unity.Console.dll"));
                    if (File.Exists(dllPath))
                    {
                        consoleAssembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(dllPath));
                        runThread = new Thread(RunThread) {IsBackground = true};
                        runThread.Start();
                    }
                }
            }
            catch
            {
            }
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLateUpdate()
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
                    initMethod.Invoke(null, new object[0]);
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

    }
}