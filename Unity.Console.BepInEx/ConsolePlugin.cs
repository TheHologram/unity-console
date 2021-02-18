using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WindowsInput;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement; //using KeyCode = WindowsInput.KeyCode;
//using Input = UnityEngine.Input;
using Object = UnityEngine.Object;
// ReSharper disable StringLiteralTypo

namespace Unity.Console.BepInEx
{
	[BepInPlugin("a43c1bf8-9f95-4077-b8c1-fa9ec3d94dcf", "UnityConsole", "0.1.0.0")]
    // ReSharper disable once UnusedMember.Global
    internal class ConsolePlugin : BaseUnityPlugin
	{
        private static Assembly consoleAssembly;
        private static Thread runThread;
        private static readonly string[] Filters = new string[0];
        private static readonly string ConsolePath;
        private static readonly string IniPath;
        private static bool enable;
        private static bool debugEnable;

		private static readonly UnityEngine.KeyCode ShowKey = UnityEngine.KeyCode.BackQuote;
        private static readonly bool ShowKeyControl = true;
        private static readonly bool ShowKeyAlt;
        private static readonly bool ShowKeyShift;
        private static bool showAtStartup;
        private static int startdelay;
        private static bool initialized = false;
        private static DateTime lastKeyProcessed = DateTime.UtcNow;

