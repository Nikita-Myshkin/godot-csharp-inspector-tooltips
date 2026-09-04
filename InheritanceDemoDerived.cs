using Godot;

namespace CSharpInspectorTooltips.Demo;

/// <summary>
/// Derived class used to demonstrate inheritance support (see issue #1).
/// This script does not redeclare <c>BaseMessage</c>, but its tooltip must
/// still appear because the plugin walks the C# inheritance chain up to
/// <see cref="InheritanceDemoBase"/>.
/// </summary>
public partial class InheritanceDemoDerived : InheritanceDemoBase
{
    /// <summary>
    /// Declared directly on the derived class.
    /// </summary>
    [Export]
    public string DerivedMessage { get; set; } = "Hover me: I'm declared on the derived class";
}
