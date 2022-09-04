using System;
using System.IO;
using HarmonyLib;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Serialization;
using System.Collections.Generic;
using static UnityModManagerNet.UnityModManager;

namespace Tweaks
{
    #region Publics
    public class Tweak
    {
        static Tweak() => Tweaks = new Dictionary<Type, Tweak>();
        public static Dictionary<Type, Tweak> Tweaks { get; }
        public static ModEntry TweakEntry { get; internal set; }
        public void Log(object obj)
            => TweakEntry.Logger.Log($"{Runner.LogPrefix}{obj}");
        public void Enable() => Runner.Enable();
        public void Disable() => Runner.Disable();
        public virtual void OnPreGUI() { }
        public virtual void OnGUI() { }
        public virtual void OnPostGUI() { }
        public virtual void OnPatch() { }
        public virtual void OnUnpatch() { }
        public virtual void OnEnable() { }
        public virtual void OnDisable() { }
        public virtual void OnUpdate() { }
        public virtual void OnHideGUI() { }
        internal TweakRunner Runner { get; set; }
    }
    public class TweakSettings : ModSettings
    {
        static TweakSettings()
        {
            string name = new object().GetHashCode().ToString();
            dynSettingsAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
            dynSettingsModule = dynSettingsAssembly.DefineDynamicModule(name);
            settingTypes = new Dictionary<TweakAttribute, Type>();
        }
        public static Type GetSettingType(TweakAttribute metadata, Type tweakType)
        {
            if (metadata.SettingsType != null) return metadata.SettingsType;
            else if (settingTypes.TryGetValue(metadata, out Type created)) return created;
            else return settingTypes[metadata] = dynSettingsModule.DefineType($"{tweakType.FullName.Replace('.', '_').Replace('+', '_')}Settings", TypeAttributes.Public, typeof(TweakSettings)).CreateType();
        }
        static readonly AssemblyBuilder dynSettingsAssembly;
        static readonly ModuleBuilder dynSettingsModule;
        static readonly Dictionary<TweakAttribute, Type> settingTypes;
        public bool IsEnabled;
        public bool IsExpanded;
        public override string GetPath(ModEntry modEntry)
            => Path.Combine(modEntry.Path, GetType().FullName.Replace("MapConverter.", "") + ".xml");
        public override void Save(ModEntry modEntry)
        {
            var filepath = GetPath(modEntry);
            try
            {
                using (var writer = new StreamWriter(filepath))
                {
                    var serializer = new XmlSerializer(GetType());
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception e)
            {
                modEntry.Logger.Error($"Can't save {filepath}.");
                modEntry.Logger.LogException(e);
            }
        }
    }
    public static class Runner
    {
        static Runner()
        {
            OnHarmony = new Harmony($"onHarmony{new object().GetHashCode()}");
            Runners = new List<TweakRunner>();
            RunnersDict = new Dictionary<Type, TweakRunner>();
            OT = typeof(Runner).GetMethod(nameof(Runner.OnToggle), (BindingFlags)15420);
            OG = typeof(Runner).GetMethod(nameof(Runner.OnGUI), (BindingFlags)15420);
            OS = typeof(Runner).GetMethod(nameof(Runner.OnSaveGUI), (BindingFlags)15420);
            OH = typeof(Runner).GetMethod(nameof(Runner.OnHideGUI), (BindingFlags)15420);
            OU = typeof(Runner).GetMethod(nameof(Runner.OnUpdate), (BindingFlags)15420);
        }
        private static readonly MethodInfo OT;
        private static readonly MethodInfo OG;
        private static readonly MethodInfo OS;
        private static readonly MethodInfo OH;
        private static readonly MethodInfo OU;
        private static Harmony OnHarmony { get; }
        public static void Load(ModEntry modEntry) => Run(modEntry);
        public static void Run(ModEntry modEntry, bool preGUI = false, params Assembly[] assemblies)
        {
            Tweak.TweakEntry = modEntry;
            TweakTypes = new List<Type>();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly asm = assemblies[i];
                if (asm == modEntry.Assembly) continue;
                TweakTypes.AddRange(asm.GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsNested));
            }
            TweakTypes.AddRange(modEntry.Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Tweak)) && !t.IsNested));
            if (modEntry.OnToggle == null)
                modEntry.OnToggle = (m, v) => OnToggle(v);
            else OnHarmony.Patch(modEntry.OnToggle.Method, postfix: new HarmonyMethod(OT));
            if (modEntry.OnGUI == null)
                modEntry.OnGUI = (m) => OnGUI();
            else
            {
                if (preGUI)
                    OnHarmony.Patch(modEntry.OnGUI.Method, new HarmonyMethod(OG));
                else OnHarmony.Patch(modEntry.OnGUI.Method, postfix: new HarmonyMethod(OG));
            }
            if (modEntry.OnHideGUI == null)
                modEntry.OnHideGUI = (m) => OnHideGUI();
            else OnHarmony.Patch(modEntry.OnHideGUI.Method, postfix: new HarmonyMethod(OH));
            if (modEntry.OnSaveGUI == null)
                modEntry.OnSaveGUI = (m) => OnSaveGUI();
            else OnHarmony.Patch(modEntry.OnSaveGUI.Method, postfix: new HarmonyMethod(OS));
            if (modEntry.OnUpdate == null)
                modEntry.OnUpdate = (m, dt) => OnUpdate();
            else OnHarmony.Patch(modEntry.OnUpdate.Method, postfix: new HarmonyMethod(OU));
        }
        private static List<Type> TweakTypes { get; set; }
        private static Dictionary<Type, TweakRunner> RunnersDict { get; }
        private static List<TweakRunner> Runners { get; }
        private static void Start()
        {
            foreach (Type tweakType in TweakTypes.OrderBy(t => t.GetCustomAttribute<TweakAttribute>().Name).OrderBy(t => t.GetCustomAttribute<TweakAttribute>().Priority))
                RegisterTweakInternal(tweakType, null, false);
            Runners.ForEach(runner =>
            {
                if (runner.Settings.IsEnabled)
                    runner.Start(true);
            });
        }
        private static void Stop()
        {
            Runners.ForEach(runner => runner.Stop());
            Tweak.Tweaks.Clear();
            Runners.Clear();
            OnSaveGUI();
        }
        private static bool OnToggle(bool value)
        {
            if (value)
                Start();
            else Stop();
            return true;
        }
        private static void OnHideGUI() => Runners.ForEach(runner => runner.OnHideGUI());
        private static void OnGUI() => Runners.ForEach(runner => runner.OnGUI());
        private static void OnSaveGUI()
            => SyncSettings.Save(Tweak.TweakEntry);
        private static void OnUpdate()
            => Runners.ForEach(runner => runner.OnUpdate());
        internal static void RegisterTweakInternal(Type tweakType, TweakRunner outerRunner, bool last, int innerTime = 0)
        {
            try
            {
                if (tweakType.BaseType != typeof(Tweak) && outerRunner == null) return;
                if (Tweak.Tweaks.Keys.Contains(tweakType)) return;
                Tweak tweak = InitTweak(tweakType, out var settings, out var attr);
                TweakRunner runner = new TweakRunner(tweak, attr, settings, last, outerRunner, innerTime);
                tweak.Runner = runner;
                if (outerRunner != null) outerRunner.InnerTweaks.Add(runner);
                else runner.Last = Tweak.Tweaks.Values.Last() == tweak;
                if (outerRunner == null)
                    Runners.Add(runner);
                var nestedTypes = tweakType.GetNestedTypes((BindingFlags)15420).Where(t => t.IsSubclassOf(tweakType));
                if (nestedTypes.Any())
                {
                    var lastType = nestedTypes.Last();
                    innerTime++;
                    foreach (Type type in nestedTypes)
                        RegisterTweakInternal(type, runner, type == lastType, innerTime);
                }
                SyncSettings.Sync(tweak.GetType(), tweak);
                SyncTweak.Sync(tweak.GetType(), tweak);
                if (runner.Metadata.PatchesType != null)
                {
                    SyncSettings.Sync(runner.Metadata.PatchesType, tweak);
                    SyncTweak.Sync(runner.Metadata.PatchesType, tweak);
                }
            }
            catch (Exception e)
            {
                Tweak.TweakEntry.Logger.Log($"{tweakType}\n{e}");
                throw e;
            }
        }
        internal static Tweak InitTweak(Type tweakType, out TweakSettings settings, out TweakAttribute attr)
        {
            ConstructorInfo constructor = tweakType.GetConstructor(new Type[] { });
            Tweak tweak = (Tweak)constructor.Invoke(null);
            attr = tweakType.GetCustomAttribute<TweakAttribute>();
            if (attr == null)
                throw new NullReferenceException("Cannot Find Tweak Metadata! (TweakAttribute)");
            Type settingType = TweakSettings.GetSettingType(attr, tweakType);
            SyncSettings.Register(Tweak.TweakEntry, settingType);
            settings = SyncSettings.Settings[settingType];
            Tweak.Tweaks.Add(tweakType, tweak);
            return tweak;
        }
    }
    #endregion
    #region Internals
    internal class TweakGroup
    {
        public List<TweakRunner> runners;
        public TweakGroup(List<TweakRunner> runners)
            => this.runners = runners;
        public void Enable(TweakRunner runner)
        {
            foreach (TweakRunner rnr in runners.Where(r => r != runner))
                rnr.Disable();
        }
    }
    internal class TweakRunner
    {
        public static Dictionary<int, Dictionary<string, TweakGroup>> Groups = new Dictionary<int, Dictionary<string, TweakGroup>>();
        public static GUIStyle Expan;
        public static GUIStyle Enabl;
        public static GUIStyle Enabl_Label;
        public static GUIStyle Descr;
        public static bool StyleInitialized = false;
        public Tweak Tweak { get; }
        public TweakRunner OuterTweak { get; }
        public List<TweakRunner> InnerTweaks { get; }
        public TweakAttribute Metadata { get; }
        public TweakSettings Settings { get; internal set; }
        public List<TweakPatch> Patches { get; }
        public Harmony Harmony { get; }
        public bool Inner { get; }
        public bool Last;
        public int InnerTime;
        public TweakGroup Group;
        public TweakRunner(Tweak tweak, TweakAttribute attr, TweakSettings settings, bool last, TweakRunner outerTweak, int innerTime)
        {
            Type tweakType = tweak.GetType();
            Tweak = tweak;
            Metadata = attr;
            Settings = settings;
            Patches = new List<TweakPatch>();
            Harmony = new Harmony($"Tweaks.{Metadata.Name}");
            InnerTweaks = new List<TweakRunner>();
            OuterTweak = outerTweak;
            Inner = outerTweak != null;
            InnerTime = innerTime;
            TweakGroupAttribute group = tweakType.GetCustomAttribute<TweakGroupAttribute>();
            if (group != null)
            {
                if (!Groups.TryGetValue(innerTime, out var groups))
                    Groups.Add(innerTime, groups = new Dictionary<string, TweakGroup>());
                if (groups.TryGetValue(group.Id, out Group))
                    Group.runners.Add(this);
                else groups.Add(group.Id, Group = new TweakGroup(new List<TweakRunner>() { this }));
            }
            Last = last;
            if (Metadata.PatchesType != null)
                AddPatches(Metadata.PatchesType, true);
            AddPatches(tweakType, false);
            Patches = Patches.OrderBy(t => t.Priority).ToList();
            if (Metadata.MustNotBeDisabled)
                Settings.IsEnabled = true;
        }
        public string LogPrefix
        {
            get
            {
                if (logPrefix != null) return logPrefix;
                StringBuilder sb = new StringBuilder();
                TweakRunner runner = this;
                List<string> names = new List<string>();
                while (runner.OuterTweak != null)
                {
                    names.Add(runner.OuterTweak.Metadata.Name);
                    runner = runner.OuterTweak;
                }
                names.Reverse();
                foreach (string name in names)
                    sb.Append($"[{name}] ");
                sb.Append($"[{Metadata.Name}] ");
                return logPrefix = sb.ToString();
            }
        }
        private string logPrefix;
        public void Start(bool force = false)
        {
            if (force || !Settings.IsEnabled)
                Enable();
            InnerTweaks.ForEach(runner =>
            {
                if (runner.Settings.IsEnabled)
                    runner.Start(true);
            });
        }
        public void Stop()
        {
            if (Settings.IsEnabled)
                Disable();
        }
        public void Enable()
        {
            Tweak.OnEnable();
            if (Metadata.PatchesType != null)
                foreach (Type type in GetNestedTypes(Metadata.PatchesType))
                    Harmony.CreateClassProcessor(type).Patch();
            foreach (Type type in Tweak.GetType().GetNestedTypes((BindingFlags)15420))
                Harmony.CreateClassProcessor(type).Patch();
            foreach (var patch in Patches)
            {
                if (patch.Prefix)
                    Harmony.Patch(patch.Target, new HarmonyMethod(patch.Patch));
                else Harmony.Patch(patch.Target, postfix: new HarmonyMethod(patch.Patch));
            }
            Tweak.OnPatch();
            Settings.IsEnabled = true;
            Group?.Enable(this);
        }
        public void Disable()
        {
            if (Metadata.MustNotBeDisabled) return;
            Tweak.OnDisable();
            Harmony.UnpatchAll(Harmony.Id);
            Tweak.OnUnpatch();
            InnerTweaks.ForEach(runner => runner.Disable());
            Settings.IsEnabled = false;
        }
        public void OnGUI()
        {
            if (!StyleInitialized)
            {
                Expan = new GUIStyle()
                {
                    fixedWidth = 10,
                    normal = new GUIStyleState() { textColor = Color.white },
                    fontSize = 15,
                    margin = new RectOffset(4, 2, 6, 6),
                };
                Enabl = new GUIStyle(GUI.skin.toggle)
                {
                    margin = new RectOffset(0, 4, 4, 4),
                };
                Enabl_Label = new GUIStyle(GUI.skin.label)
                {
                    margin = new RectOffset(0, 4, 4, 4),
                };
                Descr = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Italic,
                };
                StyleInitialized = true;
            }
            Tweak.OnPreGUI();
            GUILayout.BeginHorizontal();
            bool newIsExpanded;
            bool newIsEnabled = false;
            if (Metadata.MustNotBeDisabled)
            {
                newIsExpanded = GUILayout.Toggle(Settings.IsExpanded, Settings.IsEnabled ? (Settings.IsExpanded ? "◢" : "▶") : "", Expan);
                GUILayout.Label(Metadata.Name, Enabl_Label);
                if (Metadata.Description != null)
                {
                    GUILayout.Label("-");
                    GUILayout.Label(Metadata.Description, Descr);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                newIsExpanded = GUILayout.Toggle(Settings.IsExpanded, Settings.IsEnabled ? (Settings.IsExpanded ? "◢" : "▶") : "", Expan);
                newIsEnabled = GUILayout.Toggle(Settings.IsEnabled, Metadata.Name, Enabl);
                if (Metadata.Description != null)
                {
                    GUILayout.Label("-");
                    GUILayout.Label(Metadata.Description, Descr);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if (newIsEnabled != Settings.IsEnabled)
                {
                    Settings.IsEnabled = newIsEnabled;
                    if (newIsEnabled)
                    {
                        Enable();
                        newIsExpanded = true;
                    }
                    else Disable();
                }
            }
            if (newIsExpanded != Settings.IsExpanded)
            {
                Settings.IsExpanded = newIsExpanded;
                if (!newIsExpanded)
                    Tweak.OnHideGUI();
            }
            if (Settings.IsExpanded && Settings.IsEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Metadata.IndentSize);
                GUILayout.BeginVertical();
                Tweak.OnGUI();
                InnerTweaks.ForEach(runner => runner.OnGUI());
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                if (!Last)
                    GUILayout.Space(Metadata.SpacingSize);
            }
            Tweak.OnPostGUI();
        }
        public void OnUpdate()
        {
            if (Settings.IsEnabled)
                Tweak.OnUpdate();
            InnerTweaks.ForEach(runner => runner.OnUpdate());
        }
        public void OnHideGUI()
        {
            if (Settings.IsEnabled)
                Tweak.OnHideGUI();
            InnerTweaks.ForEach(runner => runner.OnHideGUI());
        }
        private void AddPatches(Type patchesType, bool patchNestedTypes)
        {
            void AddPatches(Type t)
            {
                foreach (MethodInfo method in t.GetMethods((BindingFlags)15420))
                {
                    IEnumerable<TweakPatch> patches = method.GetCustomAttributes<TweakPatch>(true);
                    foreach (TweakPatch patch in patches)
                    {
                        if (patch.IsValid)
                        {
                            patch.Patch = method;
                            if (patch.Target == null)
                                patch.Target = TweakPatch.FindMethod(patch.Patch.Name.Replace(patch.Splitter, '.'), patch.MethodType, false);
                            if (patch.Target == null)
                            {
                                if (patch.ThrowOnNull)
                                    throw new NullReferenceException("Cannot Patch Due To Target Is Null!");
                                else continue;
                            }
                            Patches.Add(patch);
                        }
                    }
                }
            }
            if (patchNestedTypes)
            {
                AddPatches(patchesType);
                foreach (Type type in GetNestedTypes(patchesType))
                    AddPatches(type);
            }
            else AddPatches(patchesType);
        }
        public static List<Type> GetNestedTypes(Type type)
        {
            void GetNestedTypes(Type ty, List<Type> toContain)
            {
                foreach (Type t in ty.GetNestedTypes((BindingFlags)15420))
                {
                    toContain.Add(t);
                    GetNestedTypes(t, toContain);
                }
            }
            var container = new List<Type>();
            GetNestedTypes(type, container);
            return container;
        }
    }
    #endregion
    #region Attributes
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class TweakPatch : Attribute
    {
        public static readonly int Version = (int)AccessTools.Field(typeof(GCNS), "releaseNumber").GetValue(null);
        internal MethodInfo Patch;
        public bool Prefix;
        public string PatchId;
        public int Priority;
        public int MinVersion;
        public int MaxVersion;
        public bool ThrowOnNull;
        public GSCS MethodType;
        public char Splitter = '_';
        public MethodBase Target;
        internal TweakPatch(MethodBase target)
            => Target = target;
        public TweakPatch(Type type, string name, params Type[] parameterTypes) : this(type, name, GSCS.None, parameterTypes) { }
        public TweakPatch(Type type, string name, GSCS methodType, params Type[] parameterTypes) : this(methodType)
        {
            if (methodType != GSCS.None)
            {
                switch (methodType)
                {
                    case GSCS.Getter:
                        var prop = type.GetProperty(name, (BindingFlags)15420);
                        Target = prop.GetGetMethod(true);
                        break;
                    case GSCS.Setter:
                        prop = type.GetProperty(name, (BindingFlags)15420);
                        Target = prop.GetSetMethod(true);
                        break;
                    case GSCS.Constructor:
                        Target = type.GetConstructor((BindingFlags)15420, null, parameterTypes, null);
                        break;
                    case GSCS.StaticConstructor:
                        Target = type.TypeInitializer;
                        break;
                }
            }
            else Target = parameterTypes.Any() ? type.GetMethod(name, (BindingFlags)15420, null, parameterTypes, null) : type.GetMethod(name, (BindingFlags)15420);
        }
        public TweakPatch(string fullName, GSCS methodType = GSCS.None) : this(methodType)
            => Target = FindMethod(fullName, MethodType, false);
        public TweakPatch(GSCS methodType = GSCS.None)
            => MethodType = methodType;
        public bool IsValid => (MinVersion == -1 || Version >= MinVersion) && (MaxVersion == -1 || Version <= MaxVersion);
        public static MethodBase FindMethod(string fullName, GSCS methodType = GSCS.None, bool filterProp = true, bool throwOnNull = false)
        {
            var split = fullName.Split('.');
            if ((split[0] == "get" || split[0] == "set") && filterProp)
            {
                var array = new string[split.Length - 1];
                Array.Copy(split, 1, array, 0, array.Length);
                split = array;
            }
            var paramBraces = (string)null;
            if (fullName.Contains("("))
                split = fullName.Replace(paramBraces = fullName.Substring(fullName.IndexOf('(')), "").Split('.');
            var method = split.Last();
            var type = fullName.Replace($".{(method.Contains("ctor") ? $".{method}" : method)}{paramBraces}", "");
            var isParam = false;
            var parameterTypes = new List<Type>();
            if (paramBraces != null)
            {
                isParam = true;
                var parametersString = paramBraces.Replace("(", "").Replace(")", "");
                if (string.IsNullOrWhiteSpace(parametersString))
                    goto Skip;
                var parameterSplit = parametersString.Split(',');
                parameterTypes = parameterSplit.Select(s => AccessTools.TypeByName(s)).ToList();
            }
        Skip:
            var decType = AccessTools.TypeByName(type);
            if (decType == null && throwOnNull)
                throw new NullReferenceException($"Cannot Find Type! ({type})");
            var parameterArr = parameterTypes.ToArray();
            var result = (MethodBase)null;
            if (methodType != GSCS.None)
            {
                var prop = decType.GetProperty(method, (BindingFlags)15420);
                switch (methodType)
                {
                    case GSCS.Getter:
                        result = prop.GetGetMethod(true);
                        break;
                    case GSCS.Setter:
                        result = prop.GetSetMethod(true);
                        break;
                }
            }
            else
            {
                if (method == "ctor")
                    result = decType.GetConstructor((BindingFlags)15420, null, parameterArr, null);
                else if (method == "cctor")
                    result = decType.TypeInitializer;
                else
                    result = isParam ? decType.GetMethod(method, (BindingFlags)15420, null, parameterTypes.ToArray(), null) : decType.GetMethod(method, (BindingFlags)15420);
            }
            if (result == null && throwOnNull)
                throw new NullReferenceException($"Cannot Find Method! ({method})");
            return result;
        }
    }
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TweakAttribute : Attribute
    {
        public TweakAttribute(string name, string desc = null)
        {
            Name = name;
            Description = desc;
            IndentSize = 24f;
            SpacingSize = 12f;
        }
        public string Name;
        public string Description;
        public Type PatchesType;
        public Type SettingsType;
        public bool MustNotBeDisabled;
        public int Priority;
        public float IndentSize;
        public float SpacingSize;
    }
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TweakGroupAttribute : Attribute
    {
        public string Id;
        public TweakGroupAttribute() => Id = "Default";
    }
    public class SyncSettings : Attribute
    {
        public static Dictionary<Type, TweakSettings> Settings = new Dictionary<Type, TweakSettings>();
        static readonly MethodInfo load = typeof(ModSettings).GetMethod(nameof(ModSettings.Load), (BindingFlags)15420, null, new Type[] { typeof(ModEntry) }, null);
        public static void Register(ModEntry modEntry, Type settingsType)
        {
            try { Settings[settingsType] = (TweakSettings)load.MakeGenericMethod(settingsType).Invoke(null, new object[] { modEntry }); }
            catch { Settings[settingsType] = (TweakSettings)Activator.CreateInstance(settingsType); }
        }
        public static void Sync(Type type, object instance = null)
        {
            foreach (var field in type.GetFields((BindingFlags)15420))
            {
                SyncSettings sync = field.GetCustomAttribute<SyncSettings>();
                if (sync != null)
                    if (field.IsStatic)
                        field.SetValue(null, Settings[field.FieldType]);
                    else field.SetValue(instance, Settings[field.FieldType]);
            }
            foreach (var prop in type.GetProperties((BindingFlags)15420))
            {
                SyncSettings sync = prop.GetCustomAttribute<SyncSettings>();
                if (sync != null)
                    if (prop.GetGetMethod(true).IsStatic)
                        prop.SetValue(null, Settings[prop.PropertyType]);
                    else prop.SetValue(instance, Settings[prop.PropertyType]);
            }
        }
        public static void Save(ModEntry modEntry)
        {
            foreach (var setting in Settings.Values)
                setting.Save(modEntry);
        }
    }
    public class SyncTweak : Attribute
    {
        public static void Sync(Type type, object instance = null)
        {
            foreach (var field in type.GetFields((BindingFlags)15420))
            {
                SyncTweak sync = field.GetCustomAttribute<SyncTweak>();
                if (sync != null)
                    if (field.IsStatic)
                        field.SetValue(null, Tweak.Tweaks[field.FieldType]);
                    else field.SetValue(instance, Tweak.Tweaks[field.FieldType]);
            }
            foreach (var prop in type.GetProperties((BindingFlags)15420))
            {
                SyncTweak sync = prop.GetCustomAttribute<SyncTweak>();
                if (sync != null)
                    if (prop.GetGetMethod(true).IsStatic)
                        prop.SetValue(null, Tweak.Tweaks[prop.PropertyType]);
                    else prop.SetValue(instance, Tweak.Tweaks[prop.PropertyType]);
            }
        }
    }
    public enum GSCS
    {
        None,
        Getter,
        Setter,
        Constructor,
        StaticConstructor
    }
    #endregion
}
