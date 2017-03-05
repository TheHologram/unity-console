using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using IronPython.Hosting;
using IronPython.Modules;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Shell;
using UC;
using UnityEngine;

//using Unity.Console.Commands;

namespace Unity.Console
{
    public static class Program
    {
        //private static UnityCommandLine cmdline;
        static readonly string _rootPath = null;
        static readonly string _iniPath = null;
        static readonly int _startdelay = 2000;
        static bool _enable = false;

        static Program()
        {
            var exeAsm = Assembly.GetExecutingAssembly();
            if (File.Exists(exeAsm.Location))
            {
                _rootPath = Path.GetDirectoryName(exeAsm.Location);
                _iniPath = Path.GetFullPath(Path.Combine(_rootPath, @"Console.ini"));
                if (File.Exists(_iniPath))
                {
                    _enable = GetPrivateProfileInt("Console", "Enable", 0, _iniPath) != 0;
                    _startdelay = GetPrivateProfileInt("Console", "StartDelay", 2000, _iniPath);
                }
            }
        }

        public static void Initialize()
        {
            Run(true, TimeSpan.FromMilliseconds(_startdelay));
        }

        public static void Close()
        {
            if (IntPtr.Zero != GetConsoleWindow())
            {
                FreeConsole();
            }
        }

        public static void Main()
        {
            Run(false, TimeSpan.Zero);
        }

        public static bool FindAssembly(string name, out Assembly result)
        {
            try
            {
                var asmlist = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in asmlist)
                {
                    var nm = asm.GetName();
                    if (nm.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        result = asm;
                        return true;
                    }
                }
            }
            catch
            {
                // ignored
            }
            result = null;
            return false;
        }

        internal static ScriptEngine MainEngine { get; private set; }

        public static void Run(bool allocConsole, TimeSpan waitTime)
        {
            if (!_enable) return;

            System.Threading.Thread.Sleep(waitTime);

            if (allocConsole && IntPtr.Zero == GetConsoleWindow())
            {
                var hForeground = GetForegroundWindow();
                var hActiveHwnd = GetActiveWindow();
                var hFocusHwnd = GetFocus();

                AllocConsole();
                SetConsoleTitle("Unity Console");
                SetConsoleCP(65001);
                SetConsoleOutputCP(65001);

                if (hForeground != IntPtr.Zero)
                    SetForegroundWindow(hForeground);
                if (hActiveHwnd != IntPtr.Zero)
                    SetActiveWindow(hActiveHwnd);
                if (hFocusHwnd != IntPtr.Zero)
                    SetFocus(hFocusHwnd);

            }

            if (Environment.GetEnvironmentVariable("TERM") == null)
                Environment.SetEnvironmentVariable("TERM", "dumb");

            var inStream = new InternalStream(StandardHandles.STD_INPUT);
            var outStream = new InternalStream(StandardHandles.STD_OUTPUT);
            var errStream = new InternalStream(StandardHandles.STD_ERROR);

            var oldInStream = System.Console.In;
            var oldOutStream = System.Console.Out;
            var oldErrorStream = System.Console.Error;

            System.Console.SetIn(new StreamReader(inStream));
            System.Console.SetOut(new StreamWriter(outStream) {AutoFlush = true});
            System.Console.SetError(new StreamWriter(errStream) {AutoFlush = true});

            var stdwriter = new StreamWriter(new InternalStream(StandardHandles.STD_OUTPUT)) {AutoFlush = true};
            //cmdline = new UnityCommandLine();

            //cmdline.RegisterAssembly(exeAsm);

            string[] lines;
            if (GetPrivateProfileSection("Images", _iniPath, out lines))
            {
                foreach (var line in lines)
                {
                    var scname = line.Trim();
                    if (string.IsNullOrEmpty(scname) || scname.StartsWith(";") || scname.StartsWith("#"))
                        continue;

                    var scpath = Path.IsPathRooted(scname) ? scname : Path.Combine(_rootPath, scname);
                    if (File.Exists(scpath))
                    {
                        try
                        {
                            var asm = System.AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(scpath));
                            //cmdline.RegisterAssembly(asm);
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine("ERROR: " + ex.Message);
                        }
                    }
                }
            }

            var sb = new StringBuilder(4096);

