public static class GameplayRuntimeAccess
{
    public static bool TryGetPartDb(out PartDB partDb)
    {
        partDb = PartDB.Instance;
        return partDb != null;
    }

    public static bool TryGetAssembly(out Assembly assembly)
    {
        assembly = Assembly.Instance;
        return assembly != null;
    }

    public static bool TryGetAssemblyManager(out AssemblyManager assemblyManager)
    {
        assemblyManager = AssemblyManager.Instance;
        return assemblyManager != null;
    }

    public static bool TryGetAssemblyUi(out AssemblyUI assemblyUi)
    {
        assemblyUi = AssemblyUI.Instance;
        return assemblyUi != null;
    }
}
