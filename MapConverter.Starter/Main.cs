using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static UnityModManagerNet.UnityModManager;

namespace MapConverter.Starter
{
    public static class Main
    {
        public static void Load(ModEntry modEntry)
        {
            var deps = Path.Combine(modEntry.Path, "Dependencies");
            var domain = AppDomain.CurrentDomain;
            foreach (var dep in Directory.GetFiles(deps, "*.dll"))
            {
                if (Path.GetFileName(dep) == "OpenCvSharpExtern.dll")
                    File.Copy(dep, "./OpenCvSharpExtern.dll", true);
                else domain.Load(File.ReadAllBytes(dep));
            }
            var modAss = domain.Load(File.ReadAllBytes(Path.Combine(modEntry.Path, "MapConverter.dll")));
            typeof(ModEntry).GetField("mAssembly", (BindingFlags)15420).SetValue(modEntry, modAss);
            modAss.GetType("MapConverter.Main").GetMethod("Load").Invoke(null, new object[] { modEntry });
        }
    }
}
