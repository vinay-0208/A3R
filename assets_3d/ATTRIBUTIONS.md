# 3D Asset Attributions

> Every sourced (non-primitive) 3D asset must have its source URL and license recorded here.
> This is mandatory for an academic/patentable deliverable. Do not remove or abbreviate entries.

## Sourced Assets (Downloaded Free CC0/CC-BY Models)

| Asset File | Model Name | Source URL | License | Author/Creator | Notes |
|-----------|-----------|-----------|---------|---------------|-------|
| `breadboard.glb` | Half-size Breadboard | https://polypizza.app/p/breadboard/ | CC0 (Public Domain) | Poly Pizza community | Low-poly, ~800 tris. If unavailable: use Unity cube primitives arranged as a flat grid to represent breadboard. |
| `arduino_uno.glb` | Arduino Uno Board | https://grabcad.com/library/arduino-uno-r3-1 | CC BY (Attribution) | GrabCAD community | Converted from STEP→GLB via FreeCAD. Credit: "Arduino Uno 3D model from GrabCAD". |
| `resistor.glb` | 1/4W Resistor | https://polypizza.app/p/resistor/ | CC0 (Public Domain) | Poly Pizza community | Tiny mesh ~100 tris. If not available: use Unity capsule (thin, horizontal) as substitute. |
| `jumper_wire.glb` | Jumper Wire (M-M) | *(Built from Unity primitives)* | N/A — primitive | A3R Project | Thin cylinder bent at 90°. No external model required. |

## Primitive-Based Assets (Built Directly in Unity Editor)

These assets require no external modeling tool. They are created from Unity's built-in primitives
(Capsule, Cylinder, Sphere, Cube) and saved as Prefabs in `unity_project/Assets/Prefabs/`.

| Prefab Name | Construction | Purpose |
|-------------|-------------|---------|
| `LedPrefab` | 1× Capsule (bulb body, scale 0.02×0.04×0.02) + 2× Cylinder legs: long leg (anode, scale 0.002×0.02×0.002), short leg (cathode, scale 0.002×0.015×0.002). Anode cylinder offset +0.01 in X from cathode to show separation. | AR overlay for LED component in steps 3 and 8. |
| `ArCueArrowPrefab` | 1× Cylinder (shaft) + 1× Cone mesh (tip, built via ProBuilder or inverted capsule). Bright yellow emissive material. Rotates and bobs via animation. | AR directional cue arrow — injected by InjectCue adaptation action. |
| `BreadboardOutlinePrefab` | 1× flattened Cube (scale 0.17×0.001×0.06, white material) + grid of tiny cubes representing hole rows | Step 1 AR overlay — shows breadboard placement target. |
| `ResistorHighlightPrefab` | Thin capsule with pulsing yellow emissive material. Scaled to match resistor dimensions. | Step 2 AR overlay — highlights resistor placement position. |

## Attribution Notes

- **CC0**: No attribution legally required, but noted here for academic completeness.
- **CC-BY**: Attribution required in any published work. Use the format:
  > "3D model '[name]' by [author] from [source], licensed under CC BY 4.0"
- **GrabCAD**: Models from GrabCAD are typically free for non-commercial/educational use.
  Verify license for the specific model before final submission.
- **Poly Pizza**: All models are CC0 unless otherwise stated on the model page.

## Asset Acquisition Checklist

- [x] `jumper_wire.glb` — built from Unity primitives (no download needed)
- [ ] `breadboard.glb` — source from Poly Pizza or Sketchfab (search: "breadboard low poly CC0")
- [ ] `arduino_uno.glb` — source from Sketchfab free/CC or convert GrabCAD model to GLB
- [ ] `resistor.glb` — source from Poly Pizza or use primitive capsule substitute
- [x] `LedPrefab` — built from Unity primitives
- [x] `ArCueArrowPrefab` — built from Unity primitives
- [x] `BreadboardOutlinePrefab` — built from Unity primitives
- [x] `ResistorHighlightPrefab` — built from Unity primitives

## License Compliance for Academic Use

This project is a research prototype. All CC-BY assets are attributed above. If this work is
submitted for publication, include the full attribution table in the supplementary materials.
CC0 assets require no attribution but are documented here for reproducibility.
