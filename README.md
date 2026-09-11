# SwmarlyValheimSeedTotem

Valheim 1.0 port of the Seed Totem mod originally published as Nexus Mods mod 876.

The plugin keeps the original GUID (`marcopogo.SeedTotem`) and save keys so existing totems
and configuration files remain compatible. Builds run in GitHub Actions against the current
Steam Valheim dedicated-server branch and produce a Thunderstore-ready ZIP.

## Local build

Set `VALHEIM_INSTALL` and `BEPINEX_PATH`, then build `SeedTotem.csproj` with a .NET Framework
capable SDK. The GitHub Actions workflow is the supported reproducible build because it fetches
the current Valheim 1.0 assemblies and publicizes them through Jötunn's build task.
