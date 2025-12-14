using System.Collections.Generic;
using UnityEngine;

public enum planetClass { terrestrial, jovian }
public class Planet : MonoBehaviour
{
    public planetClass planetClass_ { get; set; }
    public float heat { get; set; }
    public float atm { get; set; }
    public List<GameObject> satellites { get; set; }
}