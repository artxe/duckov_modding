# Duckov mods

Each directory is a standalone C# Harmony mod for Escape from Duckov; the root README lists them. Mod-specific invariants live in the mod's own `CLAUDE.md`.

## Build

`dotnet build` in the mod directory; the `CopyAfterBuild` target copies the DLL and assets to `$(DuckovPath)\Mods\<AssemblyName>\` (`DuckovPath`: see README).

## Coding convention

- `small_snake_case` for helper methods, fields, parameters and locals.
- Unity/Harmony entry points keep the names the framework requires (`Awake`, `OnDestroy`, `Prefix`, `Postfix`).
- Harmony patch classes are named `Type__Method`.
- Reflection caches are `I_` + the original member name (`I_gunState`, `I_ProcessMousePosViaRecoil`).
- Never rename API members, named arguments, serialized/private game member strings, or Harmony special parameters (`__instance`, `__result`).
