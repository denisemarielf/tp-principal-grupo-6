using System.Collections.Generic;
using UnityEngine;

// Materiales de sangre que funcionan igual en editor y build (sin depender de shaders de URP).
public static class BloodMaterials
{
    private static Shader shader;
    private static readonly Dictionary<Color, Material> solids = new Dictionary<Color, Material>();

    private static Shader GetShader()
    {
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
        return shader;
    }

    // Material con textura (gotas, manchas) y alpha blend.
    public static Material Textured(Texture2D tex)
    {
        Material m = new Material(GetShader());
        m.mainTexture = tex;
        m.color = Color.white;
        return m;
    }

    // Color plano (gib, muñón). Se cachea por color.
    public static Material Solid(Color color)
    {
        if (solids.TryGetValue(color, out Material m) && m != null) return m;
        m = new Material(GetShader());
        m.color = color;
        solids[color] = m;
        return m;
    }
}