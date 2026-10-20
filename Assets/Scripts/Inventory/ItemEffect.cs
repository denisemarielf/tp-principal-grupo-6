using UnityEngine;

public abstract class ItemEffect : ScriptableObject
{
    /// <summary>Solo servidor. Devuelve false si no se pudo aplicar (no se consume).</summary>
    public abstract bool Apply(GameObject user);
}