# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Fixed

- Exported properties and fields inherited from a base C# script now get their tooltip too, instead of only ones declared directly on the attached script. ([#1](https://github.com/Nikita-Myshkin/godot-csharp-inspector-tooltips/issues/1))

### Changed

- Class lookup by script filename is now cached once per C# build instead of scanning every loaded assembly for every Inspector property.
- Documentation is now indexed by the full, namespace-qualified type name instead of the bare class name, reducing (but not fully eliminating - see README limitations) the chance of two same-named classes in different namespaces being mixed up.

## [1.0.0] - 2026-07-04

### Added

- C# XML `<summary>` tooltips for exported Inspector fields and properties.
- Automatic XML documentation reload after C# builds.
- Preservation of line breaks and blank lines.
