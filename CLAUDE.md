# Duckov mods

Each directory is a standalone C# Harmony mod for Escape from Duckov; the root README lists them. Mod-specific invariants live in the mod's own `CLAUDE.md`.

## Build

`dotnet build` in the mod directory. The `CopyAfterBuild` target of each .csproj copies the DLL and assets to `$(DuckovPath)\Mods\<AssemblyName>\`. `DuckovPath` comes from the git-ignored `local.props` at the root (see README), else the default Steam path in `Directory.Build.props`.

## Coding convention

- `small_snake_case` for helper methods, fields, parameters and locals.
- Unity/Harmony entry points keep the names the framework requires (`Awake`, `OnDestroy`, `Prefix`, `Postfix`).
- Harmony patch classes are named `Type__Method`.
- Reflection caches are `I_` + the original member name (`I_gunState`, `I_ProcessMousePosViaRecoil`).
- Never rename API members, named arguments, serialized/private game member strings, or Harmony special parameters (`__instance`, `__result`).
