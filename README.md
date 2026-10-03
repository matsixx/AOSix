# AOSix

A total replacement of Tarkov's ambient occlusion, formerly "AO Replacer" (which used Amplify Occlusion).
Works on flatscreen and in VR.

Support my work on Ko-fi: https://ko-fi.com/matsix

## Features

- **Visibility-bitmask GTAO:** ground-truth AO where occluders have a finite thickness (Therrien et al. 2023, the method behind Blender EEVEE-Next's AO). Plain GTAO and HBAO treat every occluder as infinitely thick, which darkens the ground behind grass, poles and your gun. AOSix doesn't.
- **Built on Intel's XeGTAO recipe:** a depth pyramid, pixel-snapped sampling, multi-bounce lighting and an edge-aware denoise.
- **Fixes Tarkov's sky ambient ignoring AO:** the game adds its sky ambient in a late pass that only lets AO touch reflections, so the ambient washed out any AO, vanilla's included. AOSix swaps in a faithful port of that pass with the ambient occluded.
- **Close-up detail:** a screen-radius cap keeps the AO on your gun and hands sampling their own shapes.
- **No temporal filtering,** so no ghosting.
- **VR:** each eye renders correctly. SPT-VR's built-in AO steps aside when AOSix is installed.

## Requirements

- SPT 4.1.

## Install

1. Extract the release into your SPT folder. You should end up with:
   ```
   BepInEx/plugins/AOSix/AOSix.dll
   BepInEx/plugins/AOSix/Assets/aosix
   ```
2. Launch the game.

### Upgrading from AO Replacer

AOSix is a separate plugin, not an update of the old one. **Delete `BepInEx/plugins/AOReplacer`** before
installing, or both mods will draw AO on top of each other. The old config file
`BepInEx/config/com.matsix.sptaoreplacer.cfg` can go too.

## Quality

Quality follows the game's own SSAO setting in the graphics menu:

| In-game SSAO | AOSix |
|---|---|
| Off | No AO |
| Fastest Performance | 1 direction, 4 steps |
| Fast Performance | 2 directions, 4 steps |
| High Quality | 2 directions, 6 steps |
| Highest Quality | 3 directions, 8 steps |
| Colored Highest Quality | Highest, plus bounce light tinted by surface colour |

## Settings

Open the in-game configuration manager (F12) or edit `BepInEx/config/com.matsix.aosix.cfg`.

| Setting | Default | What it does |
|---|---|---|
| Enabled | On | Off gives you the game's own AO back. |
| Intensity | 1.5 | AO strength. 1 is physically based. Higher darkens contact shadows more. |
| Radius | 1 | How far the occlusion reaches, in metres. |
| Thickness | 0.5 | How thick an occluder is assumed to be, in metres. Lower lets light pass behind thin things like grass, poles and railings. |
| Max Screen Radius | 50 | Cap on the radius as a percentage of screen height. Keeps close objects sampling their own features. |
| Min Step Pixels | 0 | Ignores depth steps smaller than this many pixels. 0 is off and the most accurate. |
| Max Distance | 500 | AO fades out toward this distance, in metres. |
| Occlude Custom Ambient | On | Applies the AO to the game's sky ambient. Off restores the game's original pass for comparison. |

The Debug section has an AO-only view.

## Compatibility

- **Hollywood FX/Graphics:** compatible, but its AO settings are ignored in favour of AOSix, because double AO looks bad.
- **SPT-VR:** supported, as described above.

## Building from source

```sh
dotnet build AOSix.csproj -c Release
```

The project references the game's DLLs from a local `libs/` folder that isn't in the repository. Copy these
into it from your SPT install:

- From `EscapeFromTarkov_Data/Managed`: `Assembly-CSharp.dll`, `Comfort.dll`, `Comfort.Unity.dll`, `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.AssetBundleModule.dll`
- From `BepInEx/core`: `0Harmony.dll`, `BepInEx.dll`
- From `BepInEx/plugins/spt`: `spt-reflection.dll`

The AO shaders ship compiled in the `aosix` bundle in each release. Their source isn't part of this repository.

## Credits

- Visibility bitmask: Olivier Therrien, Yannick Levesque and Guillaume Gilet, "Screen Space Indirect Lighting with Visibility Bitmask" (2023).
- GTAO: Jorge Jimenez et al., "Practical Real-Time Strategies for Accurate Indirect Occlusion" (2016).
- XeGTAO: Filip Strugar, Intel.

## License

The code in this repository is licensed under the GNU General Public License v3.0. See `LICENSE.txt`.
