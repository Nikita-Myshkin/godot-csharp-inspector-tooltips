#if TOOLS
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace CSharpInspectorTooltips.Editor;

[Tool]
public partial class CSharpDocumentationInspectorPlugin : EditorInspectorPlugin
{
    private readonly Dictionary<string, Dictionary<string, string>> _documentation = new();
    private readonly Dictionary<string, string> _currentTooltips = new();
    private readonly Dictionary<string, Type> _typesByClassName = new();
    private readonly string _xmlPath;
    private DateTime _lastWriteTimeUtc;

    public CSharpDocumentationInspectorPlugin()
    {
        string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        _xmlPath = ProjectSettings.GlobalizePath(
            $"res://.godot/mono/temp/bin/Debug/{assemblyName}.xml");
        ReloadDocumentationIfChanged();
    }

    public bool ReloadDocumentationIfChanged()
    {
        if (!File.Exists(_xmlPath))
        {
            return false;
        }

        DateTime writeTimeUtc = File.GetLastWriteTimeUtc(_xmlPath);
        if (writeTimeUtc == _lastWriteTimeUtc)
        {
            return false;
        }

        return TryLoadDocumentation(writeTimeUtc);
    }

    public override bool _CanHandle(GodotObject @object)
    {
        if (!TryGetScriptType(@object, out Type? scriptType) || scriptType == null)
        {
            return false;
        }

        foreach (Type type in WalkProjectTypes(scriptType))
        {
            if (_documentation.ContainsKey(type.FullName ?? string.Empty))
            {
                return true;
            }
        }

        return false;
    }

    public override bool _ParseProperty(
        GodotObject @object,
        Variant.Type type,
        string name,
        PropertyHint hintType,
        string hintString,
        PropertyUsageFlags usageFlags,
        bool wide)
    {
        if (TryGetDescription(@object, name, out string description))
        {
            _currentTooltips[NormalizeName(name)] = description;
        }

        return false;
    }

    public override void _ParseBegin(GodotObject @object)
    {
        _currentTooltips.Clear();
    }

    public override void _ParseEnd(GodotObject @object)
    {
        if (_currentTooltips.Count == 0)
        {
            return;
        }

        var tooltips = new Dictionary<string, string>(_currentTooltips);
        Callable.From(() =>
            Callable.From(() => ApplyTooltips(tooltips)).CallDeferred()
        ).CallDeferred();
    }

    private void ApplyTooltips(Dictionary<string, string> tooltips)
    {
        EditorInspector inspector = EditorInterface.Singleton.GetInspector();
        ApplyTooltipsRecursive(inspector, tooltips);
    }

    private static void ApplyTooltipsRecursive(
        Node node,
        Dictionary<string, string> tooltips)
    {
        if (node is EditorProperty editorProperty)
        {
            string propertyName = editorProperty.GetEditedProperty().ToString();

            if (tooltips.TryGetValue(NormalizeName(propertyName), out string? description))
            {
                SetTooltipRecursive(editorProperty, description);
            }
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyTooltipsRecursive(child, tooltips);
        }
    }

    private static void SetTooltipRecursive(Node node, string description)
    {
        if (node is Control control)
        {
            control.TooltipText = description;
        }

        foreach (Node child in node.GetChildren())
        {
            SetTooltipRecursive(child, description);
        }
    }

