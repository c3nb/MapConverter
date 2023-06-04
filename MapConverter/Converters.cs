using System;
using System.Linq;
using AdofaiMapConverter.Converters;
using AdofaiMapConverter.Helpers;
using AdofaiMapConverter.Converters.Effects;
using Tweaks;
using UnityEngine;
using System.IO;
using System.Threading;
using System.Collections;
using HarmonyLib;
using JSON;
using System.Reflection;
using System.Collections.Generic;
using ACL = AdofaiMapConverter.CustomLevel;
using System.Reflection.Emit;
using LevelEventType = AdofaiMapConverter.Types.LevelEventType;
using PropertyInfo = System.Reflection.PropertyInfo;
using ADOFAI;
#if HAS_OPENCV
using System.Drawing;
using OpenCvSharp;
using AdofaiMapConverter.Generators;
#endif

namespace MapConverter
{
    [Tweak("Normal Converters", Priority = -1)]
    [TweakGroup(Id = "_")]
    public class NormalConverters : Tweak
    {
        public static readonly FastInvokeHandler checkUnsavedChanges = MethodInvoker.GetHandler(AccessTools.Method(typeof(scnEditor), "CheckUnsavedChanges"));
        public static readonly FastInvokeHandler pauseIfUnpaused = MethodInvoker.GetHandler(AccessTools.Method(typeof(scnEditor), "PauseIfUnpaused"));
        public static readonly FastInvokeHandler openLevelCo = MethodInvoker.GetHandler(AccessTools.Method(typeof(scnEditor), "OpenLevelCo"));
        public const string iL = "RJTUTJRRRURURURURDRDRDRDRRDLLDRRDLLLLDRRDLLDRDRRDLLDRRDLLLLDRRDLLFBRBFLFBRRULLDRRRRDLLDRRDLLLLBTLLDRRDLLDRRRRDLLLFBRBFLFLLCLCLCLGTGTGLFLLCLCLCLDLDLLDRRRDLLFBRTGLFBRRRDRRDLLLDRRBFLFBRRRRRDRRDLLLLDRBFLFBRBFLFBRRRRDLDRDLDRDLLLDDRRRRDLDDLLDDRUULLLDRULLLURRRULLLGTRDLLLLDLLLURDLLLURDLDRRCDLDLQLZDCDCREUEUQLZZCZZZZQEEQZZZZCEQQZZZCELCCCCEQECCCCZQRZZZZQEQZZQEDQQQECEQQQZCUZZZZZCEQZZZRZRZZZZZUZUZZZZZRZRZZZZZZQECCCZLQQQQZCZQQQZDCCEDRZCEDRZCCCCCCCCCCCEECCCCCCCCEEQEECEEEDTDTBTBTDTDTDDRQDRURDDRQDRURDDRQDRURDRDDDRZUERDRZUURZUERUERUUZURUUULUCUCUCUCCLCLCLCLLULULULUURURURURRZRZRZRZZUZUZUZUURURURURRDRDRDRDDQDQDQDQQRQRQRQRRDRDRDRDDLDLDLDLLELELELEEDEDEDEDDLDLDLDLDDDRULDDDEDEDDDLURDDDDDQDQDDDDDDDEDEDRUCUCUUUZUZUUUUUUUURRDDDLLUUURRDDDDDRUCEDRUCEDREURDEDEDEDDDQDQDDDRULDDRRUUCUCUUUUUUUCUCURDEDEDDLDEDEDDDDDDDDLLUURDDDDDLLUURDDDDLCZRDLCZRDZLDDDDDRURDRURDRDRDRDRDDDDDDDDDGDGDHDHDGDGDDRRDDLECULLDDLLJLJLJLJLJLJLLDDLLUCZRUULLUUMUMUMUMUMUMUULLUUBUBUBUMUMUMUULLUUZUZUZUZZCLDQZCLDQZCLDQZULEQZZCCZZQRDEQQZZQEQQQQQQZZQQEDLCEEQQECEEEEEEEEEEQQECEEEEEEQQECEEEEEEEQQZQQEQQZZZZZCEQZZZZZRDQZZRDQZZQEQZQEQZZZZZZZZZLQUER";
        public static void OpenLevel(string path)
        {
            var editor = scnEditor.instance;
            checkUnsavedChanges(editor, (Action)(() =>
            {
                pauseIfUnpaused(editor);
                editor.StartCoroutine((IEnumerator)openLevelCo(editor, path));
            }));
            if (ADOBase.isCLSLevel)
                editor.DeselectFloors(true);
        }
        public static void iLConvert()
        {
            FileInfo file = new FileInfo(Main.Path);
            ACL result = ACL.Read(JsonNode.Parse(File.ReadAllText(Main.Path)));
            var ad = AngleHelper.ReadPathData(iL).Select(ta => ta.isMidspin ? 999 : ta.Angle);
            ACL converted = ShapeConverter.Convert(result, ad);
            var resultPath = $"{file.DirectoryName}/iL {file.Name}";
            File.WriteAllText(resultPath, converted.ToNode().ToString(4));
            OpenLevel(resultPath);
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Outer Angle Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class OuterConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var result = OuterConverter.Convert(map);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Outer {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Linear Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class LinearConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var result = LinearConverter.Convert(map);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Linear {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Pattern Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class PatternConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("PathData Or AngleData Or MapPath:");
                Setting.Data = GUILayout.TextField(Setting.Data);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var result = default(ACL);
                    try
                    {
                        if (File.Exists(Setting.Data))
                            result = ShapeConverter.Convert(map, ACL.Read(JsonNode.Parse(File.ReadAllText(Setting.Data))));
                        else if (Setting.Data.Contains(","))
                            result = ShapeConverter.Convert(map, Setting.Data.Split(',').Select(FastParser.ParseDouble));
                        else result = ShapeConverter.Convert(map, AngleHelper.ReadPathData(Setting.Data).Select(ta => ta.Angle));
                    }
                    catch (Exception e)
                    {
                        result = null;
                        Notification = e.Message;
                        return;
                    }
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Pattern {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string Data = string.Empty;
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("NoSpeedChange Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class NoSpeedChangeConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Destination Bpm:");
                Setting.DestBpm = GUILayout.TextField(Setting.DestBpm);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var result = NoSpeedChangeConverter.Convert(map, FastParser.ParseDouble(Setting.DestBpm));
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Pattern {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string DestBpm = string.Empty;
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Twirl Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class TwirlConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Twirl Rate (0.0 ~ 1.0):");
                Setting.TwirlRate = GUILayout.TextArea(Setting.TwirlRate);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var twirlRate = FastParser.ParseDouble(Setting.TwirlRate);
                    var result = TwirlConverter.Convert(map, twirlRate);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Twirl {twirlRate * 100}% {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string TwirlRate = "1";
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Bpm Multiply Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class BpmMultiplyConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Multiply:");
                Setting.Multiply = GUILayout.TextArea(Setting.Multiply);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var multiply = FastParser.ParseDouble(Setting.Multiply);
                    var result = BpmMultiplyConverter.Convert(map, multiply);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Multiply {multiply}x {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string Multiply = "1";
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Chaos Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class ChaosConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("VibrationRate:");
                Setting.VibrationRate = GUILayout.TextArea(Setting.VibrationRate);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var vibrationRate = FastParser.ParseDouble(Setting.VibrationRate);
                    var result = ChaosConverter.Convert(map, vibrationRate);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Chaos {vibrationRate} {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string VibrationRate = "1";
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("All Midspin Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class AllMidspinConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Midspin Count:");
                Setting.MidspinCount = GUILayout.TextArea(Setting.MidspinCount);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var midspinCount = FastParser.ParseInt(Setting.MidspinCount);
                    var result = AllMidspinConverter.Convert(map, midspinCount);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/AllMidspin {midspinCount} {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string MidspinCount = "1";
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Pseudo Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class PseudoConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Pseudo Count:");
                Setting.PseudoCount = GUILayout.TextArea(Setting.PseudoCount);
                GUILayout.Label("Pseudo Max Angle:");
                Setting.PseudoMaxAngle = GUILayout.TextArea(Setting.PseudoMaxAngle);
                Setting.RemoveColorTrack = GUILayout.Toggle(Setting.RemoveColorTrack, "Remove Color Track");
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var pseudoCount = FastParser.ParseInt(Setting.PseudoCount);
                    var pseudoMaxAngle = FastParser.ParseDouble(Setting.PseudoMaxAngle);
                    var result = PseudoConverter.Convert(map, pseudoCount, pseudoMaxAngle, Setting.RemoveColorTrack);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Pseudo {pseudoCount} {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string PseudoCount = "1";
                public string PseudoMaxAngle = "30";
                public bool RemoveColorTrack = false;
            }
        }
        [TweakGroup(Id = "Normal Converters")]
        [Tweak("Planet Amount Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class PlanetConverterTweak : NormalConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            private Harmony harmony;
            private KeyCombo breakLimitCombo = new KeyCombo("breaklimit");
            private bool limitBroken = false;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
                if (limitBroken)
                {
                    harmony = new Harmony("Planet Amount Converter Patch");
                    harmony.CreateClassProcessor(typeof(Patches.BreakPlanetsLimit_CustomLevel)).Patch();
                }
            }
            public override void OnDisable()
            {
                harmony?.UnpatchAll(harmony?.Id);
                harmony = null;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Planet Amount:");
                Setting.PlanetAmount = GUILayout.TextArea(Setting.PlanetAmount);
                Setting.KeepShape = GUILayout.Toggle(Setting.KeepShape, "Keep Shape");
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
                if (breakLimitCombo.Check())
                {
                    scrFlash.Flash(Color.white);
                    limitBroken = true;
                    OnEnable();
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var planetAmount = FastParser.ParseInt(Setting.PlanetAmount);
                    var result = PlanetConverter.Convert(map, planetAmount, Setting.KeepShape);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Planet {planetAmount} {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string PlanetAmount = "1";
                public bool KeepShape = true;
            }
            public class Patches
            {
                [HarmonyPatch]
                public static class BreakPlanetsLimit_CustomLevel
                {
                    public static IEnumerable<MethodBase> TargetMethods()
                    {
                        yield return AccessTools.Method(typeof(scnGame), "ApplyCoreEventsToFloors", new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>), typeof(List<LevelEvent>[]) });
                        yield return AccessTools.Method(typeof(scnGame), "ApplyEventsToFloors", new[] { typeof(List<scrFloor>), typeof(LevelData), typeof(scrLevelMaker), typeof(List<LevelEvent>) });
                    }
                    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
                    {
                        List<CodeInstruction> insts = new List<CodeInstruction>(instructions);
                        for (int i = 0; i < insts.Count; i++)
                        {
                            var inst = insts[i];
                            if (inst.operand is string s && s == "Planets more than 3 works but is an unreleased feature right now. If you're reading this, please do not release a mod to disable it or share footage, so we can keep the spoiler")
                                insts.RemoveRange(i - 12, 14);
                        }
                        return insts;
                    }
                    public static void RemoveMethodCall(List<CodeInstruction> insts, MethodInfo target, int start = -1, int end = -1)
                    {
                        List<int> calls = new List<int>();
                        for (int i = start < 0 ? 0 : start; i < (end < 0 ? insts.Count : end); i++)
                        {
                            CodeInstruction inst = insts[i];
                            if (inst.opcode != OpCodes.Call)
                                continue;
                            if (inst.operand is MethodInfo meth && meth == target)
                                calls.Add(i);
                        }
                        if (!calls.Any()) return;
                        int parameterCount = target.GetParameters().Length;
                        bool isInstance = !target.IsStatic;
                        bool hasReturn = target.ReturnType != typeof(void);
                        int offset = 0;
                        foreach (int call in calls)
                        {
                            int from;
                            insts.RemoveAt(from = call + offset--);
                            int count = parameterCount + (isInstance ? 1 : 0);
                            for (int i = 0; i < count; i++)
                                insts.Insert(i + call + offset++, new CodeInstruction(OpCodes.Pop));
                            if (hasReturn)
                                insts.RemoveAt(call + offset--);
                        }
                    }
                }
            }
        }
        [HarmonyPatch(typeof(scnEditor), "OpenLevel")]
        public static class SetMapPath
        {
            public static void Postfix(scnEditor __instance) => Main.Path = __instance.customLevel?.levelPath;
        }
    }
    [Tweak("Effect Converters", Priority = 0)]
    [TweakGroup(Id = "_")]
    public class EffectConverters : Tweak
    {
        [TweakGroup(Id = "Effect Converters")]
        [Tweak("Effect Remove Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class EffectRemoveConverterTweak : EffectConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("To Remove EventTypes:");
                Setting.EventTypes = GUILayout.TextField(Setting.EventTypes);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        NormalConverters.OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var evtTypes = Setting.EventTypes.Split(',').Select(s => (LevelEventType)Enum.Parse(typeof(LevelEventType), s));
                    var result = NonEffectConverter.Convert(map, evtTypes);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/NonEffect {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string EventTypes = string.Empty;
            }
        }
        [TweakGroup(Id = "Effect Converters")]
        [Tweak("Transparency Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class TransparentConverterTweak : EffectConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Transparency (0 ~ 100):");
                Setting.Transparency = GUILayout.TextField(Setting.Transparency);
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        NormalConverters.OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var transparency = FastParser.ParseInt(Setting.Transparency);
                    var result = TransparentConverter.Convert(map, transparency);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/Transparency {transparency} {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                public string Transparency = string.Empty;
            }
        }
        [TweakGroup(Id = "Effect Converters")]
        [Tweak("Bpm Multiplier To Bpm Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class MultiplyToBpmConverterTweak : EffectConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Convert"))
                {
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        NormalConverters.OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    var mapStr = File.ReadAllText(file.FullName);
                    var map = ACL.Read(JsonNode.Parse(mapStr));
                    var result = OnlyBpmSetConverter.Convert(map);
                    var resultStr = result.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/NonEffect {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
            }
        }
        [TweakGroup(Id = "Effect Converters")]
        [Tweak("Value Converter", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class ValueConverterTweak : EffectConverters
        {
            public bool Converted = false;
            public bool Converting = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            private void UpdateCurrent()
            {
                try
                {
                    if (current == null || currentPath != Main.Path)
                        current = ACL.Read(JsonNode.Parse(File.ReadAllText(currentPath = Main.Path)));
                }
                catch (ArgumentException)
                {
                    Notification = $"'{currentPath}' Is Not Valid Path!";
                }
            }
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevConvertedPath;
            }
            private ACL current;
            private string currentPath;
            private RangeInt range = new RangeInt();
            private string eventType;
            public override void OnGUI()
            {
                Converted = Setting.PrevConvertedPath == Main.Path;
                if (!Converted && !Converting) Notification = string.Empty;

                GUILayout.BeginHorizontal();
                GUILayout.Label("EventType:");
                eventType = GUILayout.TextField(eventType);

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("From:");
                    string from = range.start.ToString();
                    from = GUILayout.TextField(from);
                    range.start = FastParser.ParseInt(from);

                    GUILayout.Label("To:");
                    string to = range.end.ToString();
                    to = GUILayout.TextField(to);
                    range.length = FastParser.ParseInt(to) - range.start;

                    if (GUILayout.Button("All"))
                    {
                        UpdateCurrent();
                        range = new RangeInt(0, current.Tiles.Count);
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Add"))
                {
                    UpdateCurrent();
                    LevelEventType realType;
                    try { realType = (LevelEventType)Enum.Parse(typeof(LevelEventType), eventType); }
                    catch { Notification = $"{eventType} Is Does Not Exist In LevelEventType!"; goto Continue; }
                    if (!Setting.Datas.Any(d => d.EventType == realType))
                        Setting.Datas.Add(new Data(current, range, realType));
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                foreach (Data data in Setting.Datas)
                    data.RenderGUI();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Convert"))
                {
                    if (Setting.Datas.Count <= 0)
                    {
                        Notification = "Cannot Convert 0 Changes!";
                        goto Continue;
                    }
                    Notification = "Converting...";
                    GetConvertThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            Continue:
                return;
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        NormalConverters.OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetConvertThread()
            {
                return new Thread(() =>
                {
                    Converting = true;
                    var file = new FileInfo(Main.Path);
                    Setting.Datas.ForEach(d => d.ChangeValue());
                    var resultStr = current.ToNode().ToString(4);
                    var resultPath = $"{file.DirectoryName}/{AggregateEvents(Setting.Datas)} Changed {file.Name}";
                    File.WriteAllText(resultPath, resultStr);
                    Setting.PrevConvertedPath = Main.Path;
                    Notification = "Convert Successful.";
                    Converting = false;
                    Converted = true;
                    ToOpenLevel = resultPath;
                });
            }
            static string AggregateEvents(List<Data> datas)
            {
                if (datas.Count == 1) return datas[0].EventType.ToString();
                return datas.Aggregate("", (c, d) => $"{c}{d.EventType},");
            }
            public class Settings : TweakSettings
            {
                public string PrevConvertedPath = string.Empty;
                internal List<Data> Datas = new List<Data>();
            }
            public class Data
            {
                public Data() { }
                public ACL Level;
                public RangeInt Range;
                public LevelEventType EventType;
                public Data(ACL level, RangeInt range, LevelEventType eventType)
                {
                    Level = level;
                    Range = range;
                    EventType = eventType;
                    Changes = new Dictionary<string, ChangeData>();
                    EventImplType = typeof(MapConverterBase).Assembly.GetType($"AdofaiMapConverter.Actions.{eventType}");
                }
                public bool Change = true;
                public Dictionary<string, ChangeData> Changes;
                public Type EventImplType;
                public bool AddChange(string name, object value, bool relative)
                {
                    PropertyInfo prop = EventImplType.GetProperty(name);
                    if (prop == null) return false;
                    Changes.Add(name, new ChangeData(prop, value, relative));
                    return true;
                }
                public bool RemoveChange(string name)
                    => Changes.Remove(name);
                public void ChangeValue()
                {
                    if (!Change) return;
                    var actions = Level.Tiles
                        .GetRange(Range.start, Range.length)
                        .SelectMany(t => t.GetActions(EventType));
                    foreach (var change in Changes.Values)
                        foreach (var action in actions)
                            change.Change(action);
                }
                private string name;
                private string notification;
                private string toRemoveQueue;
                public void RenderGUI()
                {
                    if (toRemoveQueue != null)
                    {
                        Changes.Remove(toRemoveQueue);
                        toRemoveQueue = null;
                    }
                    if (Change = GUILayout.Toggle(Change, $"<b>Change {EventType}</b>"))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Event Property Name:");
                        name = GUILayout.TextField(name);
                        if (GUILayout.Button("Add"))
                        {
                            if (!AddChange(name, null, false))
                                notification = $"{name} Does Not Exist In {EventType}.";
                            else notification = string.Empty;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        if (!string.IsNullOrWhiteSpace(notification))
                            GUILayout.Label(notification);

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.BeginVertical();
                        foreach (ChangeData change in Changes.Values)
                        {
                            if (!change.RenderGUI())
                                toRemoveQueue = change.name;
                        }
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                }
                public class ChangeData
                {
                    public ChangeData() { }
                    public enum ValueType
                    {
                        String,
                        Int,
                        Double,
                        Float
                    }
                    public ChangeData(PropertyInfo prop, object value, bool relative)
                    {
                        this.prop = prop;
                        name = prop.Name;
                        this.value = value ?? "";
                        this.relative = relative;
                        valueType = value switch
                        {
                            int => ValueType.Int,
                            double => ValueType.Double,
                            float => ValueType.Float,
                            string => ValueType.String,
                            _ => ValueType.String
                        };
                    }
                    public ValueType valueType;
                    public bool change = true;
                    public string name;
                    public PropertyInfo prop;
                    public object value;
                    public bool relative;
                    public bool RenderGUI()
                    {
                        if (change = GUILayout.Toggle(change, $"<b>Change {name}</b>"))
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            GUILayout.BeginVertical();
                            {
                                relative = GUILayout.Toggle(relative, "Relative");
                                GUILayout.BeginHorizontal();
                                {
                                    GUILayout.Label($"To Change Value{(relative ? " Relative" : "")}:");
                                    string s = value.ToString();
                                    s = GUILayout.TextArea(s);
                                    switch (valueType)
                                    {
                                        case ValueType.String:
                                            value = s;
                                            break;
                                        case ValueType.Int:
                                            value = FastParser.ParseInt(s);
                                            break;
                                        case ValueType.Float:
                                            value = FastParser.ParseFloat(s);
                                            break;
                                        case ValueType.Double:
                                            value = FastParser.ParseDouble(s);
                                            break;
                                    }
                                }
                                GUILayout.FlexibleSpace();
                                GUILayout.EndHorizontal();
                            }

                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button("Remove"))
                                return false;
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();

                            GUILayout.EndVertical();
                            GUILayout.EndHorizontal();
                        }
                        return true;
                    }
                    public object GetCurValue(AdofaiMapConverter.Actions.Action action)
                        => prop.GetValue(action);
                    public void Change(AdofaiMapConverter.Actions.Action action)
                    {
                        if (!change) return;
                        var pType = prop.PropertyType;
                        if (pType == typeof(string))
                            prop.SetValue(action, value);
                        else
                        {
                            if (relative)
                            {
                                var pValue = prop.GetValue(action);
                                if (pType == typeof(int))
                                    prop.SetValue(action, CastToIntSafe(pValue) + CastToIntSafe(value));
                                if (pType == typeof(float))
                                    prop.SetValue(action, CastToFloatSafe(pValue) + CastToFloatSafe(value));
                                if (pType == typeof(double))
                                    prop.SetValue(action, CastToDoubleSafe(pValue) + CastToDoubleSafe(value));
                            }
                            else
                            {
                                if (pType == typeof(int))
                                    prop.SetValue(action, CastToIntSafe(value));
                                if (pType == typeof(float))
                                    prop.SetValue(action, CastToFloatSafe(value));
                                if (pType == typeof(double))
                                    prop.SetValue(action, CastToDoubleSafe(value));
                            }
                        }
                    }
                    static float CastToFloatSafe(object obj)
                        => (float)Convert.ChangeType(obj, typeof(float));
                    static int CastToIntSafe(object obj)
                        => (int)Convert.ChangeType(obj, typeof(int));
                    static double CastToDoubleSafe(object obj)
                        => (double)Convert.ChangeType(obj, typeof(double));
                }
            }
        }
    }
#if HAS_OPENCV
    [Tweak("Generators", Priority = 1)]
    [TweakGroup(Id = "_")]
    public class Generators : Tweak
    {
        [TweakGroup(Id = "Generators")]
        [Tweak("Image Generator", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class ImageGeneratorTweak : Generators
        {
            public bool Generated = false;
            public bool Generating = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevGeneratedPath;
            }
            public override void OnGUI()
            {
                Generated = Setting.PrevGeneratedPath == Main.Path;
                if (!Generated && !Generating) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Width:");
                Setting.Width = GUILayout.TextField(Setting.Width);
                GUILayout.Label("Height:");
                Setting.Height = GUILayout.TextField(Setting.Height);
                if (GUILayout.Button("Generate"))
                {
                    Notification = "Generating...";
                    GetGenerateThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        NormalConverters.OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetGenerateThread()
            {
                return new Thread(() =>
                {
                    try
                    {
                        Generating = true;
                        var file = new FileInfo(Main.Path);
                        int width = FastParser.ParseInt(Setting.Width);
                        int height = FastParser.ParseInt(Setting.Height);
                        var img = new Bitmap(Main.Path);
                        var result = ImageGenerator.Generate(img);
                        var resultStr = result.ToNode().ToString(4);
                        var lastIdx = file.Name.LastIndexOf('.');
                        var resultPath = $"{file.DirectoryName}/{file.Name.Remove(lastIdx, file.Name.Length - lastIdx)}.adofai";
                        File.WriteAllText(resultPath, resultStr);
                        Setting.PrevGeneratedPath = Main.Path;
                        Notification = "Generate Successful.";
                        Generating = false;
                        Generated = true;
                        ToOpenLevel = resultPath;
                    }
                    catch (Exception e) { Notification = $"Generate Failed. ({e.Message})"; Log(e); }
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevGeneratedPath = string.Empty;
                public string Width = "-1";
                public string Height = "-1";
            }
        }
        [TweakGroup(Id = "Generators")]
        [Tweak("Video Generator", SettingsType = typeof(Settings), SpacingSize = 0)]
        public class VideoGeneratorTweak : Generators
        {
            public bool Generated = false;
            public bool Generating = false;
            public string ToOpenLevel = null;
            public string Notification = string.Empty;
            [SyncSettings]
            public Settings Setting { get; set; }
            public override void OnEnable()
            {
                if (Main.Path.Length <= 0)
                    Main.Path = Setting.PrevGeneratedPath;
            }
            public override void OnGUI()
            {
                Generated = Setting.PrevGeneratedPath == Main.Path;
                if (!Generated && !Generating) Notification = string.Empty;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Width:");
                Setting.Width = GUILayout.TextField(Setting.Width);
                GUILayout.Label("Height:");
                Setting.Height = GUILayout.TextField(Setting.Height);
                GUILayout.Label("Second From:");
                Setting.From = GUILayout.TextField(Setting.From);
                GUILayout.Label("Second To:");
                Setting.To = GUILayout.TextField(Setting.To);
                if (GUILayout.Button("Generate"))
                {
                    Notification = "Generating...";
                    GetGenerateThread().Start();
                }
                GUILayout.Label(Notification);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            public override void OnUpdate()
            {
                if (ToOpenLevel != null)
                {
                    if (scnEditor.instance)
                        NormalConverters.OpenLevel(ToOpenLevel);
                    ToOpenLevel = null;
                }
            }
            public Thread GetGenerateThread()
            {
                return new Thread(() =>
                {
                    try
                    {
                        Generating = true;
                        var file = new FileInfo(Main.Path);
                        int width = FastParser.ParseInt(Setting.Width);
                        int height = FastParser.ParseInt(Setting.Height);
                        int from = FastParser.ParseInt(Setting.From);
                        int to = FastParser.ParseInt(Setting.To);
                        var result = VideoGenerator.Generate(new VideoCapture(file.FullName), width, height, from, to);
                        var resultStr = result.ToNode().ToString(4);
                        var lastIdx = file.Name.LastIndexOf('.');
                        var resultPath = $"{file.DirectoryName}/{file.Name.Remove(lastIdx, file.Name.Length - lastIdx)}.adofai";
                        File.WriteAllText(resultPath, resultStr);
                        Setting.PrevGeneratedPath = Main.Path;
                        Notification = "Generate Successful.";
                        Generating = false;
                        Generated = true;
                        ToOpenLevel = resultPath;
                    }
                    catch (Exception e) { Notification = $"Generate Failed. ({e.Message})"; Log(e); }
                });
            }
            public class Settings : TweakSettings
            {
                public string PrevGeneratedPath = string.Empty;
                public string Width = "-1";
                public string Height = "-1";
                public string From = "-1";
                public string To = "-1";
            }
        }
    }
#endif
}
