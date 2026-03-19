using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class StructureCatalog
{
    private static List<Structure> structures;
    private static Dictionary<string, Structure> structuresByName;

    public static List<Structure> GetAllStructures()
    {
        EnsureLoaded();
        return structures;
    }

    public static Structure GetStructureByName(string structureName)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(structureName)) return null;

        structuresByName.TryGetValue(structureName, out Structure structure);
        return structure;
    }

    private static void EnsureLoaded()
    {
        if (structures != null && structuresByName != null) return;

        structures = Resources.LoadAll<Structure>("Structures")
            .OrderBy(structure => structure != null ? structure.structureName : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        structuresByName = new Dictionary<string, Structure>(StringComparer.OrdinalIgnoreCase);
        foreach (Structure structure in structures)
        {
            if (structure == null || string.IsNullOrWhiteSpace(structure.structureName)) continue;
            if (!structuresByName.ContainsKey(structure.structureName))
            {
                structuresByName.Add(structure.structureName, structure);
            }
        }
    }
}