    private bool TryGetDescription(GodotObject @object, string propertyName, out string description)
    {
        description = string.Empty;

        if (!TryGetScriptType(@object, out Type? scriptType) || scriptType == null)
        {
            return false;
        }

        string normalizedProperty = NormalizeName(propertyName);

        foreach (Type type in WalkProjectTypes(scriptType))
        {
            if (_documentation.TryGetValue(type.FullName ?? string.Empty, out Dictionary<string, string>? properties) &&
                properties.TryGetValue(normalizedProperty, out string? foundDescription) &&
                foundDescription != null)
            {
                description = foundDescription;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves the C# type behind a Godot object's attached script.
    /// <para>
    /// This intentionally does NOT use <c>@object.GetType()</c>: a script without
    /// <c>[Tool]</c> only gets a real, fully-typed instance while the game is
    /// running. While just editing a scene in the editor, Godot instead gives it a
    /// placeholder instance typed as its native base class (e.g. <c>Node</c>), so
    /// <c>@object.GetType()</c> would not return the actual script class there.
    /// The script's file path is available in both cases, so the class is resolved
    /// from that instead, via <see cref="_typesByClassName"/>.
    /// </para>
    /// </summary>
    private bool TryGetScriptType(GodotObject @object, out Type? type)
    {
        type = null;
        return TryGetCSharpClassName(@object, out string className) &&
               _typesByClassName.TryGetValue(className, out type);
    }

    private static bool TryGetCSharpClassName(GodotObject @object, out string className)
    {
        className = string.Empty;
        Variant scriptVariant = @object.GetScript();

        if (scriptVariant.VariantType != Variant.Type.Object)
        {
            return false;
        }

        Script? script = scriptVariant.AsGodotObject() as Script;
        string scriptPath = script?.ResourcePath ?? string.Empty;

        if (!scriptPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        className = Path.GetFileNameWithoutExtension(scriptPath);
        return !string.IsNullOrWhiteSpace(className);
    }

    /// <summary>
    /// Rebuilds the class-name -> Type cache from every currently loaded assembly.
    /// Done once per XML documentation reload (i.e. once per C# build), not per
    /// Inspector property, so the cost of scanning assemblies is paid rarely.
    /// If two classes anywhere share a simple name, the first one found wins -
    /// documentation is still stored per fully-qualified name, so this only risks
    /// resolving the wrong Type for that rare collision, not mixing up their docs.
    /// </summary>
    private void RebuildTypeIndex()
    {
        _typesByClassName.Clear();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(t => t != null).ToArray()!;
            }
            catch (Exception)
            {
                continue;
            }

            foreach (Type type in types)
            {
                _typesByClassName.TryAdd(type.Name, type);
            }
        }
    }

    /// <summary>
    /// Walks <paramref name="scriptType"/> and its base classes, stopping once it
    /// reaches Godot's own engine types (Node, Resource, GodotObject, ...) since
    /// project documentation never lives there.
    /// </summary>
    private static IEnumerable<Type> WalkProjectTypes(Type scriptType)
    {
        Assembly engineAssembly = typeof(GodotObject).Assembly;

        for (Type? currentType = scriptType;
             currentType != null && currentType.Assembly != engineAssembly;
             currentType = currentType.BaseType)
        {
            yield return currentType;
        }
    }

    private bool TryLoadDocumentation(DateTime writeTimeUtc)
    {
        try
        {
            XDocument document = XDocument.Load(_xmlPath);
            var loadedDocumentation = new Dictionary<string, Dictionary<string, string>>();

            foreach (XElement member in document.Descendants("member"))
            {
                string? memberId = member.Attribute("name")?.Value;
                XElement? summary = member.Element("summary");

                if (string.IsNullOrWhiteSpace(memberId) || summary == null)
                {
                    continue;
                }

                if (!memberId.StartsWith("F:", StringComparison.Ordinal) &&
                    !memberId.StartsWith("P:", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TrySplitMemberId(memberId[2..], out string typeName, out string propertyName))
                {
                    continue;
                }

                string description = NormalizeDescription(summary.Value);
                if (description.Length == 0)
                {
                    continue;
                }

                if (!loadedDocumentation.TryGetValue(typeName, out Dictionary<string, string>? properties))
                {
                    properties = new Dictionary<string, string>();
                    loadedDocumentation[typeName] = properties;
                }

                properties[NormalizeName(propertyName)] = description;
            }

            _documentation.Clear();
            foreach ((string typeName, Dictionary<string, string> properties) in loadedDocumentation)
            {
                _documentation[typeName] = properties;
            }

            RebuildTypeIndex();
            _lastWriteTimeUtc = writeTimeUtc;
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError($"C# Inspector Tooltips: failed to read XML documentation: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Splits a "Namespace.Outer.Property" member id into its full, namespace-qualified
    /// type name and its property name. Keeping the namespace avoids mixing up two
    /// classes that share a simple name in different namespaces.
    /// </summary>
    private static bool TrySplitMemberId(
        string memberId,
        out string typeName,
        out string propertyName)
    {
        typeName = string.Empty;
        propertyName = string.Empty;

        int propertySeparator = memberId.LastIndexOf('.');
        if (propertySeparator <= 0 || propertySeparator == memberId.Length - 1)
        {
            return false;
        }

        propertyName = memberId[(propertySeparator + 1)..];
        typeName = memberId[..propertySeparator];

        return typeName.Length > 0 && propertyName.Length > 0;
    }

    private static string NormalizeDescription(string description)
    {
        string[] lines = description
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(NormalizeLine)
            .ToArray();

        int firstContentLine = Array.FindIndex(lines, line => line.Length > 0);
        if (firstContentLine < 0)
        {
            return string.Empty;
        }

        int lastContentLine = Array.FindLastIndex(lines, line => line.Length > 0);
        return string.Join("\n", lines[firstContentLine..(lastContentLine + 1)]);
    }

    private static string NormalizeLine(string line)
    {
        return string.Join(
            " ",
            line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string NormalizeName(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
#endif
