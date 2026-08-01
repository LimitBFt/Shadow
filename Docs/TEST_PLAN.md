# Test Plan

Testing should prioritize deterministic math and mesh construction before gameplay.

## EditMode Tests

Planned EditMode test areas:

- Ray from light source through object vertex intersects projection plane correctly.
- Parallel or invalid ray-plane cases are handled safely.
- World-space points convert to stable two-dimensional plane coordinates.
- Convex hull returns ordered boundary points.
- Duplicate and collinear points are handled predictably.
- Thick shadow Mesh contains expected vertices, triangles, normals, and bounds.
- MeshCollider assignment uses the generated Mesh.
- Dictionary management creates one generated shadow per projection object.
- Dirty-state checks update only when light, object, mesh, or plane state changes.

## PlayMode Tests

Planned PlayMode test areas:

- Generated shadow platform can receive collision.
- Player can stand on generated shadow geometry.
- Moving light or projection object updates the generated platform.
- Unchanged objects do not trigger unnecessary rebuilds.

## Current Status

No tests have been created or run yet.

