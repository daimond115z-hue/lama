using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using WolfLive.Api;
using WolfLive.Api.Commands;

Console.WriteLine($"WolfClient type: {typeof(WolfClient).FullName}");
Console.WriteLine("WolfClient methods:");
foreach (var m in typeof(WolfClient).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(x => x.Name))
{
    Console.WriteLine($"{m.Name} : {m}");
}

var iface = typeof(WolfClient).GetInterfaces().FirstOrDefault(i => i.Name.Contains("Wolf"));
Console.WriteLine($"WolfClient interface: {iface}");
if (iface != null)
{
    foreach (var m in iface.GetMethods().OrderBy(x => x.Name))
    {
        Console.WriteLine($"I {m.Name} : {m}");
    }
}

Console.WriteLine("Extension methods on IWolfClient/WolfClient:");
var assemblies = AppDomain.CurrentDomain.GetAssemblies();
foreach (var asm in assemblies.OrderBy(a => a.FullName))
{
    foreach (var type in asm.GetTypes().Where(t => t.IsSealed && t.IsAbstract))
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var attr = method.GetCustomAttribute<ExtensionAttribute>();
            if (attr == null) continue;
            if (method.GetParameters().Length == 0) continue;
            var first = method.GetParameters()[0].ParameterType;
            if (first == typeof(IWolfClient) || first == typeof(WolfClient))
            {
                Console.WriteLine($"{type.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            }
        }
    }
}

