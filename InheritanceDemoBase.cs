using Godot;

namespace CSharpInspectorTooltips.Demo;

/// <summary>
/// Base class used to demonstrate inheritance support (see issue #1):
/// an exported property declared here should still show its tooltip when
/// inspected through a derived script such as <see cref="InheritanceDemoDerived"/>.
/// </summary>
public partial class InheritanceDemoBase : Node
{
    /// <summary>
    /// Declared on the base class. Hovering this property while the derived
    /// script is selected must still show this tooltip.
    /// </summary>
    [Export]
    public string BaseMessage { get; set; } = "Hover me: I'm inherited from the base class";
}
