using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WindowsInput;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;
using KeyCode = UnityEngine.KeyCode;
using Object = UnityEngine.Object;

//using Input = UnityEngine.Input;
// ReSharper disable StringLiteralTypo


namespace Unity.Console.UnityModManager
{
    internal class Manager : MonoBehaviour
    {

		/// <summary>
		///     This function is always called before any Start functions and also just after a prefab is instantiated.
		/// </summary>
		// ReSharper disable once UnusedMember.Local
		private void Awake()
        {
            Main.DebugLog("Unity Console Manager Awake");
        }


		/// <summary>
		///     Start is called before the first frame update only if the script instance is enabled
		/// </summary>
		// ReSharper disable once UnusedMember.Local
		private void Start()
		{
		}

		/// <summary>
		///     Update is called once per frame. It is the main workhorse function for frame updates
		/// </summary>
		// ReSharper disable once UnusedMember.Local
		private void Update()
		{
			Main.Update();
		}

		/// <summary>
		/// 	This function is called when the behaviour becomes disabled.
		/// </summary>
        // ReSharper disable once UnusedMember.Local
		private void OnDisable()
        {
            Main.DebugLog("Unity Console Manager: OnDisable");
			Main.OnDisable();

        }
		/// <summary>
		///     This function is called after all frame updates for the last frame of the object’s existence (the object might be destroyed in response to Object.Destroy or at the closure of a scene).
		/// </summary>
		// ReSharper disable once UnusedMember.Local
		private void OnDestroy()
		{
			Main.DebugLog("Unity Console Manager: Destroy");
            Main.OnDisable();
            Main.Shutdown();
		}

		/// <summary>
		/// Sent to all GameObjects before the application quits.
		/// </summary>
		void OnApplicationQuit()
        {
            Main.DebugLog("Unity Console Manager: Application ending after " + Time.time + " seconds");
            Main.OnDisable();
			Main.Shutdown();
		}
    }


	internal static class Main
	{
		private static Assembly consoleAssembly;
		private static Thread runThread;
		static readonly string[] _filters = new string[0];
		static readonly string _rootPath;
		static readonly string _consolePath;
		static readonly string _iniPath;
		static bool _enable;
        static Settings settings = new Settings();

		public static GameObject ManagerObject { get; private set; }
        public static Manager ManagerComponent { get; private set; }

		internal static UnityModManagerNet.UnityModManager.ModEntry.ModLogger logger;
		//internal static Harmony12.HarmonyInstance harmony;

        static KeyBinding ShowHotkey = new KeyBinding { keyCode = KeyCode.BackQuote };

		static readonly WindowsInput.KeyCode ShowKey = WindowsInput.KeyCode.BackQuote;
		static readonly bool ShowKeyControl = true;
		static readonly bool ShowKeyAlt;
		static readonly bool ShowKeyShift;
		static bool _startHidden;
        static bool _showAtStartup;
        static int _startdelay;
        static DateTime lastKeyProcessed = DateTime.UtcNow;

        //[DrawFields(DrawFieldMask.Public)]
        //public class ModelsSettings
        //{
        //    public float VisibleDistance = 300f;
        //    public bool WithDrivers;
        //}


