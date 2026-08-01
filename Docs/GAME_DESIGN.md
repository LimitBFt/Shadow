# Game Design

This project is a 3D light-and-shadow interaction platform game prototype.

## Core Idea

The player should eventually be able to use generated shadows as physical platforms. A light source illuminates scene objects, and those objects cast calculated shadow meshes onto a chosen projection plane.

## Intended Player Experience

- Move through a 3D platforming space.
- Change light, object, or plane relationships to reshape traversal paths.
- Stand on generated shadow geometry through MeshCollider support.
- Solve spatial puzzles by understanding how light direction and object silhouette affect the playable shadow.

## Shadow Gameplay Model

- A light source has a world-space position.
- A projection object provides Mesh vertices.
- A projection plane receives the projected points.
- Rays are traced from the light position through object vertices.
- Ray-plane intersections form projected points.
- Projected points are reduced into a two-dimensional convex polygon.
- The polygon is extruded into a shadow Mesh with thickness.
- A MeshCollider makes the generated shadow standable.
- Generated shadows are tracked per projection object with a Dictionary.
- Shadows update only when relevant transforms or mesh inputs change.

## Current Scope

This document records the design direction only. No formal player controller, shadow system, generated mesh runtime, or interaction mechanic is implemented yet.

