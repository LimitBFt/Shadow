# Architecture

The project will keep custom work inside `Assets/_Project`.

## Planned Runtime Areas

- `Scripts/Core`: shared project-level utilities and base types.
- `Scripts/Shadow`: shadow projection, polygon processing, mesh generation, and shadow object management.
- `Scripts/Player`: player movement and controller code, when approved.
- `Scripts/Interaction`: switches, movable objects, light controls, and puzzle triggers.
- `Scripts/Debugging`: gizmos, runtime diagnostics, and debug visual helpers.

## Planned Shadow Pipeline

1. Read the light source position.
2. Read source Mesh vertices from the projection object.
3. Convert relevant vertices to world space.
4. Build rays from the light position through those vertices.
5. Intersect each ray with the configured projection plane.
6. Convert intersection points into the plane's local 2D coordinate space.
7. Compute a two-dimensional convex hull.
8. Generate a Mesh from the hull with a configurable thickness.
9. Assign the Mesh to rendering and MeshCollider components.
10. Store generated shadow objects in a Dictionary keyed by the projection object.
11. Rebuild only when the light, projection object, mesh, or projection plane changes.

## Update Strategy

The shadow system should avoid per-frame allocations and repeated object creation. Runtime code should cache generated GameObjects, Meshes, and reusable buffers where practical. Dirty-state checks should decide when recalculation is required.

## Testing Strategy

Math-heavy code should be covered with EditMode tests, especially:

- Ray-plane intersection.
- Plane-space conversion.
- Convex hull generation.
- Mesh vertex and triangle generation.
- Dirty-state detection.

No Assembly Definition or test code is created yet.