		static Main()
		{
			DebugLog("Console Plugin Static Constructor");

			var exeAsm = Assembly.GetExecutingAssembly();
			if (File.Exists(exeAsm.Location))
			{
				_rootPath = Path.GetDirectoryName(exeAsm.Location);
				_consolePath = Path.Combine(_rootPath, "Console");
				_iniPath = Path.GetFullPath(Path.Combine(_consolePath, @"Console.ini"));
				if (File.Exists(_iniPath))
				{
					_enable = GetPrivateProfileInt("System", "Enable", 0, _iniPath) != 0;
					//if (_enable)
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
                            if (0 < GetPrivateProfileString("Console", "ShowKey", "BackQuote", sb, sb.Capacity,
                                _iniPath))
                            {
                                ShowKey = (WindowsInput.KeyCode) Enum.Parse(typeof(WindowsInput.KeyCode), sb.ToString());
                                ShowHotkey.keyCode = (KeyCode) Enum.Parse(typeof(KeyCode), sb.ToString());
                            }
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

		internal static void DebugLog(string msg)
		{
            //System.Console.WriteLine(msg);
            logger?.Log(msg);
            //Debug.Log(msg);
		}
        internal static void DebugError(string msg)
        {
            //System.Console.Error.WriteLine(msg);
            logger?.Error(msg);
            //Debug.LogError(msg);
        }
		internal static void DebugError(Exception ex)
		{
			//System.Console.Error.WriteLine(ex.StackTrace);
            logger?.Log(ex + "\n" + ex.StackTrace);
            //Debug.LogException(ex);
		}


        static void OnApplicationStart()
        {
			try
			{
                DebugLog($"--> Console Plugin Application Start: {_enable}");


                if (_showAtStartup)
                {
                    ShowConsole();
                }
                else if (_startHidden)
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

        private static Type AssemblyGetType(Assembly asm, string name, bool throwOnError, bool ignoreCase)
        {
            var types = asm?.GetTypes();
            return types?.FirstOrDefault(x => string.Compare(x.FullName, name, ignoreCase) == 0);
        }


		private static bool Initialized { get; set; } = false;
		/// <summary>
		/// Called during static constructor
		/// Timing is important to capture StdIn and StdOut before Unity overrides them to redirect to log files
		/// </summary>
		private static void Initialize()
        {
            if (Initialized) return;
			Initialized = true;

            DebugLog("--> Initialize");

			// load initial state
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
                    }
                }

                DebugLog("--- Preload complete");
                _startdelay = GetPrivateProfileInt("Console", "StartDelay", 0, _iniPath);
                _showAtStartup = GetPrivateProfileInt("Console", "ShowAtStartup", 0, _iniPath) != 0;
                _startHidden = GetPrivateProfileInt("Console", "StartHidden", 1, _iniPath) != 0;
            }


			// call initialize in the Unity.Console assembly
			try
			{
                DebugLog("--- Initialize");
                var dllPath = Path.GetFullPath(Path.Combine(_consolePath, @"Unity.Console.dll"));
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


                    var progType = AssemblyGetType(consoleAssembly,"Unity.Console.Engine", false, true);
                    if (progType == null)
                    {
                        DebugLog("Unable to resolve to Unity.Console.Engine");
                    }
                    else
                    {
                        var variables = new Dictionary<string, object>() { };
						var initMethod = progType.GetMethod("Initialize",
                            BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod, null, CallingConventions.Any
							, new[] { typeof(string), typeof(string), typeof(Action<string>), typeof(Dictionary<string, object>) }
							, new ParameterModifier[0]
                        );
                        if (initMethod != null)
                        {
                            DebugLog($"Initialize: {_rootPath}, {_consolePath}, {_iniPath}");
                            initMethod.Invoke(null, new object[]
                            {
                                _consolePath, _iniPath, new Action<string>(DebugLog), variables
                            });
                        }
                        else
                        {
                            DebugError("Unable to bind to initialize function");
                        }
                    }
                }
                AppDomain.CurrentDomain.ProcessExit += delegate { Shutdown(); };
                AppDomain.CurrentDomain.DomainUnload += delegate { Shutdown(); };
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

		static bool OnToggle(UnityModManagerNet.UnityModManager.ModEntry modEntry, bool value)
        {
			if (value)
            {
				DebugLog("Toggle Enable");
				if (_showAtStartup)
                    OnEnable();
			}
			else
            {
				DebugLog("Toggle Disable");
				Close();
            }

			_enable = value;
			return true;
        }

		static bool Load(UnityModManagerNet.UnityModManager.ModEntry modEntry)
		{
			try
			{
                settings = UnityModManagerNet.UnityModManager.ModSettings.Load<Settings>(modEntry);
				modEntry.OnToggle = OnToggle;
				modEntry.OnUpdate = OnUpdate;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;
				logger = modEntry.Logger;

                DebugLog($"--> Console Plugin Load");

				if (ManagerObject == null)
                {
                    ManagerObject = new GameObject("UnityConsole_Manager");
                    Object.DontDestroyOnLoad(ManagerObject);
                    ManagerComponent = ManagerObject.AddComponent<Manager>();
                    ManagerObject.SetActive(true);
                }
				if (_showAtStartup || _startHidden)
				    OnApplicationStart();

			}
			catch (Exception ex)
            {
				DebugError(ex);
			}
            DebugLog($"<-- Console Plugin Load");
			return true;
		}

        static void OnGUI(UnityModManagerNet.UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }

        static void OnSaveGUI(UnityModManagerNet.UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
		}

		//public string[] Filter => _filters;

		public static void OnDisable()
		{
            DebugLog("Console Plugin Disable");
			_enable = false;

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

		}

		/// <summary>
		/// Show Console
		/// </summary>
		private static void ShowConsole()
		{
            Initialize();

			DebugLog($"--> Console Plugin Show Console: {_startHidden}, {_startdelay}");
			
			if (runThread != null) return;
            runThread = new Thread(RunThread) { IsBackground = true };
			runThread.Start();
        }


        private static void InitEngine()
        {
            Initialize();
			DebugLog($"Console Plugin Init Engine");
            try
			{
                if (consoleAssembly == null) return;
                var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", false, true);
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
            finally
            {
            }

		}

		/// <summary>
		/// Close open console window 
		/// </summary>
		/// <param name="force"></param>
		private static void Close(bool force = false)
		{
			try
			{
				if (consoleAssembly == null) return;
				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", false, true);
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
     //           if (runThread != null)
     //           {
     //               var failed = runThread.Join(force ? 0 : 1000);
					//if (failed)
					//    runThread.Abort();
     //               runThread = null;
     //           }
			}
			catch (Exception ex)
			{
                DebugLog("Exception: " + ex.Message);
				// ignored
			}
			finally{
                //DebugLog($"Console Plugin Close: {runThread == null}");
            }
		}

		/// <summary>
		/// Shut everything down related to the console
		/// </summary>
		internal static void Shutdown()
		{
			try
			{
                DebugLog($"--> Console Plugin Shutdown: {runThread == null}");

                if (ManagerObject != null)
                {
                    ManagerComponent = null;
                    UnityEngine.Object.Destroy(ManagerObject);
				}

				if (runThread == null)
					return;
				if (consoleAssembly == null) return;
				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", false, true);
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

		private static void Toggle()
        {
            if ((DateTime.UtcNow - lastKeyProcessed).TotalSeconds < 2) return;

            lastKeyProcessed = DateTime.UtcNow;


			DebugLog("Toggle Console");
			if (runThread == null)
			{
				ShowConsole();
				OnEnable();
			}
			else
			{
				Close();
			}
		}

		static void OnEnable()
		{
            DebugLog($"Console Plugin Enable {Application.unityVersion}, {Application.version}");

			try
			{
				var version = new Version(Application.unityVersion.Replace("p", "."));
				if (version.Major > 5 || (version.Major == 5 && version.Minor >= 4))
					EnableSceneLoading();
            }
			catch
			{
				// ignore
			}

            try
            {
				ShowConsole();
                OnSceneChange(0, true);
			}
            catch (Exception a)
            {
                DebugError("Exception durin SceneChange:" + a);
            }
		}

		private static void EnableSceneLoading()
		{
			//SceneManager.sceneLoaded += OnLevelFinishedLoading;
		}
		// only supported in 5.6 and later
		private static void DisableSceneLoading()
		{
			//SceneManager.sceneLoaded -= OnLevelFinishedLoading;
		}

		private static void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
		{
            DebugLog($"Console Plugin Level Loaded {scene.name}, {mode}");

			int.TryParse(scene.name.Replace("level", ""), out var level);
			OnSceneChange(level, false);
		}
		
		private static void OnSceneChange(int level, bool init)
		{
			try
			{
                DebugLog($"--> Console Plugin Run Scene Change: {level}, {init}");
				if (consoleAssembly == null) return;
				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", false, true);
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

		public static void OnUpdate(UnityModManagerNet.UnityModManager.ModEntry modEntry, float arg)
		{
			Update();
		}

        // ReSharper disable once UnusedMember.Local
        internal static void Update()
        {
            //if (!_enable) return;

            if (WinInput.GetKey(ShowKey))
            {
                bool ControlDown = GetAsyncKeyState(0xA2) != 0 || GetAsyncKeyState(0xA3) != 0;
                bool AltDown = GetAsyncKeyState(0xA4) != 0 || GetAsyncKeyState(0xA5) != 0;
                bool ShiftDown = GetAsyncKeyState(0xA0) != 0 || GetAsyncKeyState(0xA1) != 0;
                //DebugLog("OnUpdate {0} | {1} {2} {3} | {4} {5} {6}"
                //    , ShowKey
                //    , ControlDown, AltDown, ShiftDown
                //    , ShowKeyControl ^ ControlDown
                //    , ShowKeyAlt ^ AltDown
                //    , ShowKeyShift ^ ShiftDown
                //    );
                //DebugLog("OnUpdate" + ControlDown + " " + AltDown + " " + ShiftDown);
                if (true
                    && !(ShowKeyControl ^ ControlDown)
                    && !(ShowKeyAlt ^ AltDown)
                    && !(ShowKeyShift ^ ShiftDown)
                )
                {
					Toggle();
                }
            }
			else if (ShowHotkey.Pressed())
            {
				Toggle();
			}

		}

		private static void RunThread()
		{
			try
			{
                DebugLog("-> Console Plugin Run Thread");
				if (consoleAssembly == null) return;

                if (_startdelay > 0)
                    Thread.Sleep(_startdelay);

				var progType = AssemblyGetType(consoleAssembly, "Unity.Console.Engine", false, true);
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
                Close(false);
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool WritePrivateProfileString(string lpAppName, string lpKeyName, string lpString, string lpFileName);

		[DllImport("kernel32.dll")]
		static extern int GetPrivateProfileSection(string lpAppName, byte[] lpReturnedString, int nSize, string lpFileName);

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

		[DllImport("user32.dll")]
		static extern short GetAsyncKeyState(int vKey);
	}
}
