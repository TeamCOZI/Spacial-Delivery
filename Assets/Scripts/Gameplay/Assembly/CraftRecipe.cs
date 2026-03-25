using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CraftRecipe
{
    [Serializable]
    public class IngredientEntry
    {
        public string itemName;
        [Min(1)] public int amount = 1;
    }

    public List<IngredientEntry> ingredients = new List<IngredientEntry>();

    public bool IsEmpty => ingredients == null || ingredients.Count == 0;
}