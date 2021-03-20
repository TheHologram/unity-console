using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using IronPython.Hosting;
using IronPython.Modules;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;
using UC;
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

//using Unity.Console.Commands;

namespace Unity.Console
{
	public static partial class Engine
	{
		private static UnityConsole console;
        private static TextReader conInStream, origInStream;
        private static TextWriter conOutStream, conErrorStream, origOutStream, origErrorStream;
		private static Stream inStream, outStream, errStream;
        private static Stream conInPtr, conOutPtr, conErrPtr;
        private static Encoding conInEncoding, conOutEncoding;

        internal static string _iniPath;
        internal static string _libPath;
        internal static Action<string> _logger;
        internal static Dictionary<string, object> _variables;


        private static CompiledCode sceneInit;
		private static ScriptScope scriptScope;
        private static UnityPythonCommandLine cmdline;
        private static ModManager mods;

        internal static ScriptEngine MainEngine { get; private set; }
		internal static ScriptRuntime MainRuntime { get; private set; }

        static Engine()
        {
            if (Environment.GetEnvironmentVariable("TERM") == null)
                Environment.SetEnvironmentVariable("TERM", "dumb");

			// already too late?
			_logger = System.Console.WriteLine;
            conInPtr = System.Console.OpenStandardInput();
            conOutPtr = System.Console.OpenStandardOutput();
            conErrPtr = System.Console.OpenStandardError();

            origInStream = System.Console.In;
            origOutStream = System.Console.Out;
            origErrorStream = System.Console.Error;

			//// this seem like nop but resets the console encoder back to 
			conInEncoding = System.Console.InputEncoding;
            conOutEncoding = System.Console.OutputEncoding;
        }

        public static void DebugLog(string message)
        {
			_logger?.Invoke(message);
        }

		
        // ReSharper disable once UnusedMember.Global
        public static void Initialize(string _, string lib, string ini)
        {
            Initialize(lib, ini, (Action<string>)null, null);
        }

		
        public static void Initialize(string lib, string ini, Action<string> logger, Dictionary<string, object> variables)
		{
			_libPath = lib;
			_iniPath = ini;
            _logger = logger ?? System.Console.WriteLine;
            _variables = variables;
            _logger?.Invoke($"Engine: LibPath='{_libPath}', IniFile='{_iniPath}'");
		}

        // ReSharper disable once UnusedMember.Global
		public static void Close()
        {
            _logger?.Invoke("--> Unity Console Engine Close");
			if (console != null)
            {
                _logger?.Invoke("--> Unity Console Engine Abort");
                console.Abort(); // Should cancel any pending console readlines
                console = null;
                _logger?.Invoke("<-- Unity Console Engine Abort");
			}
			
			if (IntPtr.Zero != Internal.GetConsoleWindow())
            {
                _logger?.Invoke("--- Unity Console Engine Free Console");
				//SetConsoleCtrlHandler(ConsoleHandler, false);
				ResetStreams();
                Internal.FreeConsole();
            }
            _logger?.Invoke("<-- Unity Console Engine Close");
		}

		// ReSharper disable once UnusedMember.Global
		public static void Shutdown()
        {
            Close();
            MainEngine = null;
            MainRuntime?.Shutdown();
            MainRuntime = null;
        }

        // ReSharper disable once UnusedMember.Global
        public static void SceneChange(int level, bool init)
        {
			_logger?.Invoke($"SceneChange: {level} {init}");
            {
                scriptScope?.SetVariable("level", level);
                scriptScope?.SetVariable("init", init);
                sceneInit?.Execute(scriptScope);
            }

            mods?.OnSceneChange(level, init);
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
            catch (Exception ex)
            {
                DebugLog("Find Assembly Error: " + ex.Message);
                DebugLog(ex.StackTrace);
            }
			result = null;
			return false;
		}

		public class UnityPythonCommandLine : PythonCommandLine
		{
			protected override int RunInteractive()
			{
				if (Scope == null)
					Scope = this.CreateScope();
				var scope = ScriptScope;
				scope.SetVariable("_console", console);
                scope.SetVariable("_mods", mods);
				return base.RunInteractive();
			}
        }