            try
            {
                var runtimeoptions = new Dictionary<string, object>
                {
                    ["PrivateBinding"] = true, ["Debug"] = false,
                    //["Frames"] = false, ["Tracing"] = false,
                };
                var _env = Python.CreateRuntime(runtimeoptions);
                var _pe = _env.GetEngine("py");
                MainEngine = _pe;
                //_pe.SetTrace(OnTraceback);

                var scriptfolders = new List<string>();
                sb.Length = 0;
                sb.Capacity = 4096;
                if (0 < GetPrivateProfileString("Console", "ScriptsFolders", ".", sb, sb.Capacity, _iniPath))
                {
                    foreach (var scname in sb.ToString().Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    {
                        var scpath = Path.GetFullPath(Path.IsPathRooted(scname) ? scname : Path.Combine(_rootPath, scname));
                        scriptfolders.Add(scpath);
                    }
                }
                if (scriptfolders.Count == 0)
                    scriptfolders.Add(Path.GetFullPath(_rootPath));
                _pe.SetSearchPaths(scriptfolders.ToArray());

                if (GetPrivateProfileSection("Preload.Assemblies", _iniPath, out lines))
                {
                    foreach (var line in lines)
                    {
                        var asmname = line.Trim();
                        if (string.IsNullOrEmpty(asmname) || asmname.StartsWith(";") || asmname.StartsWith("#"))
                            continue;
                        Assembly.Load(new AssemblyName(Path.GetFullPath(Path.Combine(_rootPath, asmname))));
                    }
                }
                if (GetPrivateProfileSection("Script.Assemblies", _iniPath, out lines))
                {
                    foreach (var line in lines)
                    {
                        var asmname = line.Trim();
                        if (string.IsNullOrEmpty(asmname) || asmname.StartsWith(";") || asmname.StartsWith("#"))
                            continue;

                        Assembly asm;
                        if (!FindAssembly(asmname, out asm))
                            stdwriter.WriteLine("Error adding assembly: " + asmname);
                        else
                            _env.LoadAssembly(asm);
                    }
                }

                var cmdline = new PythonCommandLine();
                var console = new UnityConsole(cmdline);
                var options = new PythonConsoleOptions
                {
                    PrintUsage = false,
                    PrintVersion = false,
                    ColorfulConsole = true,
                    IsMta = false,
                    Introspection = false,
                    TabCompletion = true,
                    AutoIndentSize = 2,
                    AutoIndent = true,
                    HandleExceptions = true,
                    IgnoreEnvironmentVariables = true,
                };
                if (GetPrivateProfileSection("Startup.Script.Py", _iniPath, out lines) && lines != null && lines.Length > 0)
                {
                    var scope = _env.CreateScope();
                    var str = string.Join("\n", lines);
                    var source = _pe.CreateScriptSourceFromString(str, SourceCodeKind.File);
                    source.Compile().Execute(scope);
                }
                cmdline.Run(_pe, console, options);

            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Exception: " + ex.ToString());
                System.Threading.Thread.Sleep(10000);
            }
            MainEngine = null;

            System.Console.SetIn(oldInStream);
            System.Console.SetOut(oldOutStream);
            System.Console.SetError(oldErrorStream);

            if (allocConsole)
                Close();

        }

        public static void Shutdown()
        {
            //cmdline?.Stop();
            Close();
            _enable = false;
        }

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

        [DllImport("kernel32.dll")]
        static extern int GetPrivateProfileInt(string lpAppName, string lpKeyName,int nDefault, string lpFileName);

        [DllImport("kernel32.dll")]
        static extern uint GetPrivateProfileSection(string lpAppName, StringBuilder lpReturnedString, int nSize, string lpFileName);

        [DllImport("kernel32.dll")]
        static extern int GetPrivateProfileSection(string lpAppName, byte[] lpReturnedString, int nSize, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileString(
            string lpAppName,
            string lpKeyName,
            string lpDefault,
            StringBuilder lpReturnedString,
            int nSize,
            string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [PreserveSig]
        private static extern uint GetModuleFileName([In] IntPtr hModule, [Out] StringBuilder lpFilename,
            [In] [MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleTitle(string lpConsoleTitle);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleOutputCP(uint wCodePageID);
    }
}