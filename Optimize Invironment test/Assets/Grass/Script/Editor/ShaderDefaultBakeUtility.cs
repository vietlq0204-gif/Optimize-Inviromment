using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes a material's current values back into its shader defaults.
/// </summary>
public static class ShaderDefaultBakeUtility
{
    private const string MenuItemPath = "Tools/Shaders/Bake Selected Material Values To Shader Defaults";

    private static readonly Regex PropertyLineRegex = new(
        "^(?<indent>\\s*)(?<attributes>(?:\\[[^\\]]+\\]\\s*)*)(?<name>[_A-Za-z0-9]+)\\s*\\(\\s*\"(?<display>[^\"]*)\"\\s*,\\s*(?<type>Range\\s*\\([^\\)]*\\)|Int|Float|Color|Vector|2DArray|2D|3D|Cube|CubeArray|Any)\\s*\\)\\s*=\\s*(?<default>.+?)\\s*$",
        RegexOptions.Compiled);

    [MenuItem(MenuItemPath)]
    private static void BakeSelectedMaterialMenu()
    {
        Material material = Selection.activeObject as Material;
        BakeMaterialWithDialogs(material);
    }

    [MenuItem(MenuItemPath, true)]
    private static bool ValidateBakeSelectedMaterialMenu()
    {
        return Selection.activeObject is Material material && CanBake(material, out _);
    }

    /// <summary>
    /// Attempts to bake the material with editor dialogs for confirmation and result.
    /// </summary>
    /// <param name="material">Material to bake.</param>
    public static void BakeMaterialWithDialogs(Material material)
    {
        if (!CanBake(material, out string reason))
        {
            EditorUtility.DisplayDialog("Bake Shader Defaults", reason, "OK");
            return;
        }

        string shaderPath = AssetDatabase.GetAssetPath(material.shader);
        string message =
            $"Material: {material.name}\nShader: {material.shader.name}\nPath: {shaderPath}\n\n" +
            "This will overwrite numeric/color/vector defaults in the shader file and sync texture defaults via ShaderImporter.\n\n" +
            "Texture scale/offset and other material-only data cannot be baked into shader defaults.";

        bool confirmed = EditorUtility.DisplayDialog(
            "Bake Shader Defaults",
            message,
            "Bake",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        BakeResult result = BakeMaterial(material);
        string title = result.Success ? "Bake Completed" : "Bake Failed";
        EditorUtility.DisplayDialog(title, result.Message, "OK");
    }

    /// <summary>
    /// Returns whether the material can be baked into its shader defaults.
    /// </summary>
    /// <param name="material">Candidate material.</param>
    /// <param name="reason">Failure reason if unsupported.</param>
    /// <returns>True when baking is supported.</returns>
    public static bool CanBake(Material material, out string reason)
    {
        if (material == null)
        {
            reason = "Select exactly one Material asset first.";
            return false;
        }

        Shader shader = material.shader;
        if (shader == null)
        {
            reason = "The selected material has no shader.";
            return false;
        }

        string shaderPath = AssetDatabase.GetAssetPath(shader);
        if (string.IsNullOrEmpty(shaderPath))
        {
            reason = "Could not resolve the shader asset path.";
            return false;
        }

        if (!shaderPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
        {
            reason = "This tool currently supports text-based .shader assets only.";
            return false;
        }

        string fullPath = Path.GetFullPath(shaderPath);
        if (!File.Exists(fullPath))
        {
            reason = $"Shader file was not found on disk:\n{fullPath}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Bakes the material values into the shader asset and importer defaults.
    /// </summary>
    /// <param name="material">Material to bake.</param>
    /// <returns>Operation result.</returns>
    public static BakeResult BakeMaterial(Material material)
    {
        if (!CanBake(material, out string reason))
        {
            return BakeResult.Failure(reason);
        }

        try
        {
            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            string fullPath = Path.GetFullPath(shaderPath);
            string shaderSource = File.ReadAllText(fullPath);

            if (!TryFindPropertiesBlock(shaderSource, out int propertiesStart, out int propertiesEnd))
            {
                return BakeResult.Failure("Could not find a valid Properties block in the shader file.");
            }

            string propertiesBlock = shaderSource.Substring(propertiesStart, propertiesEnd - propertiesStart);
            string updatedPropertiesBlock = BakePropertiesBlock(material, propertiesBlock, out BakeSummary summary);

            bool shaderFileChanged = !string.Equals(propertiesBlock, updatedPropertiesBlock, StringComparison.Ordinal);
            if (shaderFileChanged)
            {
                string updatedShaderSource =
                    shaderSource.Substring(0, propertiesStart) +
                    updatedPropertiesBlock +
                    shaderSource.Substring(propertiesEnd);

                File.WriteAllText(fullPath, updatedShaderSource);
                AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceSynchronousImport);
            }

            bool textureDefaultsChanged = ApplyTextureDefaults(shaderPath, summary.TexturePropertyNames, summary.TextureDefaults);
            AssetDatabase.SaveAssets();

            string status = BuildResultMessage(material, summary, shaderFileChanged, textureDefaultsChanged);
            return BakeResult.Successful(status);
        }
        catch (Exception exception)
        {
            return BakeResult.Failure($"Bake failed with exception:\n{exception.Message}");
        }
    }

    private static string BakePropertiesBlock(Material material, string propertiesBlock, out BakeSummary summary)
    {
        string lineEnding = propertiesBlock.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string[] lines = propertiesBlock.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        summary = new BakeSummary();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            Match match = PropertyLineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string propertyName = match.Groups["name"].Value;
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            string propertyType = match.Groups["type"].Value.Trim();
            string updatedDefault = GetFormattedDefaultValue(material, propertyName, propertyType, out Texture texture);
            if (updatedDefault == null)
            {
                continue;
            }

            if (IsTextureProperty(propertyType))
            {
                summary.TexturePropertyNames.Add(propertyName);
                summary.TextureDefaults.Add(texture);
                continue;
            }

            string rebuiltLine =
                $"{match.Groups["indent"].Value}{match.Groups["attributes"].Value}{propertyName} " +
                $"(\"{match.Groups["display"].Value}\", {propertyType}) = {updatedDefault}";

            if (!string.Equals(line, rebuiltLine, StringComparison.Ordinal))
            {
                lines[i] = rebuiltLine;
                summary.NumericPropertyCount++;
            }
        }

        return string.Join(lineEnding, lines);
    }

    private static string GetFormattedDefaultValue(
        Material material,
        string propertyName,
        string propertyType,
        out Texture texture)
    {
        texture = null;

        if (propertyType.StartsWith("Range", StringComparison.Ordinal) ||
            string.Equals(propertyType, "Float", StringComparison.Ordinal))
        {
            return FormatFloat(material.GetFloat(propertyName));
        }

        if (string.Equals(propertyType, "Int", StringComparison.Ordinal))
        {
            int intValue = Mathf.RoundToInt(material.GetFloat(propertyName));
            return intValue.ToString(CultureInfo.InvariantCulture);
        }

        if (string.Equals(propertyType, "Color", StringComparison.Ordinal))
        {
            Color color = material.GetColor(propertyName);
            return FormatVector4(color);
        }

        if (string.Equals(propertyType, "Vector", StringComparison.Ordinal))
        {
            Vector4 vector = material.GetVector(propertyName);
            return FormatVector4(vector);
        }

        if (IsTextureProperty(propertyType))
        {
            texture = material.GetTexture(propertyName);
            return string.Empty;
        }

        return null;
    }

    private static bool ApplyTextureDefaults(string shaderPath, IReadOnlyList<string> propertyNames, IReadOnlyList<Texture> textures)
    {
        if (propertyNames.Count == 0)
        {
            return false;
        }

        ShaderImporter importer = AssetImporter.GetAtPath(shaderPath) as ShaderImporter;
        if (importer == null)
        {
            return false;
        }

        importer.SetDefaultTextures(ToArray(propertyNames), ToArray(textures));
        importer.SaveAndReimport();
        return true;
    }

    private static string[] ToArray(IReadOnlyList<string> items)
    {
        string[] array = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            array[i] = items[i];
        }

        return array;
    }

    private static Texture[] ToArray(IReadOnlyList<Texture> items)
    {
        Texture[] array = new Texture[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            array[i] = items[i];
        }

        return array;
    }

    private static bool TryFindPropertiesBlock(string shaderSource, out int blockStart, out int blockEnd)
    {
        blockStart = -1;
        blockEnd = -1;

        int propertiesIndex = shaderSource.IndexOf("Properties", StringComparison.Ordinal);
        if (propertiesIndex < 0)
        {
            return false;
        }

        int openBraceIndex = shaderSource.IndexOf('{', propertiesIndex);
        if (openBraceIndex < 0)
        {
            return false;
        }

        int depth = 0;
        for (int i = openBraceIndex; i < shaderSource.Length; i++)
        {
            char c = shaderSource[i];
            if (c == '{')
            {
                depth++;
                if (depth == 1)
                {
                    blockStart = i + 1;
                }
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    blockEnd = i;
                    return true;
                }
            }
        }

        blockStart = -1;
        blockEnd = -1;
        return false;
    }

