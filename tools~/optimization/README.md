# Optimized release build

From this mod in `Assets/Mods`, run `tools~/optimization/build.ps1` (optional `-SdkRoot`). This is the release recipe for 1.0.7. It uses the prepared visual assets and exact runtime roots in `release.json`, so a legacy model import cannot silently restore discarded source geometry.

Requirements: the surrounding Big Ambitions SDK and its referenced library mods, installed game references configured in `UserSettings/BAModBuilder.ImportedDlls.json`, Unity 2022.3.62f2 with Mac support, Python with UnityPy and dnfile. The self-contained recipe scripts are included here. Large generated meshes, when applicable, are restored from the checked gzip archive and SHA-256 verified.

Builds run under the system temporary `BigAmbitions` folder. They check visual caches, traffic ownership, DLL versions and bundle roots/resources, then replace only this mod's `Output` folder while retaining the previous package. There is no game install or Steam upload. `package-audit.json` records actual built resources and hashes; the targeted check reports do not establish gameplay or FPS results.