        static ConsolePlugin()
        {
            DebugLog("Console Plugin Static Constructor");

            if (!FindModFolder("Console.ini", out var pluginfolder, out var inifile))
            {
                DebugLog("Unable to locate plugin folder with console.ini");
			}
			else
            {
                ConsolePath = pluginfolder;
                IniPath = inifile;
                if (File.Exists(IniPath))
                {
                    enable = GetPrivateProfileInt("System", "Enable", 0, IniPath) != 0;
                    debugEnable = GetPrivateProfileInt("System", "Debug", 0, IniPath) != 0;
					//if (_enable)
					{
                        var sb = new StringBuilder(4096);
                        if (0 < GetPrivateProfileString("Console", "Filter", "", sb, sb.Capacity, IniPath))
                        {
                            Filters = sb.ToString().Split(",;".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        }

                        sb.Length = 0;
                        try
                        {
                            if (0 < GetPrivateProfileString("Console", "ShowKey", "BackQuote", sb, sb.Capacity,
                                IniPath))
                            {
                                ShowKey = (UnityEngine.KeyCode)Enum.Parse(typeof(WindowsInput.KeyCode), sb.ToString());
                            }
                        }
                        catch
                        {
							DebugLog("Unable to parse KeyCode");
							// ignore
                        }
                        ShowKeyControl = GetPrivateProfileInt("Console", "ShowKeyControl", 1, IniPath) != 0;
                        ShowKeyAlt = GetPrivateProfileInt("Console", "ShowKeyAlt", 0, IniPath) != 0;
                        ShowKeyShift = GetPrivateProfileInt("Console", "ShowKeyShift", 0, IniPath) != 0;
                        DebugLog($"ShowKey {ShowKey} {ShowKeyControl} {ShowKeyAlt} {ShowKeyShift}");
                    }
				}
            }
        }

		internal static void DebugLog(string msg)
        {
            if (!debugEnable) return;
            System.Console.WriteLine("Console: " + msg);
        }
        internal static void DebugError(string msg)
        {
            if (!debugEnable) return;
            System.Console.WriteLine("Console: " + msg);
        }
        internal static void DebugError(Exception ex)
        {
            if (!debugEnable) return;
            System.Console.WriteLine("Console: " + ex.Message);
			System.Console.WriteLine(ex.StackTrace);
        }

        // ReSharper disable once UnusedMember.Local
        private void Awake()
        {
            if (!enable) return;
			System.Console.WriteLine("Console Plugin Awake");
            OnApplicationStart();
        }


		// ReSharper disable once UnusedMember.Local
		private void OnApplicationStart()
        {
            if (!enable) return;
			Object.DontDestroyOnLoad(this);

			Initialize();

            try
            {
                DebugLog($"--> Console Plugin Application Start: {enable}");

                AppDomain.CurrentDomain.ProcessExit += delegate { Shutdown(); };
                AppDomain.CurrentDomain.DomainUnload += delegate { Shutdown(); };

                if (showAtStartup)
                {
                    ShowConsole();
                }
                else 
                {
                    InitEngine();
                    OnSceneChange(-1, true);
                }
            }
            catch (Exception ex)
            {
                DebugLog("OnApplicationStart Error");
                DebugLog(ex.ToString());
            }
            finally
            {
                DebugLog("<-- Console Plugin Application Start");
            }
        }

        private static Type AssemblyGetType(Assembly asm, string name, bool ignoreCase)
        {
            var types = asm?.GetTypes();
            return types?.FirstOrDefault(x => string.Compare(x.FullName, name, ignoreCase) == 0);
        }

        private static IEnumerable<string> EnumerateRootFolders()
        {
            var dllAsm = Assembly.GetExecutingAssembly();
            if (!string.IsNullOrEmpty(dllAsm.Location))
            {
                var dirName = Path.GetDirectoryName(dllAsm.Location);
                if (!string.IsNullOrEmpty(dirName))
                {
                    yield return Path.GetFullPath(dirName);
                    dirName = Path.GetFullPath(Path.Combine(dirName, ".."));
					yield return dirName;
                    yield return Path.GetFullPath(Path.Combine(dirName, "plugins"));
                }
            }
            var exeAsm = Assembly.GetEntryAssembly();
            if (exeAsm != null && !string.IsNullOrEmpty(exeAsm.Location))
            {
                var dirName = Path.GetDirectoryName(exeAsm.Location);
                if (!string.IsNullOrEmpty(dirName))
                {
                    yield return Path.GetFullPath(dirName);
                    yield return Path.GetFullPath(Path.Combine(dirName, "BepInEx"));
                    yield return Path.GetFullPath(Path.Combine(dirName, "Mods"));
                    yield return Path.GetFullPath(Path.Combine(dirName, "Plugins"));
                }
            }
        }

		private static bool FindModFolder(string configName, out string pluginfolder, out string inifile)
        {
            try
            {
                foreach (var rootPath in EnumerateRootFolders())
                {
                    if (Directory.Exists(rootPath))
                    {
                        foreach (var subFolder in new[] { ".", "Console", "Unity.Console" })
                        {
                            var configFolder = Path.GetFullPath(Path.Combine(rootPath, subFolder));
                            if (Directory.Exists(configFolder))
                            {
                                var configfile = Path.GetFullPath(Path.Combine(configFolder, configName));
                                if (File.Exists(configfile))
                                {
                                    pluginfolder = Path.GetFullPath(configFolder);
                                    inifile = Path.GetFullPath(configfile);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
			catch (Exception e)
            {
                DebugError(e);
            }
            pluginfolder = null;
            inifile = null;
            return false;
        }

		/// <summary>
		/// Called during static constructor
		/// Timing is important to capture StdIn and StdOut before Unity overrides them to redirect to log files
		/// </summary>
		private void Initialize()
        {
            if (!enable) return;
			if (initialized) return;
            initialized = true;
			// load initial state
			if (!string.IsNullOrEmpty(IniPath) && File.Exists(IniPath))
			{
                if (GetPrivateProfileSection("Preload.Assemblies", IniPath, out var lines))
				{
					foreach (var line in lines)
					{
						var asmname = line.Trim();
						if (string.IsNullOrEmpty(asmname) || asmname.StartsWith(";") || asmname.StartsWith("#"))
							continue;

						AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(Path.GetFullPath(Path.Combine(ConsolePath, asmname))));
					}
				}

				DebugLog("--- Preload complete");
				startdelay = GetPrivateProfileInt("Console", "StartDelay", 0, IniPath);
				showAtStartup = GetPrivateProfileInt("Console", "ShowAtStartup", 0, IniPath) != 0;
			}


			// call initialize in the Unity.Console assembly
			try
			{
				DebugLog("--> Initialize");
				var dllPath = Path.GetFullPath(Path.Combine(ConsolePath, @"Unity.Console.dll"));
				if (!File.Exists(dllPath))
				{
					DebugLog("Unable to resolve to Unity.Console.dll location");
				}
				else
				{
					consoleAssembly = AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(dllPath));
					if (consoleAssembly == null)
					{
						DebugLog("Unable to resolve to Unity.Console.dll location");
					}


					var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", true);
					if (progType == null)
					{
						DebugLog("Unable to resolve to Unity.Console.Engine");
					}
					else
                    {							   
                        var variables = new Dictionary<string, object>() { {"logger", this.Logger} };

						var initMethod = progType.GetMethod("Initialize",
							BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, CallingConventions.Any
							, new[] { typeof(string), typeof(string), typeof(Action<string>), typeof(Dictionary<string, object>) }
							, new ParameterModifier[0]
						);
						if (initMethod != null)
						{
							DebugLog($"Initialize: {ConsolePath}, {IniPath}");
							initMethod.Invoke(null, new object[]
							{
								ConsolePath, IniPath, new Action<string>(DebugLog), variables
							});
						}
						else
						{
							DebugError("Unable to bind to initialize function");
						}
					}
				}
			}
			catch (Exception ex)
			{
				DebugLog("--- Initialize Exception");
				DebugError(ex);
			}
			finally
			{
				DebugLog("<-- Initialize");
			}
		}

		//static bool OnToggle(UnityModManagerNet.UnityModManager.ModEntry modEntry, bool value)
		//{
		//	if (value)
		//	{
		//		DebugLog("Toggle Enable");
		//		OnEnable();
		//	}
		//	else
		//	{
		//		DebugLog("Toggle Disable");
		//		Close();
		//	}

		//	_enable = value;
		//	return true;
		//}
		
        // ReSharper disable once UnusedMember.Global
        public void OnDisable()
		{
			DebugLog("Console Plugin Disable");
			enable = false;

			try
			{
				var version = new Version(Application.unityVersion.Replace("p", "."));
				if (version.Major > 5 || (version.Major == 5 && version.Minor >= 4))
				{
					DisableSceneLoading();
				}
            }
			catch
			{
				// ignore
			}
            Close();
		}

		/// <summary>
		/// Show Console
		/// </summary>
		private void ShowConsole()
		{
            if (!enable) return;
			DebugLog($"--> Console Plugin Show Console: {startdelay}");
            Initialize();

			if (runThread != null) return;
			runThread = new Thread(RunThread) { IsBackground = true };
			runThread.Start();
		}


		private void InitEngine()
		{
            if (!enable) return;
			DebugLog("Console Plugin Init Engine");
			try
            {
                Initialize();

				if (consoleAssembly == null) return;
				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", true);
				if (progType != null)
				{
					var initMethod = progType.GetMethod("InitEngine",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
					initMethod?.Invoke(null, new object[0]);
				}
			}
			catch (Exception ex)
			{
				DebugLog("Exception: " + ex.Message);
				// ignored
			}

		}

        /// <summary>
        /// Close open console window 
        /// </summary>
        private static void Close()
		{
			try
			{
				if (consoleAssembly == null) return;
				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", true);
				if (progType != null)
				{
					var closeMethod = progType.GetMethod("Close",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null,
						CallingConventions.Any, new Type[0], new ParameterModifier[0]);
					if (closeMethod == null)
					{
						DebugLog("Cannot find Close");
					}
					else
					{
						DebugLog("Closing");
						closeMethod.Invoke(null, new object[0]);
						DebugLog("Closed");
					}
				}
			}
			catch (Exception ex)
			{
				DebugLog("Exception: " + ex.Message);
				// ignored
			}
			finally
			{
				DebugLog($"Console Plugin Close: {runThread == null}");
			}
		}

		/// <summary>
		/// Shut everything down related to the console
		/// </summary>
		internal void Shutdown()
		{
			try
			{
				DebugLog($"--> Console Plugin Shutdown: {runThread == null}");

				if (runThread == null)
					return;
				if (consoleAssembly == null) return;
				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", true);
				if (progType != null)
				{
					var initMethod = progType.GetMethod("Shutdown",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod);
					initMethod?.Invoke(null, new object[0]);
				}

				if (runThread != null)
				{
					runThread.Join(500);
					runThread.Abort();
					runThread = null;
				}
				//runThread?.Join(1000);
			}
			catch (Exception ex)
			{
				DebugLog("Exception: " + ex.Message);
				// ignored
			}
			finally
			{
				DebugLog("<-- Console Plugin Shutdown");
			}

		}

		private void Toggle()
		{
            if (!enable) return;

			if ((DateTime.UtcNow - lastKeyProcessed).TotalSeconds < 2) return;

			lastKeyProcessed = DateTime.UtcNow;


			
			if (runThread == null)
			{
                DebugLog("Toggle Console: Enable");
				ShowConsole();
				OnEnable();
			}
			else
			{
                DebugLog("Toggle Console: Disable");
				Close();
			}
		}

        private void OnEnable()
		{
   //         if (!enable) return;
			//DebugLog($"Console Plugin Enable {Application.unityVersion}, {Application.version}");
			//try
			//{
			//	var version = new Version(Application.unityVersion.Replace("p", "."));
			//	if (version.Major > 5 || (version.Major == 5 && version.Minor >= 4))
			//		EnableSceneLoading();
			//}
			//catch
			//{
			//	// ignore
			//}

			//try
			//{
			//	ShowConsole();
			//	OnSceneChange(0, true);
			//}
			//catch (Exception a)
			//{
			//	DebugError("Exception during SceneChange:" + a);
			//}
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
			DebugLog($"Console Plugin Level Loaded {scene.name}, {mode}");

			int.TryParse(scene.name.Replace("level", ""), out var level);
			OnSceneChange(level, false);
		}

		private static void OnSceneChange(int level, bool init)
		{
            if (!enable) return;
			try
			{
				DebugLog($"--> Console Plugin Run Scene Change: {level}, {init}");
				if (consoleAssembly == null) return;
				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", true);
				if (progType != null)
				{
					var sceneChange = progType.GetMethod("SceneChange",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null,
						CallingConventions.Any, new[] { typeof(int), typeof(bool) }, new ParameterModifier[0]
					);
					sceneChange?.Invoke(null, new object[] { level, init });
				}
			}
			catch (Exception ex)
			{
				DebugLog("Exception: " + ex.Message);
				DebugLog(ex.ToString());
			}
			finally
			{
				DebugLog("<-- Console Plugin Run Scene Change");
			}

		}
		

        // ReSharper disable once UnusedMember.Global
        internal void Update()
		{
			if (!enable) return;

            
			if (Input.GetKey(ShowKey))
			{
				bool controlDown = GetAsyncKeyState(0xA2) != 0 || GetAsyncKeyState(0xA3) != 0;
				bool altDown = GetAsyncKeyState(0xA4) != 0 || GetAsyncKeyState(0xA5) != 0;
				bool shiftDown = GetAsyncKeyState(0xA0) != 0 || GetAsyncKeyState(0xA1) != 0;
                DebugLog($"OnUpdate {ShowKey} | {controlDown} {altDown} {shiftDown} | {ShowKeyControl ^ controlDown} {ShowKeyAlt ^ altDown} {ShowKeyShift ^ shiftDown}");
                DebugLog("OnUpdate" + controlDown + " " + altDown + " " + shiftDown);
                if (   !(ShowKeyControl ^ controlDown)
					&& !(ShowKeyAlt ^ altDown)
					&& !(ShowKeyShift ^ shiftDown)
				)
				{
					Toggle();
				}
			}
		}

		private static void RunThread()
		{
			try
			{
				DebugLog("-> Console Plugin Run Thread");
				if (consoleAssembly == null) return;

                if (showAtStartup && startdelay > 0)
                {
                    Thread.Sleep(startdelay);
                    startdelay = 0;
                }

                var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", true);
				if (progType != null)
				{
					var initMethod = progType.GetMethod("Run",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null,
						CallingConventions.Any, new Type[0], new ParameterModifier[0]
					);
					if (initMethod != null)
					{
						DebugLog("Run Start");
						initMethod.Invoke(null, new object[0]);
					}
					else
					{
						DebugError("Unable to bind to run function");
					}
				}
				else
				{
					DebugError("Unable to resolve to Unity.Console.Engine");
				}
			}
			catch (Exception ex)
			{
				DebugLog("!-- Console Plugin Run Thread Exception");
				DebugLog(ex.ToString());
			}
			finally
			{
				runThread = null;
				DebugLog("<-- Console Plugin Run Thread");
				Close();
			}

		}

		#region Extern Imports

		[DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileInt(string lpAppName, string lpKeyName, int nDefault, string lpFileName);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern uint GetPrivateProfileString(
			string lpAppName,
			string lpKeyName,
			string lpDefault,
			StringBuilder lpReturnedString,
			int nSize,
			string lpFileName);

		[DllImport("kernel32.dll")]
        private static extern int GetPrivateProfileSection(string lpAppName, byte[] lpReturnedString, int nSize, string lpFileName);

		public static bool GetPrivateProfileSection(string appName, string fileName, out string[] section)
		{
			section = null;

			if (!File.Exists(fileName))
				return false;

			int maxBuffer = 32767;
			var bytes = new byte[maxBuffer];
			int nbytes = GetPrivateProfileSection(appName, bytes, maxBuffer, fileName);
			if ((nbytes == maxBuffer - 2) || (nbytes == 0))
				return false;
			section = Encoding.ASCII.GetString(bytes, 0, nbytes).Trim('\0').Split('\0');
			return true;
		}

		[DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
		#endregion
	}

}
