#if NETCOREAPP
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using Stardust;

/// <summary>启动钩子</summary>
internal class StartupHook
{
    /// <summary>被 DOTNET_STARTUP_HOOKS 调用的初始化方法</summary>
    public static void Initialize()
    {
        var env = Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS");
        var hooks = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? env?.Split(';') : env?.Split(':');
        if (hooks == null || hooks.Length == 0) return;

        var file = hooks.FirstOrDefault(e => e.EndsWith("Stardust.dll", StringComparison.OrdinalIgnoreCase));
        if (String.IsNullOrEmpty(file) || !File.Exists(file)) return;

        var dir = Path.GetDirectoryName(file)!;

        var context = new MyAssemblyLoadContext();
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var assemblyPath = Path.Combine(dir, name.Name + ".dll");
            if (!File.Exists(assemblyPath)) return null;

            try
            {
                var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
                if (name.Version == assemblyName.Version)
                {
                    var keyToken = name.GetPublicKeyToken();
                    var assemblyKeyToken = assemblyName.GetPublicKeyToken();
                    if (keyToken!.SequenceEqual(assemblyKeyToken!))
                    {
                        return name.Name!.StartsWith("NewLife", StringComparison.Ordinal)
                            ? AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath)
                            : context.LoadFromAssemblyPath(assemblyPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return null;
        };

        var type = typeof(StartupHook);
        type.InvokeMember(nameof(Init), BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic, null, null, [dir]);
    }

    private static void Init(String dir)
    {
        var hasNewLife = Directory.GetFiles(Environment.CurrentDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Any(e => e.StartsWith("NewLife", StringComparison.OrdinalIgnoreCase));
        if (!hasNewLife)
        {
            var name = Assembly.GetEntryAssembly()?.GetName().Name;
            Environment.SetEnvironmentVariable("BASEPATH", Path.Combine(dir, name!));

            Runtime.CreateConfigOnMissing = false;
        }

#if DEBUG
        XTrace.UseConsole();
#endif

        var runtimeConfig = Directory.GetFiles(Environment.CurrentDirectory, "*.runtimeconfig.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (!runtimeConfig.IsNullOrEmpty() && File.ReadAllText(runtimeConfig).Contains("AspNetCore"))
        {
            var ext = Path.Combine(dir, "Stardust.Extensions.dll");
            if (File.Exists(ext))
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(ext);
                if (asm != null)
                {
                    var existingAssemblies = Environment.GetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES");
                    var name = asm.GetName().Name;
                    if (existingAssemblies.IsNullOrEmpty())
                        existingAssemblies = name;
                    else if (!existingAssemblies.Contains(name))
                        existingAssemblies = existingAssemblies + ";" + name;
                    Environment.SetEnvironmentVariable("ASPNETCORE_HOSTINGSTARTUPASSEMBLIES", existingAssemblies);

                    return;
                }
            }
        }

        var services = ObjectContainer.Current;
        services.AddStardust();
    }

    internal class MyAssemblyLoadContext : AssemblyLoadContext
    {
        protected override Assembly Load(AssemblyName assemblyName) => null!;
    }
}

#endif