    private static bool IsTextureProperty(string propertyType)
    {
        return string.Equals(propertyType, "2D", StringComparison.Ordinal) ||
               string.Equals(propertyType, "2DArray", StringComparison.Ordinal) ||
               string.Equals(propertyType, "3D", StringComparison.Ordinal) ||
               string.Equals(propertyType, "Cube", StringComparison.Ordinal) ||
               string.Equals(propertyType, "CubeArray", StringComparison.Ordinal) ||
               string.Equals(propertyType, "Any", StringComparison.Ordinal);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static string FormatVector4(Vector4 value)
    {
        return $"({FormatFloat(value.x)},{FormatFloat(value.y)},{FormatFloat(value.z)},{FormatFloat(value.w)})";
    }

    private static string BuildResultMessage(
        Material material,
        BakeSummary summary,
        bool shaderFileChanged,
        bool textureDefaultsChanged)
    {
        string shaderName = material.shader != null ? material.shader.name : "<missing>";
        string status =
            $"Material '{material.name}' was baked into shader '{shaderName}'.\n\n" +
            $"- Numeric/Color/Vector defaults updated: {summary.NumericPropertyCount}\n" +
            $"- Texture defaults synced via ShaderImporter: {summary.TexturePropertyNames.Count}\n" +
            $"- Shader file changed: {(shaderFileChanged ? "Yes" : "No")}\n" +
            $"- Shader importer changed: {(textureDefaultsChanged ? "Yes" : "No")}\n\n" +
            "Not baked: texture scale/offset, keywords, render queue, and other material-only state.";

        return status;
    }

    private sealed class BakeSummary
    {
        public int NumericPropertyCount;
        public readonly List<string> TexturePropertyNames = new();
        public readonly List<Texture> TextureDefaults = new();
    }

    /// <summary>
    /// Result object for bake operations.
    /// </summary>
    public readonly struct BakeResult
    {
        private BakeResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        public string Message { get; }

        public static BakeResult Failure(string message)
        {
            return new BakeResult(false, message);
        }

        public static BakeResult Successful(string message)
        {
            return new BakeResult(true, message);
        }
    }
}
