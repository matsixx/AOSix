**AOSix**

A total replacement of Tarkov's ambient occlusion (formerly "AO Replacer", which used Amplify Occlusion).

- **Visibility-bitmask GTAO** — ground-truth AO where occluders have a finite thickness (Therrien et al. 2023,
  the method behind Blender EEVEE-Next's AO), on Intel XeGTAO's production recipe: a depth MIP pyramid,
  pixel-snapped sampling, multi-bounce, and an edge-aware denoise. Plain GTAO/HBAO treat every occluder as
  infinitely thick, which darkens the ground behind grass, poles and your gun; this doesn't.
- **Fixes EFT's Custom Ambient ignoring AO.** EFT adds its sky ambient in a late pass that only lets AO
  touch reflections, so the diffuse ambient washed out any AO (vanilla's too). AOSix swaps in a faithful port
  of that pass with the ambient occluded. Toggle "Occlude Custom Ambient" to compare.
- No temporal filtering (no ghosting). Works flatscreen and in SPT-VR (per-eye; SPT-VR's built-in AO steps
  aside when AOSix is installed).

Quality follows the in-game SSAO setting (Off disables it; Colored Highest Quality tints the bounce light by
surface color). Intensity, radius, thickness and fade distance are in the F12 config menu.

To install, copy the `AOSix` folder (DLL + `Assets/aosix`) into `BepInEx/plugins`.

Compatible with Hollywood FX/Graphics, but its AO settings are ignored in favor of this one (double AO is bad).

Support my work on Ko-fi: https://ko-fi.com/matsix

**Building:** `dotnet build -c Release` (deploys to `F:\SPT4.1\BepInEx\plugins\AOSix`). The shaders in `Assets/`
go in the `aosix` AssetBundle (Unity project copy under `Assets/Volumetric clouds/`, bundle-labeled).
