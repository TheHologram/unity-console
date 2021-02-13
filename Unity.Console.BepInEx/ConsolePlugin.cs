using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using BepInEx;
using WindowsInput;
using UnityEngine;
using UnityEngine.SceneManagement;
using KeyCode = WindowsInput.KeyCode;
using Input = UnityEngine.Input;

namespace Unity.Console.Plugin
{
	[BepInPlugin("a43c1bf8-9f95-4077-b8c1-fa9ec3d94dcf", "UnityConsole", "0.1.0.0")]
	internal class ConsolePlugin : BepInEx.BaseUnityPlugin
	{
#if true

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
			System.Console.WriteLine("Console Plugin Static Constructor");

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
						// default to this exe assembly name
						if (_filters.Length == 0)
						{
							_filters = new[] { exeAsm.GetName().Name };
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
						ShowKeyControl = GetPrivateProfileInt("Console", "ShowKeyControl", 1, _iniPath) != 0;
						ShowKeyAlt = GetPrivateProfileInt("Console", "ShowKeyAlt", 0, _iniPath) != 0;
						ShowKeyShift = GetPrivateProfileInt("Console", "ShowKeyShift", 0, _iniPath) != 0;
					}
				}
			}
		}


		public ConsolePlugin() // base()
		{
			System.Console.WriteLine("Console Plugin Constructor");
			//var asm = System.Reflection.Assembly.GetExecutingAssembly();
			//this.Version = asm.GetName().Version.ToString();
		}

		//public static bool Initialized { get; set; }

		//public string[] Filter => _filters;

		public void OnDisable()
		{
			System.Console.WriteLine("Console Plugin Disable");
			_enable = false;

			try {
				var version = new System.Version(Application.unityVersion.Replace("p", "."));
				if (version.Major > 5 || (version.Major == 5 && version.Minor >= 4))
				{
					DisableSceneLoading();
				}
			}
			catch{
				// ignore
			}

			Shutdown();
		}

		public void Awake()
		{
			System.Console.WriteLine("Console Plugin Awake");
			OnApplicationStart();
		}

		public void OnApplicationStart()
		{
			try
			{
				System.Console.WriteLine($"--> Console Plugin Application Start: {_enable}");

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

					System.Console.WriteLine("--- Preload complete");
					var showAtStartup = GetPrivateProfileInt("Console", "ShowAtStartup", 0, _iniPath) != 0;
					if (showAtStartup)
					{
						Startup(false, false);
					}
					else
					{
						var startHidden = GetPrivateProfileInt("Console", "StartHidden", 0, _iniPath) != 0;
						if (startHidden)
							Startup(false, true);
					}
				}
			}
			catch (Exception ex)
			{
				System.Console.WriteLine("OnApplicationStart Error");
				System.Console.WriteLine(ex.ToString());
			}
			finally
			{
				System.Console.WriteLine("<-- Console Plugin Application Start");
			}
		}

		private void Startup(bool focus, bool hidden)
		{
			System.Console.WriteLine($"--> Console Plugin Startup {focus}, {hidden}");
			_focusWindow = focus;
			_startHidden = hidden;
			if (runThread != null)
				return;
			var dllPath = Path.GetFullPath(Path.Combine(_consolePath, @"Unity.Console.dll"));
			if (File.Exists(dllPath))
			{
				consoleAssembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(dllPath));
				runThread = new Thread(RunThread) { IsBackground = true };
				runThread.Start();
			}
		}

		private void Close()
		{
			System.Console.WriteLine($"Console Plugin Close: {runThread == null}");
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
			try
			{
				System.Console.WriteLine($"--> Console Plugin Shutdown: {runThread == null}");
				if (runThread == null)
					return;

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
				System.Console.WriteLine("<-- Console Plugin Shutdown");
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

		void OnEnable()
		{
			System.Console.WriteLine($"Console Plugin Enable {Application.unityVersion}, {Application.version}");

			try {
				var version = new System.Version(Application.unityVersion.Replace("p","."));
				if (version.Major > 5 || (version.Major == 5 && version.Minor >= 4))
					EnableSceneLoading();
			}
			catch {
				// ignore
			}
		}

		private void EnableSceneLoading()
		{
			SceneManager.sceneLoaded += OnLevelFinishedLoading;
		}
		// only supported in 5.6 and later
		private void DisableSceneLoading()
		{
			SceneManager.sceneLoaded -= OnLevelFinishedLoading;
		}

		private void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
		{
			System.Console.WriteLine($"Console Plugin Level Loaded {scene.name}, {mode}");

			int.TryParse(scene.name.Replace("level", ""), out var level);
			OnSceneChange(level, false);
		}

		public void OnLevelWasInitialized(int level)
		{
			OnSceneChange(level, true);
		}

		public void OnLevelWasLoaded()
		{
			OnSceneChange(0, false);
		}

		public void OnLevelWasLoaded(int level)
		{
			OnSceneChange(level, false);
		}

		public void OnLevelWasLoaded(string level)
		{
			OnSceneChange(0, false);
		}

		private bool boundSceneChange = false;
		private MethodInfo sceneChange = null;
		private void OnSceneChange(int level, bool init)
		{
			try
			{
				System.Console.WriteLine($"--> Console Plugin Run Scene Change: {level}, {init}");
				if (consoleAssembly == null) return;
				if (!boundSceneChange)
				{
					var progType = consoleAssembly.GetType("Unity.Console.Program", false, true);
					if (progType != null)
					{
						sceneChange = progType.GetMethod("SceneChange",
							BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null,
							CallingConventions.Any, new Type[] { typeof(int), typeof(bool) }, new ParameterModifier[0]
						);
						boundSceneChange = true;
					}
				}

				sceneChange.Invoke(null, new object[] { level, init });
			}
			catch (Exception ex)
			{
				System.Console.WriteLine("Exception: " + ex.Message);
				System.Console.WriteLine(ex.ToString());
			}
			finally
			{
				System.Console.WriteLine("<-- Console Plugin Run Scene Change");
			}

		}

		public void OnUpdate()
		{
			if (!_enable)
				return;

			if (WinInput.GetKey(ShowKey))
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
				System.Console.WriteLine("-> Console Plugin Run Thread");
				if (consoleAssembly == null) return;

				var progType = consoleAssembly.GetType("Unity.Console.Program", false, true);
				if (progType != null)
				{
					var initMethod = progType.GetMethod("Initialize",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
                    initMethod?.Invoke(null, new object[] {_rootPath, _consolePath, _iniPath, _startHidden} );
				}
			}
			catch (Exception ex)
			{
				System.Console.WriteLine("!-- Console Plugin Run Thread Exception");
				System.Console.WriteLine(ex.ToString());
			}
			finally
			{
				runThread = null;
				System.Console.WriteLine("<-- Console Plugin Run Thread");
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
#endif
		}

	}
