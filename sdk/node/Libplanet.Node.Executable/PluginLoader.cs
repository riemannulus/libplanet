using System.Reflection;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain.Policies;

namespace Libplanet.Node.API;

public static class PluginLoader
{
    public static IActionLoader LoadActionLoader(string relativePath, string typeName)
    {
        Assembly assembly = LoadPlugin(relativePath);
        IEnumerable<IActionLoader> loaders = Create<IActionLoader>(assembly);
        foreach (IActionLoader loader in loaders)
        {
            if (loader.GetType().FullName == typeName)
            {
                return loader;
            }
        }

        throw new ApplicationException(
            $"Can't find {typeName} in {assembly} from {assembly.Location}. " +
            $"Available types: {string
                .Join(",", loaders.Select(x => x.GetType().FullName))}");
    }

    public static IPolicyActionsRegistry LoadPolicyActionRegistry(
        string relativePath,
        string typeName)
    {
        Assembly assembly = LoadPlugin(relativePath);
        IEnumerable<IPolicyActionsRegistry> policies = Create<IPolicyActionsRegistry>(assembly);
        foreach (IPolicyActionsRegistry policy in policies)
        {
            if (policy.GetType().FullName == typeName)
            {
                return policy;
            }
        }

        throw new ApplicationException(
            $"Can't find {typeName} in {assembly} from {assembly.Location}. " +
            $"Available types: {string
                .Join(",", policies.Select(x => x.GetType().FullName))}");
    }

    private static IEnumerable<T> Create<T>(Assembly assembly)
        where T : class
    {
        int count = 0;

        foreach (Type type in assembly.GetTypes())
        {
            if (typeof(T).IsAssignableFrom(type))
            {
                T result = Activator.CreateInstance(type) as T;
                if (result != null)
                {
                    count++;
                    yield return result;
                }
            }
        }

        if (count == 0)
        {
            string availableTypes = string.Join(",", assembly.GetTypes().Select(t => t.FullName));
            throw new ApplicationException(
                $"Can't find any type which implements ICommand in {assembly} from {assembly.Location}.\n" +
                $"Available types: {availableTypes}");
        }
    }

    private static Assembly LoadPlugin(string relativePath)
    {
        // Navigate up to the solution root
        string root = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(
                Path.GetDirectoryName(
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            Path.GetDirectoryName(typeof(Program).Assembly.Location)))))));

        string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
        Console.WriteLine($"Loading commands from: {pluginLocation}");
        PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
        return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
    }
}
