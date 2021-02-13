using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using IronPython.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Ast;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;
using UC;
using UnityEngine;
using UnityEngine.Events;

//using Unity.Console.Commands;

namespace Unity.Console
{
	public static class Program
	{
		private static UnityConsole console;
		private static InternalStream inStream, outStream, errStream;

		static string _rootPath;
		static string _iniPath;
		static string _libPath;
		static int _startdelay = 0;
		static bool _enable;

		private static CompiledCode sceneInit;
		private static ScriptScope scriptScope;

		internal static ScriptEngine MainEngine { get; private set; }
		internal static ScriptRuntime MainRuntime { get; private set; }

		public static void Initialize(string root, string lib, string ini, bool startHidden)
		{
			_rootPath = root;
			_libPath = lib;
			_iniPath = ini;
			_enable = true;

			Run(!startHidden, false, TimeSpan.FromMilliseconds(_startdelay));
		}

		public static void Close()
		{
			console?.Abort();
			if (IntPtr.Zero != GetConsoleWindow())
			{
				FreeConsole();
			}
		}

		public static void Main()
		{
			Run(false, false, TimeSpan.Zero);
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

		public class UnityPythonCommandLine : PythonCommandLine
		{
			protected override int RunInteractive()
			{
				if (base.Scope == null)
				{
					base.Scope = this.CreateScope();
				}
				Microsoft.Scripting.Hosting.ScriptScope scope = base.ScriptScope;
				scope.SetVariable("_console", console);
				return base.RunInteractive();
			}
		}


		public static void Run(bool allocConsole, bool focusWindow, TimeSpan waitTime)
		{
			if (!_enable) return;

			System.Threading.Thread.Sleep(waitTime);

			System.Console.WriteLine("Allocate Console");
			AllocateConsole(allocConsole: allocConsole, focusWindow: focusWindow);
			System.Console.WriteLine("Console Allocated");
			RunConsole(allocConsole: allocConsole);
			System.Console.WriteLine("Console Run Complete");
			if (allocConsole) Close();
			_enable = false; // TODO: something goes wrong with console after shutdown and restart
		}

		private static void RunConsole(bool allocConsole)
		{
			var oldInStream = System.Console.In;
			var oldOutStream = System.Console.Out;
			var oldErrorStream = System.Console.Error;
			try
			{
				if (Environment.GetEnvironmentVariable("TERM") == null)
					Environment.SetEnvironmentVariable("TERM", "dumb");

				if (allocConsole)
				{
					if (inStream == null)
						inStream = new InternalStream(StandardHandles.STD_INPUT);
					outStream = new InternalStream(StandardHandles.STD_OUTPUT);
					errStream = new InternalStream(StandardHandles.STD_ERROR);

					oldInStream = System.Console.In;
					oldOutStream = System.Console.Out;
					oldErrorStream = System.Console.Error;

					System.Console.SetIn(new StreamReader(inStream));
					System.Console.SetOut(new StreamWriter(outStream) { AutoFlush = true });
					System.Console.SetError(new StreamWriter(errStream) { AutoFlush = true });
				}

				var stdwriter = new StreamWriter(new InternalStream(StandardHandles.STD_OUTPUT)) { AutoFlush = true };


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

					var scriptfolders = new List<string>();
					var sb = new StringBuilder(4096);
					sb.Length = 0;
					sb.Capacity = 4096;
					if (0 < GetPrivateProfileString("Console", "ScriptsFolders", ".", sb, sb.Capacity, _iniPath))
					{
						foreach (var scname in sb.ToString()
							.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
						{
							var scpath = Path.GetFullPath(Path.IsPathRooted(scname)
								? scname
								: Path.Combine(_rootPath, scname));
							scriptfolders.Add(scpath);
						}
					}

					if (scriptfolders.Count == 0)
						scriptfolders.Add(Path.GetFullPath(_rootPath));
					MainEngine.SetSearchPaths(scriptfolders.ToArray());

					string[] lines;
					if (GetPrivateProfileSection("Preload.Assemblies", _iniPath, out lines))
					{
						foreach (var line in lines)
						{
							var asmname = line.Trim();
							if (string.IsNullOrEmpty(asmname) || asmname.StartsWith(";") || asmname.StartsWith("#"))
								continue;
							Assembly.LoadFile(Path.GetFullPath(Path.Combine(_libPath, asmname)));
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
								MainRuntime.LoadAssembly(asm);
						}
					}

					if (GetPrivateProfileSection("SceneChange.Script.Py", _iniPath, out lines) && lines != null && lines.Length > 0)
					{
						var trimmedlines = lines; //.Where(x => !x.StartsWith("#") && !x.StartsWith(";")).ToArray();
						if (trimmedlines.Length > 0)
						{
							var str = string.Join("\n", trimmedlines);
							var source = MainEngine.CreateScriptSourceFromString(str, SourceCodeKind.File);
							sceneInit = source.Compile();
							var scope = new Scope(new Dictionary<string, object> {
								{"init", true}
								, {"level", -1}
							});
							scriptScope = MainEngine.CreateScope(scope);
						}
					}

					if (GetPrivateProfileSection("Startup.Script.Py", _iniPath, out lines) && lines != null &&
						lines.Length > 0)
					{
						var trimmedlines = lines; //.Where(x => !x.StartsWith("#") && !x.StartsWith(";")).ToArray();
						if (trimmedlines.Length > 0)
						{
							var str = string.Join("\n", trimmedlines);
							var source = MainEngine.CreateScriptSourceFromString(str, SourceCodeKind.File);
							source.Compile().Execute();
						}
					}
				}
#if UNITY5
				if (sceneInit != null)
				{
					try
					{
						var version = new System.Version(Application.unityVersion.Replace("p", ".").Replace("f", "."));
						if (version.Major > 5 || (version.Major == 5 && version.Minor >= 4))
						{
							var scm = typeof(UnityEngine.SceneManagement.SceneManager);
							var mi = scm.GetMethod("add_activeSceneChanged");
							var action = new UnityAction<UnityEngine.SceneManagement.Scene, UnityEngine.SceneManagement.Scene>(SceneChangeNew);
							mi.Invoke(null, new object[] { action });
						}
					}

					catch (Exception e)
					{
						System.Console.WriteLine(e);
					}

				}
#endif


				if (allocConsole && MainEngine != null)
				{
					var cmdline = new UnityPythonCommandLine();
					console = new UnityConsole(cmdline);
					var options = new PythonConsoleOptions
					{
						PrintUsage = false,
						PrintVersion = false,
						ColorfulConsole = true,
						IsMta = false,
						Introspection = false,
						TabCompletion = true
						,
						AutoIndentSize = 2,
						AutoIndent = true,
						HandleExceptions = true,
						IgnoreEnvironmentVariables = true,
					};
					//cmdline.ScriptScope.SetVariable("_console", console);
					cmdline.Run(MainEngine, console, options);
				}
			}
			catch (Exception ex)
			{
				System.Console.WriteLine("Exception: " + ex.ToString());
				System.Threading.Thread.Sleep(GetPrivateProfileInt("Console", "ErrorWaitTime", 10000, _iniPath));
			}
			finally
			{
				console = null;
				System.Console.SetIn(oldInStream);
				System.Console.SetOut(oldOutStream);
				System.Console.SetError(oldErrorStream);
			}
		}

		private static void AllocateConsole(bool allocConsole, bool focusWindow)
		{
			try
			{
				var consoleWnd = GetConsoleWindow();
				if (allocConsole && IntPtr.Zero == consoleWnd)
				{
					var hForeground = GetForegroundWindow();
					var hActiveHwnd = GetActiveWindow();
					var hFocusHwnd = GetFocus();
					if (hActiveHwnd == IntPtr.Zero) hActiveHwnd = hForeground;
					if (hFocusHwnd == IntPtr.Zero) hFocusHwnd = hForeground;

					bool restoreWindow = false;

					AllocConsole();
					SetConsoleTitle("Unity Console 1");
					SetConsoleCP(65001);
					SetConsoleOutputCP(65001);

					int monitorIdx = GetPrivateProfileInt("Console", "MoveToMonitor", -1, _iniPath);
					int monitorWidth = GetPrivateProfileInt("Console", "MonitorWidth", 0, _iniPath);
					int monitorHeight = GetPrivateProfileInt("Console", "MonitorHeight", 0, _iniPath);

					if (monitorIdx > 0)
					{
						consoleWnd = GetConsoleWindow();
						var monitors = GetDisplays();
						if (monitorIdx >= 0 && monitorIdx < monitors.Count)
						{
							var monitor = monitors[monitorIdx];
							RectStruct crect;
							GetWindowRect(consoleWnd, out crect);
							restoreWindow = true;
							focusWindow = true;
							if (monitorWidth < 0) monitorWidth += (monitor.WorkArea.right - monitor.WorkArea.left);
							if (monitorWidth < 0) monitorHeight += (monitor.WorkArea.bottom - monitor.WorkArea.top);
							var width = Math.Min(Math.Max(monitorWidth, crect.right - crect.left), monitor.WorkArea.right - monitor.WorkArea.left);
							var height = Math.Min(Math.Max(monitorHeight, crect.bottom - crect.top), monitor.WorkArea.bottom - monitor.WorkArea.top);
							MoveWindow(consoleWnd, monitor.WorkArea.left, monitor.WorkArea.top, width, height, true);
							// has to be called twice for some reason to work on windows 10
							MoveWindow(consoleWnd, monitor.WorkArea.left, monitor.WorkArea.top, width, height, true); 
						}
					}

					if (restoreWindow)
					{
						ShowWindowAsync(hForeground, SW_RESTORE);
						ShowWindowAsync(hForeground, SW_SHOW);
					}

					// reset focus
					if (focusWindow)
					{
						if (hForeground != IntPtr.Zero) SetForegroundWindow(hForeground);
						if (hActiveHwnd != IntPtr.Zero) SetActiveWindow(hActiveHwnd);
						if (hFocusHwnd != IntPtr.Zero) SetFocus(hFocusHwnd);
					}
				}
				else if (IntPtr.Zero != consoleWnd)
				{
					SetConsoleTitle("Unity Console 2");
				}
			}
			catch
			{
			}
		}

		public static void Shutdown()
		{
			Close();
			MainEngine = null;
			MainRuntime?.Shutdown();
			MainRuntime = null;
			_enable = false;
		}

		public static void SceneChange(int level, bool init)
		{
			//System.Console.WriteLine("SceneChange: {0} {1}", level, init);
			if (_enable)
			{
				scriptScope?.SetVariable("level", level);
				scriptScope?.SetVariable("init", init);
				sceneInit?.Execute(scriptScope);
			}
		}
#if UNITY5
		public static void SceneChangeNew(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
		{
			if (_enable)
			{
				scriptScope.SetVariable("level", to.buildIndex);
				scriptScope.SetVariable("init", true);
				sceneInit?.Execute(scriptScope);
			}
		}
#endif

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

		private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData);

		[StructLayout(LayoutKind.Sequential)]
		public struct RectStruct
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		private const int CCHDEVICENAME = 32;
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		internal struct MonitorInfoEx
		{
			public int Size;
			public RectStruct Monitor;
			public RectStruct WorkArea;
			public uint Flags;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
			public string DeviceName;

			public void Init()
			{
				Size = 40 + 2 * CCHDEVICENAME;
				DeviceName = string.Empty;
			}
		}

		public class DisplayInfo
		{
			public string Availability { get; set; }
			public string ScreenHeight { get; set; }
			public string ScreenWidth { get; set; }
			public RectStruct MonitorArea { get; set; }
			public RectStruct WorkArea { get; set; }
		}

		public class DisplayInfoCollection : List<DisplayInfo> { }

		public static DisplayInfoCollection GetDisplays()
		{
			DisplayInfoCollection col = new DisplayInfoCollection();

			EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
				delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData)
				{
					var mi = new MonitorInfoEx();
					mi.Size = Marshal.SizeOf(mi);
					var success = GetMonitorInfo(hMonitor, ref mi);
					if (success)
					{
						var di = new DisplayInfo
						{
							ScreenWidth = (mi.Monitor.right - mi.Monitor.left).ToString()
							, ScreenHeight = (mi.Monitor.bottom - mi.Monitor.top).ToString()
							, MonitorArea = mi.Monitor
							, WorkArea = mi.WorkArea
							, Availability = mi.Flags.ToString()
						};
						col.Add(di);
					}
					return true;
				}, IntPtr.Zero);
			return col;
		}

		[DllImport("user32.dll")]
		static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

		[DllImport("user32.dll", SetLastError = true)]
		static extern bool GetWindowRect(IntPtr hwnd, out RectStruct lpRect);

		[DllImport("user32.dll", SetLastError = true)]
		internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

		[DllImport("kernel32.dll")]
		static extern int GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);

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

		[DllImport("user32.dll")]
		private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

		private const int SW_RESTORE = 9;
		private const int SW_SHOW = 5;

	}
}