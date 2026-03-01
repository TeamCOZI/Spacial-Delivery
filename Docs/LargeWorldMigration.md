# Large World Migration (Spacial-Delivery)

## Goal
- Keep simulation stable from close-up view (`z ~= -0.01`) to system view (`z ~= -40000`).
- Preserve gameplay automation/encounters without dropping simulation.
- Avoid precision artifacts (flicker, unstable ray hits, transform jitter).

## Core Strategy
1. Use `double` for simulation coordinates.
2. Keep Unity `Transform` in a camera-local `float` space.
3. Run multi-tier simulation:
- Near-field: full Unity physics.
- Mid-field: reduced-step / simplified interactions.
- Far-field: deterministic analytical update (event-driven).

## Current Project Constraints
- Orbit simulation currently writes directly into `Rigidbody.MovePosition` using `float` output from recursive calculation:
  - `Assets/Scripts/CelestialBody/OrbitRevolution.cs`
- Camera range is extreme and shared by the same scene objects:
  - `Assets/Scripts/CameraManager.cs`
- World shifting exists but still relies on `Transform` world coordinates:
  - `Assets/Scripts/WorldOriginManager.cs`

## Phase 1 (Implemented in this patch)
### New infrastructure (non-breaking)
- `Assets/Scripts/LargeWorld/Double3.cs`
  - Minimal serializable double vector type.
- `Assets/Scripts/LargeWorld/WorldPosition.cs`
  - Per-object absolute world position holder (`double`).
- `Assets/Scripts/LargeWorld/LargeWorldCoordinator.cs`
  - Converts absolute world position to local render position (`world - origin`).
  - Syncs registered objects in `LateUpdate`.

This phase does not replace existing orbit/physics yet. It adds the foundation safely.

## Phase 2 (Next code changes)
1. Update `OrbitRevolution`:
- Calculate absolute orbit position as `Double3`.
- Write to `WorldPosition.worldPosition` if present.
- Keep legacy `Rigidbody.MovePosition` path as fallback.

2. Attach `WorldPosition` on generated celestial objects:
- `SolarSystemFactory.GenerateStar/Planet/Satellite`
- `SolarSystemGenerator` artificial satellite branch.

3. Set `LargeWorldCoordinator.worldOrigin` near active focus:
- On focus change (`FocusManager`), recenter origin to focused object's `WorldPosition`.

## Phase 3 (Physics tiers)
### Baseline implemented
- `Assets/Scripts/LargeWorld/SimulationTier.cs`
- `Assets/Scripts/LargeWorld/SimulationTierTarget.cs`
- `Assets/Scripts/LargeWorld/SimulationTierManager.cs`

Current behavior:
- Distance-based tier assignment (`Near/Mid/Far`) from camera.
- Tier-driven orbit update interval (`OrbitRevolution.SetSimulationStepInterval`).
- Optional far-tier toggles for colliders and heavy visual helpers.

2. Encounter/process systems should read simulation state, not raw rigidbody state.

## Phase 4 (Rendering and interaction stability)
1. Separate icon/overlay interaction from world collider competition.
2. Use deterministic hit priority for selectable targets.
3. Keep camera clip ranges adaptive per mode/layer.

## Practical Rollout
1. Add `LargeWorldCoordinator` to scene root (single instance).
2. Migrate only star/planet first.
3. Validate:
- Focus stability at deep zoom-in/out.
- No visible jumps while changing focus.
- Deterministic automation state after long fast-forward.
4. Migrate satellites, then artificial satellites and encounter entities.
