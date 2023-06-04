using System;
using UnityEngine;
using Tweaks;
using SFB;
using System.IO;
using static UnityModManagerNet.UnityModManager;
using HarmonyLib;

namespace MapConverter
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class CommentAttribute : Attribute
    {
        public string Comment { get; }
        public CommentAttribute(string comment) => Comment = comment;
    }
    [Comment("Secret Key Combo List:")]
    [Comment("ilconverter => Make IL Shape Converter On GUI")]
    [Comment("breaklimit => Break Planet Count Limits")]
    public static class Main
    {
        public static string Path = string.Empty;
        public static bool isiLActivated = false;
        public static bool isiL = false;
        public static ModEntry Mod;
        public static ModEntry.ModLogger Logger;
        public static KeyCombo iLCombo = new KeyCombo("ilconverter");
        public static void Load(ModEntry modEntry)
        {
#if HAS_OPENCV
            if (Application.platform == RuntimePlatform.OSXPlayer && !File.Exists("Mods/MapConverter/libOpenCvSharpExtern.dylib"))
            {
                FileInfo fi = new FileInfo("Mods/MapConverter/OpenCvSharpExtern/libOpenCvSharpExtern.dylib");
                fi.CopyTo("Mods/MapConverter/libOpenCvSharpExtern.dylib");
            }
            AppDomain.CurrentDomain.Load(File.ReadAllBytes("Mods/MapConverter/System.Buffers.dll"));
            AppDomain.CurrentDomain.Load(File.ReadAllBytes("Mods/MapConverter/System.Runtime.CompilerServices.Unsafe.dll"));
            AppDomain.CurrentDomain.Load(File.ReadAllBytes("Mods/MapConverter/System.Numerics.Vectors.dll"));
            AppDomain.CurrentDomain.Load(File.ReadAllBytes("Mods/MapConverter/System.Memory.dll"));
            AppDomain.CurrentDomain.Load(File.ReadAllBytes("Mods/MapConverter/OpenCvSharp.dll"));
            LoadAfter(modEntry);
#endif
            Mod = modEntry;
            Logger = modEntry.Logger;
            AppDomain.CurrentDomain.Load(File.ReadAllBytes("Mods/MapConverter/AdofaiMapConverter.dll"));
            modEntry.OnGUI = OnGUI;
            modEntry.OnUpdate = (m, dt) =>
            {
                if (iLCombo.Check())
                {
                    isiLActivated = true;
                    scrFlash.Flash(Color.white);
                    iLCombo.curIndex = 0;
                }
            };
            Runner.Run(modEntry);
        }
#if HAS_OPENCV
        static void LoadAfter(ModEntry modEntry)
        {
            OpenCvSharp.Internal.WindowsLibraryLoader.Instance.AdditionalPaths.Add("Mods/MapConverter/OpenCvSharpExtern");
        }
#endif
        public static void OnGUI(ModEntry modEntry)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Path:");
            Path = GUILayout.TextField(Path);
            if (GUILayout.Button("Select"))
                Path = StandaloneFileBrowser.OpenFilePanel("Select", Persistence.GetLastUsedFolder(), "*", false)[0];
            if (GUILayout.Button("Get Current Map Path"))
                Path = scnEditor.instance?.customLevel?.levelPath ?? Path;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (isiLActivated)
                DrawiLGUI();
        }
        static void DrawiLGUI()
        {
            GUILayout.BeginHorizontal();
            isiL = GUILayout.Toggle(isiL, "iL Shape Converter");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            if (isiL && GUILayout.Button("Convert"))
                NormalConverters.iLConvert();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
