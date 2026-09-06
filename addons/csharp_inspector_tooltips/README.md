# C# Inspector Tooltips for Godot

An editor plugin that displays C# XML `<summary>` comments as tooltips for exported properties in the Godot Inspector.

## Features

- Uses standard C# XML documentation comments.
- Supports exported fields and properties.
- Supports properties inherited from a base class.
- Preserves line breaks and blank lines.
- Reloads documentation automatically after a C# build.
- Runs only in the editor and does not affect game performance.

## Requirements

- Godot 4.7 with .NET support.
- .NET 8.

Only Godot 4.7 has been tested so far.

## Installation

1. Copy `addons/csharp_inspector_tooltips` into your Godot project.
2. Enable XML documentation generation in your `.csproj`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

`1591` is suppressed because enabling XML documentation otherwise warns about every public member without documentation.

3. Build the C# project.
4. Open **Project > Project Settings > Plugins**.
5. Enable **C# Inspector Tooltips**.

## Usage

Add a standard XML `<summary>` directly above an exported field or property:

```csharp
/// <summary>
/// Location loaded immediately when the game starts in the editor.
/// Skips the main menu and is intended for quick testing.
///
/// Leave empty to use the normal main menu flow.
/// </summary>
[Export]
public Resource? DebugStartLocation { get; set; }
```

Build the C# project, select the object in the Scene dock, and hover over `Debug Start Location` in the Inspector.

After later documentation changes, build again. The plugin detects the updated XML file and refreshes the current Inspector automatically.

## Writing Useful Tooltips

A useful description should explain:

1. What the field controls.
2. When or where it is used.
3. What happens when it is empty or keeps its default value.
4. Any important setup rule or limitation.

Prefer two to four short lines. Avoid repeating the field name or its type.

## How It Works

The C# compiler writes `<summary>` comments to the generated XML documentation file, keyed by each member's full, namespace-qualified name. Whenever that file changes (i.e. after a build), the plugin also scans loaded assemblies once to map each class's file name to its actual C# `Type`. For an inspected object, it reads the attached script's file name, looks up that `Type`, and walks up its base classes to find documented members - which is what makes tooltips work for properties declared on a parent script - before assigning the text to the existing Inspector controls.

The lookup is based on the script's file name rather than `@object.GetType()`: an object whose script is not marked `[Tool]` only gets a real, fully-typed instance while the game is running; while just editing a scene in the editor it gets a placeholder instance typed as its native base class, so `@object.GetType()` would not give the actual script class there.

The XML file is checked once per second inside the editor. The plugin code is wrapped in `#if TOOLS`, so it is excluded from exported games.

## Current Limitations

- Only Godot 4.7 .NET has been tested.
- A C# build is required after changing documentation.
- The C# class name should match its script filename.
- Documentation is read from the editor Debug or Release build output, whichever is newer; other build configurations are not checked.
- `<summary>` is supported; other XML elements such as `<remarks>` are not yet processed separately. Reference tags with no text of their own (e.g. `<see cref="Other"/>`, `<paramref name="value"/>`) show their referenced name instead of disappearing.
- If two classes anywhere in the project share the same simple name (in different namespaces), the wrong one may be resolved for tooltip lookup even though each keeps its own documentation internally.

## Troubleshooting

### The plugin is not listed

Make sure this file exists:

```text
addons/csharp_inspector_tooltips/plugin.cfg
```

Then build the C# project and restart the editor if necessary.

### A tooltip is missing

- Confirm `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is present in the `.csproj`.
- Build the C# project.
- Confirm the comment uses `<summary>` and is directly above an `[Export]` field or property, either on the script itself or on one of its base classes.
- Confirm the script filename matches the C# class name.

## License

MIT License. See [LICENSE](LICENSE).

## Contributing

Bug reports and focused pull requests are welcome. Please include the Godot version, operating system, and a minimal reproduction when reporting an issue.
