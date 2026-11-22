using System;
using ShadowPath.Interaction;
using ShadowPath.Lighting;
using ShadowPath.Shadow;
using UnityEngine;

namespace ShadowPath.Player
{
    internal sealed class CheckpointSnapshot
    {
        internal Vector3 PlayerPosition { get; private set; }
        internal Quaternion PlayerRotation { get; private set; }
        internal LightGameplayArea Area { get; private set; }
        private LightSourceController light;
        private LightSourceController.CheckpointState lightState;
        private LightGameplayArea.CheckpointState areaState;
        private RotatableObject[] objects;
        private RotatableObject.CheckpointState[] objectStates;
        private ShadowSystem[] shadowSystems;
        private Transform projectionPlane;
        private Light projectionLight;

        internal static bool TryCapture(Vector3 position, Quaternion rotation, LightGameplayArea area,
            RotatableObject[] configuredObjects, ShadowSystem[] configuredSystems,
            out CheckpointSnapshot snapshot, out string failureReason)
        {
            snapshot = null;
            failureReason = null;
            if (area == null || area.ProjectionPlane == null || area.LightController == null)
            {
                failureReason = "Snapshot requires a Gameplay Area, Projection Plane and LightSourceController.";
                return false;
            }
            if (configuredSystems == null || configuredSystems.Length == 0)
            {
                failureReason = "Assign the Shadow Systems used by this checkpoint.";
                return false;
            }
            LightSourceController light = area.LightController;
            foreach (ShadowSystem system in configuredSystems)
            {
                if (system != null && system.MatchesCheckpointEnvironment(
                    light.transform, area.ProjectionPlane, area.ProjectionLight)) continue;
                failureReason = "Each configured ShadowSystem must be active and use this area's Light and Projection Plane.";
                return false;
            }
            if (!area.TryGetGameplayCircle(out _, out _))
            {
                failureReason = "Cannot capture an invalid Gameplay Circle.";
                return false;
            }

            configuredObjects = configuredObjects ?? Array.Empty<RotatableObject>();
            var states = new RotatableObject.CheckpointState[configuredObjects.Length];
            for (int i = 0; i < configuredObjects.Length; i++)
            {
                if (configuredObjects[i] == null || configuredObjects[i].gameObject.scene != area.gameObject.scene)
                {
                    failureReason = "Checkpoint Objects contains a missing object or an object from another scene.";
                    return false;
                }
                states[i] = configuredObjects[i].CaptureCheckpointState();
            }
            snapshot = new CheckpointSnapshot
            {
                PlayerPosition = position,
                PlayerRotation = rotation,
                Area = area,
                light = light,
                lightState = light.CaptureCheckpointState(),
                areaState = area.CaptureCheckpointState(),
                objects = (RotatableObject[])configuredObjects.Clone(),
                objectStates = states,
                shadowSystems = (ShadowSystem[])configuredSystems.Clone(),
                projectionPlane = area.ProjectionPlane,
                projectionLight = area.ProjectionLight
            };
            return true;
        }

        internal bool TryCaptureCurrentEnvironment(Vector3 playerPosition, Quaternion playerRotation,
            out CheckpointSnapshot snapshot, out string failureReason)
        {
            return TryCapture(playerPosition, playerRotation, Area, objects, shadowSystems,
                out snapshot, out failureReason);
        }

        internal bool TryRestoreEnvironment(out string failureReason)
        {
            failureReason = null;
            try
            {
                if (Area == null || light == null || Area.LightController != light
                    || Area.ProjectionPlane != projectionPlane || Area.ProjectionLight != projectionLight)
                {
                    failureReason = "Snapshot environment references were removed or replaced.";
                    return false;
                }
                foreach (RotatableObject target in objects)
                {
                    if (target != null) continue;
                    failureReason = "A saved RotatableObject was destroyed.";
                    return false;
                }
                foreach (ShadowSystem system in shadowSystems)
                {
                    if (system != null && system.MatchesCheckpointEnvironment(
                        light.transform, Area.ProjectionPlane, Area.ProjectionLight)) continue;
                    failureReason = "A saved ShadowSystem is missing, inactive or has different projection references.";
                    return false;
                }

                // Local poses are independent of configured parent/child ordering.
                for (int i = 0; i < objects.Length; i++) objects[i].RestoreCheckpointState(objectStates[i]);
                if (!light.TryRestoreCheckpointState(lightState))
                {
                    failureReason = "Saved Light pose is invalid or outside Level Light Bounds.";
                    return false;
                }
                // The Spot Light may be a child of LightSource; restore its world pose after its parent.
                Area.RestoreCheckpointState(areaState);
                Physics.SyncTransforms();
                foreach (ShadowSystem system in shadowSystems)
                    if (!system.TryRebuildForCheckpoint(out failureReason)) return false;
                // Rebuild publishes GeometryVersion only after sharedMesh has been replaced.
                Physics.SyncTransforms();
                return true;
            }
            catch (Exception exception)
            {
                failureReason = $"Snapshot restoration threw {exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }
    }
}