        // ReSharper disable once UnusedMember.Global
		public static void Run()
        {
            _logger("Allocate Console");
            AllocateConsole(focusWindow: false);
            _logger("Console Allocated");
            RunConsole();
            _logger("Console Run Complete");
            Close();
		}

//#if UNITY5
//        public static void SceneChangeNew(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
//        {
//            if (_enable)
//            {
//                scriptScope.SetVariable("level", to.buildIndex);
//                scriptScope.SetVariable("init", true);
//                sceneInit?.Execute(scriptScope);
//            }
//        }
//#endif

        public static void InitEngine()
        {
            _logger("Initializing Engine");
			if (MainRuntime == null)
			{
				var runtimeoptions = new Dictionary<string, object>
				{
					["PrivateBinding"] = true,
					["Debug"] = false,
					//["Frames"] = false, ["Tracing"] = false,
				};

				MainRuntime = Python.CreateRuntime(runtimeoptions);
				MainEngine = MainRuntime.GetEngine("py");

                var searchPlugins = Internal.GetPrivateProfileInt("Console", "SearchPluginsForMods", 1, _iniPath) != 0; 
                // create the mod manager
                var modFolderString = Internal.GetPrivateProfileString("Console", "ModFolder", @".\Mods", _iniPath);
                if (!string.IsNullOrEmpty(modFolderString))
                {
                    var modfolder = Path.GetFullPath(Path.Combine(_libPath, modFolderString));
                    if (Directory.Exists(modfolder))
                        mods = new ModManager(modfolder, _variables);
                    else if (searchPlugins)
                    {
                        var pluginPath = Path.GetFullPath(Path.Combine(_libPath, ".."));
                        var di = new DirectoryInfo(pluginPath);
                        if (string.Compare(di.Name, "plugins", StringComparison.CurrentCultureIgnoreCase) == 0)
                        {
                            mods = new ModManager(pluginPath, _variables);
                        }
                        else
                        {
                            DebugLog($"Mod Folder '{pluginPath}' does not exist");
                        }
                    }
                    else
                    {
                        DebugLog($"Mod Folder '{modfolder}' does not exist");
                    }
                }
                else
                {
                    DebugLog("Unable to locate Mod Folder ");
                }
                mods?.Parse();

                var scriptfolders = new List<string>();
                var sb = new StringBuilder(4096) {Length = 0, Capacity = 4096};
                if (0 < Internal.GetPrivateProfileString("Console", "ScriptsFolders", ".", sb, sb.Capacity, _iniPath))
				{
					foreach (var scname in sb.ToString()
						.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
					{
						var scpath = Path.GetFullPath(Path.IsPathRooted(scname)
							? scname
							: Path.Combine(_libPath, scname));
						scriptfolders.Add(scpath);
					}
				}

                // append mod folders
                if (mods != null)
                    scriptfolders.AddRange(mods.GetAdditionalPaths());

                if (scriptfolders.Count == 0)
					scriptfolders.Add(Path.GetFullPath(_libPath));
				MainEngine.SetSearchPaths(scriptfolders.ToArray());

                if (Internal.GetPrivateProfileSection("Preload.Assemblies", _iniPath, out var lines))
				{
					foreach (var line in lines)
					{
						var asmname = line.Trim();
						if (string.IsNullOrEmpty(asmname) || asmname.StartsWith(";") || asmname.StartsWith("#"))
							continue;
						Assembly.LoadFile(Path.GetFullPath(Path.Combine(_libPath, asmname)));
					}
				}

				if (Internal.GetPrivateProfileSection("Script.Assemblies", _iniPath, out lines))
				{
					foreach (var line in lines)
					{
						var asmname = line.Trim();
						if (string.IsNullOrEmpty(asmname) || asmname.StartsWith(";") || asmname.StartsWith("#"))
							continue;

                        if (!FindAssembly(asmname, out var asm))
							_logger("Error adding assembly: " + asmname);
						else
							MainRuntime.LoadAssembly(asm);
					}
				}

				if (Internal.GetPrivateProfileSection("SceneChange.Script.Py", _iniPath, out lines) && lines != null && lines.Length > 0)
				{
                    _logger.Invoke("SceneChange Script: \r\n" + lines.ToString());
					var trimmedlines = lines.Where(x => !x.StartsWith("#") && !x.StartsWith(";")).ToArray();
					if (trimmedlines.Length > 0)
					{
						var str = string.Join("\r\n", trimmedlines);
						_logger.Invoke("SceneChange: \r\n" + str);
						var source = MainEngine.CreateScriptSourceFromString(str, SourceCodeKind.File);
						sceneInit = source.Compile();
						var scope = new Scope(new Dictionary<string, object> {
								{"init", true}
								, {"level", -1}
								, {"log", _logger}
							});
						scriptScope = MainEngine.CreateScope(scope);
					}
				}

                if (Internal.GetPrivateProfileSection("Startup.Script.Py", _iniPath, out lines) && lines != null && lines.Length > 0)
				{
					var trimmedlines = lines.Where(x => !x.StartsWith("#") && !x.StartsWith(";")).ToArray();
					if (trimmedlines.Length > 0)
					{
						var str = string.Join("\r\n", trimmedlines);
						var source = MainEngine.CreateScriptSourceFromString(str, SourceCodeKind.File);
						source.Compile().Execute();
					}
				}

                // final step of initialize to is to load mods
                mods?.Load();
            }
//#if UNITY5
//			if (sceneInit != null)
//			{
//				try
//				{
//					var version = new Version(Application.unityVersion.Replace("p", ".").Replace("f", "."));
//					if (version.Major > 5 || (version.Major == 5 && version.Minor >= 4))
//					{
//						var scm = typeof(UnityEngine.SceneManagement.SceneManager);
//						var mi = scm.GetMethod("add_activeSceneChanged");
//						var action = new UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.Scene>(SceneChangeNew);
//						mi?.Invoke(null, new object[] { action });
//					}
//				}
//
//				catch (Exception e)
//				{
//					_logger(e.ToString());
//				}
//
//			}
//#endif
            _logger("Initialized Engine");

        }

		private static void RunConsole()
        {
			_logger.Invoke("--> Run Console");
            //System.Console.InputEncoding = Encoding.Default;
            //System.Console.OutputEncoding = Encoding.Default;

            try
			{
				if (Environment.GetEnvironmentVariable("TERM") == null)
					Environment.SetEnvironmentVariable("TERM", "dumb");

				//InitStreams();
                //if (_first)
                {
                    inStream = new InternalStream(StandardHandles.STD_INPUT);
                    outStream = new InternalStream(StandardHandles.STD_OUTPUT);
                    errStream = new InternalStream(StandardHandles.STD_ERROR);

                    conInStream = TextReader.Synchronized(new StreamReader(inStream));
                    conOutStream = TextWriter.Synchronized(new StreamWriter(outStream) {AutoFlush = true});
                    conErrorStream = TextWriter.Synchronized(new StreamWriter(errStream) {AutoFlush = true});

					GC.SuppressFinalize(conInStream);
					GC.SuppressFinalize(conOutStream);
					GC.SuppressFinalize(conErrorStream);

                }

				_logger?.Invoke($"Stream Handles:  {inStream}, {outStream}, {errStream}");

				InitStreams();
				InitEngine();

				if (MainEngine != null)
                {
                    MainRuntime.IO.SetInput(inStream, conInStream, conInEncoding);
					MainRuntime.IO.SetOutput(outStream, conOutStream);
					MainRuntime.IO.SetErrorOutput(errStream, conErrorStream);

                    var history = new UnityConsole.History();

                    try
                    {
                        var persistHistory = Internal.GetPrivateProfileInt("Console", "PersistHistory", 0, _iniPath) != 0;
                        var noDuplicates = Internal.GetPrivateProfileInt("Console", "NoDuplicates", 1, _iniPath) != 0;
                        
                        if (persistHistory)
                        {
                            var historyfile = Internal.GetPrivateProfileString("Console", "HistoryFile", "", _iniPath);
                            var fullHistory = Path.GetFullPath(Path.Combine(_libPath, historyfile));
                            if (File.Exists(fullHistory))
                            {
                                using (var reader = new StreamReader(fullHistory))
                                    history.Load(reader);
                            }
                            var autoflushhistory = Internal.GetPrivateProfileInt("Console", "AutoFlushHistory", 1, _iniPath) != 0;
                            history.AttachWriter(fullHistory, autoflushhistory);
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog("Exception: " + ex);
                    }

                    cmdline = new UnityPythonCommandLine();
					console = new UnityConsole(cmdline, history);
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
                        SkipImportSite = true,
                    };
					//cmdline.ScriptScope.SetVariable("_console", console);

                    _logger?.Invoke($"Console Run");
					cmdline.Run(MainEngine, console, options);
				}
			}
			catch (Exception ex)
			{
                _logger("Exception: " + ex);
				System.Threading.Thread.Sleep(Internal.GetPrivateProfileInt("Console", "ErrorWaitTime", 10000, _iniPath));
			}
			finally
            {
				console?.Abort();
				cmdline = null;
				console = null;
                ResetStreams();
                _logger.Invoke("<-- Run Console");
			}
        }

        private static void InitStreams()
        {
            try
            {
                System.Console.SetIn(conInStream);
                System.Console.SetOut(conOutStream);
                System.Console.SetError(conErrorStream);
            }
            catch (Exception ex)
            {
                DebugLog("Reset Streams Error: " + ex.Message);
            }
        }

        private static void InvalidateStreams()
        {
        }

		private static void ResetStreams()
        {
            try
            {
                System.Console.InputEncoding = conInEncoding;
                System.Console.OutputEncoding = conOutEncoding;
                System.Console.SetIn(origInStream);
                System.Console.SetOut(origOutStream);
                System.Console.SetError(origErrorStream);

				// empty the python runtime stream.  Will reset when console is reopened3
                MainRuntime.IO.SetInput(new MemoryStream(), Encoding.Default);
                MainRuntime.IO.SetOutput(new MemoryStream(), Encoding.Default);
                MainRuntime.IO.SetErrorOutput(new MemoryStream(), Encoding.Default);
			}
            catch (Exception ex)
            {
                DebugLog("Reset Streams Error: " + ex.Message);
            }
        }

        private static void AllocateConsole(bool focusWindow)
		{
			try
			{
				var consoleWnd = Internal.GetConsoleWindow();
                if (IntPtr.Zero != consoleWnd) return;

                var hForeground = Internal.GetForegroundWindow();
                var hActiveHwnd = Internal.GetActiveWindow();
                var hFocusHwnd = Internal.GetFocus();
                if (hActiveHwnd == IntPtr.Zero) hActiveHwnd = hForeground;
                if (hFocusHwnd == IntPtr.Zero) hFocusHwnd = hForeground;

                bool restoreWindow = false;

                Internal.AllocConsole();
                Internal.SetConsoleTitle("Unity Console");
                Internal.SetConsoleCP(65001);
                Internal.SetConsoleOutputCP(65001);

                RemoveConsoleCloseButton();
                //DeleteMenu(GetSystemMenu(GetConsoleWindow(), 0), 0xF060, 0);

                //SetConsoleCtrlHandler(ConsoleHandler, true);
                var widthPct = false;
                var heightPct = false;
                var monitorWidth = 0;
                var monitorHeight = 0;

				int monitorIdx = Internal.GetPrivateProfileInt("Console", "MoveToMonitor", -1, _iniPath);
                var sb = new StringBuilder(4096) { Length = 0, Capacity = 4096 };
                if (Internal.GetPrivateProfileString("Console", "MonitorWidth", "0", sb, sb.Capacity, _iniPath) > 0)
                {
                    var str = sb.ToString().Trim();
                    DebugLog($"MonitorWidth: {str}");

                    if (str.EndsWith("%"))
                    {
                        str.Remove(str.Length - 1);
                        if (int.TryParse(str, out var intval))
                        {
                            widthPct = true;
                            monitorWidth = intval;
                        }
					}
					else if(int.TryParse(str, out var intval))
                    {
                        widthPct = false;
                        monitorWidth = intval;
                    }
				}
                else
                {
                    monitorWidth = Internal.GetPrivateProfileInt("Console", "MonitorWidth", 0, _iniPath);
                }
                sb = new StringBuilder(4096) { Length = 0, Capacity = 4096 };
				if (Internal.GetPrivateProfileString("Console", "MonitorHeight", "0", sb, sb.Capacity, _iniPath) > 0)
                {
                    var str = sb.ToString().Trim();
                    DebugLog($"MonitorHeight: {str}");
                    if (str.EndsWith("%"))
                    {
                        str.Remove(str.Length - 1);
                        if (int.TryParse(str, out var intval))
                        {
                            heightPct = true;
                            monitorHeight = intval;
                        }
                    }
					else if (int.TryParse(str, out var intval))
                    {
                        heightPct = false;
                        monitorHeight = intval;
                    }
                }
                else
                {
                    monitorHeight = Internal.GetPrivateProfileInt("Console", "MonitorHeight", 0, _iniPath);
                }

                if (monitorIdx > 0)
                {
					DebugLog($"Move to Monitor: {monitorIdx}");
                    consoleWnd = Internal.GetConsoleWindow();
                    var monitors = Internal.GetDisplays();
                    if (monitorIdx <= monitors.Count)
                    {
                        var monitor = monitors[monitorIdx-1];
                        Internal.GetWindowRect(consoleWnd, out var crect);
                        restoreWindow = true;
                        focusWindow = true;
                        if (widthPct)
                            monitorWidth = monitorWidth * (monitor.WorkArea.right - monitor.WorkArea.left) / 100;
                        else if (monitorWidth < 0)
                            monitorWidth = (monitor.WorkArea.right - monitor.WorkArea.left);

                        if (heightPct)
                            monitorHeight = monitorHeight * (monitor.WorkArea.bottom - monitor.WorkArea.top) / 100;
                        else if (monitorHeight < 0)
							monitorHeight = (monitor.WorkArea.bottom - monitor.WorkArea.top); ;

                        DebugLog($"Rect: {monitorIdx}, {widthPct} {monitorWidth}, {heightPct} {monitorHeight}");

						var width = Math.Min(Math.Max(monitorWidth, crect.right - crect.left), monitor.WorkArea.right - monitor.WorkArea.left);
                        var height = Math.Min(Math.Max(monitorHeight, crect.bottom - crect.top), monitor.WorkArea.bottom - monitor.WorkArea.top);
                        Internal.MoveWindow(consoleWnd, monitor.WorkArea.left, monitor.WorkArea.top, width, height, true);
                        // has to be called twice for some reason to work on windows 10
                        Internal.MoveWindow(consoleWnd, monitor.WorkArea.left, monitor.WorkArea.top, width, height, true); 
                    }
                }

                if (restoreWindow)
                {
                    Internal.ShowWindowAsync(hForeground, Internal.SW_RESTORE);
                    Internal.ShowWindowAsync(hForeground, Internal.SW_SHOW);
                }

                // reset focus
                if (focusWindow)
                {
                    if (hForeground != IntPtr.Zero) Internal.SetForegroundWindow(hForeground);
                    if (hActiveHwnd != IntPtr.Zero) Internal.SetActiveWindow(hActiveHwnd);
                    if (hFocusHwnd != IntPtr.Zero) Internal.SetFocus(hFocusHwnd);
                }
            }
			catch (Exception ex)
			{
                DebugLog("Alloc Console Error: " + ex.Message);
                DebugLog(ex.StackTrace);
			}
		}

        public static void RemoveConsoleCloseButton()
        {
            IntPtr handle = Internal.GetConsoleWindow();
            IntPtr hMenu = Internal.GetSystemMenu(handle, false);

            Internal.DeleteMenu(hMenu, Internal.SC_CLOSE, Internal.MF_GRAYED);
            Internal.DrawMenuBar(handle);
        }
    }
}