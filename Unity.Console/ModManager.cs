using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace Unity.Console
{
    /// <summary>
    /// Manager object to manage mod loading
    /// </summary>
    internal class ModManager
    {
        internal class ModInfo
        {
            public bool Loaded { get; internal set; }
            public bool Enable { get; internal set; }
            public string Name { get; internal set; }
            public string Description { get; internal set; }
            public string ScriptsFolders { get; internal set; }
            public string StartupScriptPy { get; internal set; }
            public string SceneChangeScriptPy { get; internal set; }
            public string ReloadScriptPy { get; internal set; }
            public string ConfigFile { get; internal set; }
            public string ConfigPath { get; internal set; }

            internal CompiledCode StartupScript;
            internal CompiledCode SceneChangeScript;
            internal CompiledCode ReloadScript;
            internal ScriptScope ModScope;
        }

        public ReadOnlyDictionary<string, ModInfo> Mods = null;
        public ReadOnlyCollection<ModInfo> ModList = null;
        public string ModFolder { get; }
        public ReadOnlyDictionary<string, object> Variables { get; }

        internal ModManager(string modFolder, Dictionary<string, object> variables)
        {
            this.ModFolder = modFolder;
            this.ModList = new List<ModInfo>().AsReadOnly();
            this.Variables = new ReadOnlyDictionary<string, object>(variables);
            Engine.DebugLog($"Mods Folder: {this.ModFolder}");
        }

        public void Parse()
        {
            var mods = new List<ModInfo>();
            var dict = new Dictionary<string, ModInfo>();
            if (!string.IsNullOrEmpty(this.ModFolder) && System.IO.Directory.Exists(this.ModFolder))
            {
                foreach (var file in Directory.GetFiles(this.ModFolder, "*.ini", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fullfile = Path.GetFullPath(!Path.IsPathRooted(file) ? Path.Combine(this.ModFolder, file) : file);
                        var mod = new ModInfo()
                        {
                            ConfigFile = fullfile,
                            ConfigPath = Path.GetDirectoryName(fullfile),
                            Loaded = false,
                            Enable = Internal.GetPrivateProfileInt("ModInfo", "Enable", 0, fullfile) != 0,
                            Name = Internal.GetPrivateProfileString("ModInfo", "Name", null, fullfile),
                            Description = Internal.GetPrivateProfileString("ModInfo", "Description", null, fullfile),
                            ScriptsFolders = Internal.GetPrivateProfileString("ModInfo", "ScriptsFolders", null, fullfile),
                            StartupScriptPy = Internal.GetScriptFromSection("Startup.Script.Py", fullfile),
                            SceneChangeScriptPy = Internal.GetScriptFromSection("SceneChange.Script.Py", fullfile),
                            ReloadScriptPy = Internal.GetScriptFromSection("Reload.Script.Py", fullfile),
                        };
                        mod.StartupScript = !string.IsNullOrEmpty(mod.StartupScriptPy) ? Engine.MainEngine.CreateScriptSourceFromString(mod.StartupScriptPy, SourceCodeKind.Statements)?.Compile() : null;
                        mod.SceneChangeScript = !string.IsNullOrEmpty(mod.SceneChangeScriptPy) ? Engine.MainEngine.CreateScriptSourceFromString(mod.SceneChangeScriptPy, SourceCodeKind.Statements)?.Compile() : null;
                        mod.ReloadScript = !string.IsNullOrEmpty(mod.ReloadScriptPy) ? Engine.MainEngine.CreateScriptSourceFromString(mod.ReloadScriptPy, SourceCodeKind.Statements)?.Compile() : null;
                        mods.Add(mod);
                        dict[mod.Name] = mod;
                    }
                    catch (Exception ex)
                    {
                        Engine.DebugLog("Mods Loading Error: " + ex.Message);
                    }
                }
            }
            ModList = mods.AsReadOnly();
            Mods = new ReadOnlyDictionary<string, ModInfo>(dict);
        }

        public IEnumerable<string> GetAdditionalPaths()
        {
            
            foreach (var mod in ModList.Where(x => x.Enable))
            {
                if (!string.IsNullOrEmpty(mod.ScriptsFolders))
                {
                    foreach (var scname in mod.ScriptsFolders.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                    {
                        var path = Path.GetFullPath(Path.IsPathRooted(scname) ? scname : Path.Combine(mod.ConfigPath, scname));
                        if (Directory.Exists(path)) yield return path;
                    }
                }
            }
        }

        public void Load()
        {
            Engine.DebugLog("Mods Loading");

            foreach (var mod in ModList.Where(x => x.Enable))
            {
                try
                {
                    if (mod.ModScope == null)
                    {
                        var vars = new Dictionary<string, object>
                        {
                            {"_mods", this}, {"mod", mod}, {"log", Engine._logger}
                        };
                        foreach (var kvp in this.Variables)
                            vars[kvp.Key] = kvp.Value;
                        mod.ModScope = Engine.MainEngine.CreateScope(new Scope(vars));
                    }

                    if (mod.Loaded)
                        mod.ReloadScript?.Execute(mod.ModScope);
                    else
                    {
                        mod.StartupScript?.Execute(mod.ModScope);
                        mod.Loaded = true;
                    }
                }
                catch (Exception ex)
                {
                    Engine.DebugLog("Mods Execute Error: " + ex.Message);
                }
            }
        }

        public void Reload()
        {
            Parse();
            Load();
        }

        public void OnSceneChange(int level, bool init)
        {
            foreach (var mod in ModList.Where(x => x.Enable))
            {
                try
                {
                    if (!mod.Loaded)
                    {
                        mod.StartupScript?.Execute(mod.ModScope);
                        mod.Loaded = true;
                    }
                    mod.ModScope?.SetVariable("level", level);
                    mod.ModScope?.SetVariable("init", init);
                    if (mod.Loaded)
                        mod.SceneChangeScript?.Execute(mod.ModScope);
                }
                catch (Exception ex)
                {
                    Engine.DebugLog("Mods Scene Change Error: " + ex.Message);
                }
            }
        }
    }
}
