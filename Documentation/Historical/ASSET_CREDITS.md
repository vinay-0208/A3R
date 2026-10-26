# A³R 3D Asset Credits & Provenance Catalog

**Document Version:** 1.0.0  
**Audit Scope:** `assets_3d/sourced/`, `unity_project/Assets/Prefabs/`, `unity_project/Assets/Scripts/AR/`  
**Status:** PROVISIONAL PLACEHOLDER CATALOG — PROCEDURAL PRIMITIVES ACTIVE AT RUNTIME

---

## 1. Executive Summary

This document establishes the official provenance, runtime usage status, and licensing compliance requirements for all 3D assets in the A³R repository.

Key conclusions:
1. **Procedural Geometry at Runtime**: The active Unity runtime scene (`A3R_Main.unity`) does **not** load or depend on external binary GLTF/GLB models. All directional cue arrows, target rings, and component highlight meshes are dynamically constructed in C# via `ProceduralMeshUtility.cs` using standard parametric primitives.
2. **Placeholder GLB Files**: The four files located in `assets_3d/sourced/` are minimal placeholder stubs (each $\sim 664–672\text{ bytes}$) retained from early conceptual planning. They are not bundled into the Android APK build and are not loaded by any runtime component.
3. **Distribution Disclaimer**: Source and license provenance for external assets remains subject to verification prior to any public or commercial release.

---

## 2. Inventory of `assets_3d/sourced/`

| File Name | Size (Bytes) | Intended Object | File Type / Nature | Runtime Dependency | Provenance / Licensing Status |
|---|---|---|---|---|---|
| `arduino_uno.glb` | 672 bytes | Microcontroller Board | Minimal GLTF header / placeholder stub | None (not referenced in scene) | Source/license provenance not yet established; attribution must be verified before external/public distribution. |
| `breadboard.glb` | 668 bytes | Half-Size Breadboard | Minimal GLTF header / placeholder stub | None (not referenced in scene) | Source/license provenance not yet established; attribution must be verified before external/public distribution. |
| `jumper_wire.glb` | 672 bytes | Jumper Wire | Minimal GLTF header / placeholder stub | None (not referenced in scene) | Built from procedural primitive specification; external provenance not yet established. |
| `resistor.glb` | 664 bytes | 1/4W Resistor | Minimal GLTF header / placeholder stub | None (not referenced in scene) | Source/license provenance not yet established; attribution must be verified before external/public distribution. |

> [!IMPORTANT]
> **License Compliance Disclaimer**:  
> *Source/license provenance not yet established; attribution must be verified before external/public distribution.*  
> No proprietary or restricted 3D geometry is included in the application bundle or runtime execution.

---

## 3. Runtime & Preview Visual Presentation Architecture

All visual guidance elements and physical preview models in the A³R application are generated procedurally at runtime and during offline preview construction (`ProceduralMeshUtility.cs`, `PreviewSceneBuilder.cs`) and managed by `ARGuidanceController.cs`:

1. **Modern AR Navigation Path (`A3R_ChevronPath.cs`)**:
   - **Chevrons**: 4 procedurally generated extruded 2D chevron polygon meshes (`MeshTopology.Triangles`) with holographic translucent cyan material (`#00E5FF`, alpha $\approx 0.72$, emission HDR intensity 1.4).
   - **Wave Pulse**: Parametric travelling luminosity wave ($v = 1.05\text{ s}^{-1}$, period $\sim 0.95\text{ s}$, amplitude $0.22$).
   - **Hover Height**: Calibrated hover elevation of $0.024\text{ m}$ above table surface, oriented along tabletop plane pointing directly at target anchor.
   - **Safety Buffer**: Arrow path terminates at $t = 0.80$, guaranteeing a $\sim 0.059\text{ m}$ clear visual buffer before target anchor.

2. **Precision Segmented Reticle & Beacon (`A3R_TargetRing.cs`)**:
   - **Reticle**: Multi-ring segmented arc reticle rotating at $12^\circ/\text{s}$ with subtle breathing pulse (amplitude $0.06$).
   - **Vertical Beacon**: Vertical light column ($h = 0.045\text{ m}$) topped by an emissive diamond beacon ($r = 0.005\text{ m}$).
   - **Dynamic Sizing**: Radius scaled dynamically per target type ($r = 0.045\text{ m}$ for breadboard overview; $r = 0.024\text{ m}$ for discrete component pins) ensuring physical targets are never obscured.

3. **Procedural Physical Electronics Scene (`PreviewSceneBuilder.cs`)**:
   - **ESP32 Microcontroller Board**: Dark green/black FR-4 PCB ($51\text{ mm} \times 28\text{ mm} \times 1.6\text{ mm}$), 4 brass corner mounting holes, metallic RF shield canister (`ESP-WROOM-32`), 2x 15-pin 0.1" female headers, Micro-USB / USB-C connector housing, EN and BOOT tactile pushbuttons, power indicator LED, and 3D floating label explicitly reading `"ESP32 BOARD VISUAL PLACEHOLDER"`.
   - **Half-Size Solderless Breadboard**: Off-white ABS plastic body ($82\text{ mm} \times 55\text{ mm} \times 8.5\text{ mm}$), 0.3mm central IC isolation trench, color-coded power distribution rails (red $+V$, blue $-V$), and 30-row pinhole socket grid with silk-screen coordinate markers.
   - **Coherent Circuit Components**:
     - *220Ω Resistor*: Cylindrical beige ceramic body ($3.2\text{ mm} \times 6\text{ mm}$) with official 4-band color coding: Red (2), Red (2), Brown ($\times 10^1 = 220\Omega$), and Gold ($\pm 5\%$), with curved silver axial leads inserted into breadboard rows 10 and 14.
     - *5mm Red LED*: Diffuse translucent red epoxy dome lens ($d = 5\text{ mm}$) with distinct anode/cathode lead geometry inserted into row 14 and row 18.
     - *Jumper Wires*: Insulated yellow jumper wire routing GPIO 13 to row 10; black jumper wire routing GND to row 18.
     - *USB Power Cable*: Matte black cable routing from workspace boundary into the ESP32 USB connector.

---

## 4. Academic & Public Release Compliance

Before any external submission, open-source publication, or public distribution:
- Any future replacement of procedural primitives or placeholder GLBs with textured artist meshes must strictly utilize CC0 (Public Domain) or CC-BY (Creative Commons Attribution) models with documented author names, source URLs, and original license text.
- Because all production and preview assets are currently procedurally synthesized from standard mathematical primitives, zero proprietary third-party 3D geometry or copyrighted CAD models are incorporated into the repository or build artifacts.
- Research disclaimers regarding provisional GPIO pin allocations (GPIO 13 / 12 software-test notice) and hardware bridge simulation remain active and uncompromised.

