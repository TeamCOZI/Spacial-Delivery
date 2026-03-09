# PART_SPEC

Reusable specification format for adding new assembly parts, even in a fresh session.

## Required Rules
- Always include `part_spec_version`.
- Always include `id`, `name_en`, and all fields in `naming`.
- Always set `classification.part_type` explicitly.
- Define ports only with `cell + side` (`Top | Bottom | Left | Right`).
- For non-pipe parts, `AssemblyPartPortProfile` input direction must match `ports.inputs` side definition.
- New parts should be placeable by auto-orienting ghost input to cell-side output mapping (same behavior as pipe start placement).
- Keep these scale rules:
  - Body prefab Z scale is fixed to `1.0`.
  - Port prefab Z scale is fixed to `0.95`.
- Port visuals must be placed on face centers only, never at body corners.
- For multi-cell parts, each port position must align to its owning cell face center.
- Port visual design scale baseline is `0.3 x 0.3` (XY).
- If parent/body scale is not `1 x 1` cell, compensate child local scale by parent scale so final world visual size stays consistent.
  - Example: body scale `1 x 3` (`x:0.1, y:0.3`) with baseline port world look => port local scale `x:0.3, y:0.1`.
- Ghost prefab must include visible port objects too.
- Input port visual = output port visual shape, but color is blue.
- Ghost validity tint must not recolor port visuals (`Port`/`Dock` renderers keep original colors).
- Part List UI icon tint should match the part's representative color.

## YAML Template
```yaml
part_spec_version: 1
name_kr: ""
name_en: ""
id: ""   # snake_case recommended

classification:
  part_type: Processor   # Core | Pipe | Processor | Storage
  tag: Part

visual:
  mesh: Cube
  scale: { x: 0.1, y: 0.1, z: 1.0 }   # body z must stay 1.0
  color_rgba_255: [255, 255, 255, 255]

grid:
  width: 1
  height: 1
  cell_size: 0.1

stats:
  durability: 100
  capacity: 0
  mass: 0

ports:
  cell_origin: [0, 0]
  port_visual_scale: { x: 0.3, y: 0.3, z: 0.95 }   # baseline visual scale, local scale can be compensated by parent scale
  inputs:
    - { cell: [0, 0], side: Left, color: InputBlue }
  outputs:
    - { cell: [0, 0], side: Right, color: OutputRed }

ghost:
  required: true
  include_ports: true
  body_material: GhostMaterial
  keep_same_port_layout_as_main: true
  keep_port_visual_original_color: true

assets:
  create_prefab: true
  create_ghost_prefab: true
  create_part_asset: true
  create_input_port_material_if_missing: true
  add_to_part_list: true
  resources_part_path: "Assets/Resources/Parts"

naming:
  prefab: ""
  ghost_prefab: ""
  part_asset: ""
  material: ""
  input_port_material: "InputPortMaterial"

mapping:
  durability_target: "Part.asset.durability"
  capacity_target: "Part.asset.inventory"
  mass_target: "Part.asset.mass (auto-calculated from partPrefab scale)"
  part_list_icon_color_source: "partPrefab first renderer material _BaseColor/_Color"
  input_port_profile_source: "AssemblyPartPortProfile.inputPortLocalPositions (must match ports.inputs)"

notes: ""
```

## Fresh-Session Prompt Template
```text
Create assets from this Part Spec:
- Create Prefab, Ghost Prefab, and Part.asset
- Ensure it appears via Resources/Parts loading
- Build ports by AssemblyPartPortLayout
- Set AssemblyPartPortProfile input direction to match `ports.inputs` side
- Apply scale rules (body z=1.0, port z=0.95, baseline port scale 0.3/0.3/0.95)
- Compensate port local scale by parent scale when body is non-1x1, so final world visual size stays consistent
- Place ports on face centers only (never on corners)
- Ghost must include visual ports
- Input ports are blue, output ports are red
- Ghost body can tint by validity, but port visuals must keep original colors
- Part List UI icon tint must match part color
- Report file list and final value mapping

[Part Spec YAML]
```

## Example: Heater
```yaml
part_spec_version: 1
name_kr: "가열기"
name_en: "Heater"
id: "heater"

classification:
  part_type: Processor
  tag: Part

visual:
  mesh: Cube
  scale: { x: 0.1, y: 0.1, z: 1.0 }
  color_rgba_255: [252, 238, 33, 255]

grid:
  width: 1
  height: 1
  cell_size: 0.1

stats:
  durability: 100
  capacity: 2
  mass: 100

ports:
  cell_origin: [0, 0]
  port_visual_scale: { x: 0.3, y: 0.3, z: 0.95 }
  inputs:
    - { cell: [0, 0], side: Left, color: InputBlue }
  outputs:
    - { cell: [0, 0], side: Right, color: OutputRed }
    - { cell: [0, 0], side: Top, color: OutputRed }
    - { cell: [0, 0], side: Bottom, color: OutputRed }

ghost:
  required: true
  include_ports: true
  body_material: GhostMaterial
  keep_same_port_layout_as_main: true
  keep_port_visual_original_color: true

assets:
  create_prefab: true
  create_ghost_prefab: true
  create_part_asset: true
  create_input_port_material_if_missing: true
  add_to_part_list: true
  resources_part_path: "Assets/Resources/Parts"

naming:
  prefab: "HeaterPrefab"
  ghost_prefab: "HeaterGhostPrefab"
  part_asset: "Heater.asset"
  material: "HeaterMaterial"
  input_port_material: "InputPortMaterial"

mapping:
  durability_target: "Part.asset.durability"
  capacity_target: "Part.asset.inventory"
  mass_target: "Part.asset.mass (auto-calculated from partPrefab scale)"
  part_list_icon_color_source: "partPrefab first renderer material _BaseColor/_Color"
  input_port_profile_source: "AssemblyPartPortProfile.inputPortLocalPositions"

notes: ""
```
