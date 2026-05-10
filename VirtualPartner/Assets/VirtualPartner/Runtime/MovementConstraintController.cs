using System;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MovementConstraintController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform root;
        [SerializeField] private RoomMoveArea roomMoveArea;
        [SerializeField] private ObstacleArea[] obstacleAreas = Array.Empty<ObstacleArea>();

        [Header("Settings")]
        [SerializeField] private float rootClearanceRadius = 0.3f;

        [Header("Runtime Status")]
        [SerializeField] private bool constraintActive;
        [SerializeField] private bool lastResult = true;
        [SerializeField] private string lastReason = "Not checked.";
        [SerializeField] private int obstacleAreaCount;

        private bool missingRoomWarningLogged;

        public bool IsConstraintActive => constraintActive;
        public bool LastResult => lastResult;
        public string LastReason => lastReason;
        public int ObstacleAreaCount => obstacleAreaCount;
        public float RootClearanceRadius => Mathf.Max(0f, rootClearanceRadius);

        public void Configure(Transform rootTransform)
        {
            root = rootTransform;
            missingRoomWarningLogged = false;
            RefreshObstacleAreaCount();
            constraintActive = roomMoveArea != null && roomMoveArea.isActiveAndEnabled;
            lastResult = true;
            lastReason = constraintActive ? "Ready." : "MovementConstraint disabled.";
        }

        public bool CanMoveTo(Vector3 proposedPosition, out string reason)
        {
            RefreshObstacleAreaCount();

            if (roomMoveArea == null || !roomMoveArea.isActiveAndEnabled)
            {
                constraintActive = false;
                reason = "MovementConstraint disabled: RoomMoveArea reference is missing.";
                lastResult = true;
                lastReason = reason;

                if (!missingRoomWarningLogged)
                {
                    Debug.LogWarning($"[VirtualPartner] {reason}", this);
                    missingRoomWarningLogged = true;
                }

                return true;
            }

            constraintActive = true;

            var clearanceRadius = RootClearanceRadius;

            if (!roomMoveArea.ContainsWorldCircle(proposedPosition, clearanceRadius))
            {
                reason = "Root clearance would leave RoomMoveArea.";
                lastResult = false;
                lastReason = reason;
                return false;
            }

            if (obstacleAreas == null)
                obstacleAreas = Array.Empty<ObstacleArea>();

            for (var i = 0; i < obstacleAreas.Length; i++)
            {
                var obstacleArea = obstacleAreas[i];
                if (obstacleArea == null || !obstacleArea.isActiveAndEnabled)
                    continue;

                if (!obstacleArea.IntersectsWorldCircle(proposedPosition, clearanceRadius))
                    continue;

                reason = $"Root clearance would overlap ObstacleArea '{obstacleArea.name}'.";
                lastResult = false;
                lastReason = reason;
                return false;
            }

            reason = string.Empty;
            lastResult = true;
            lastReason = "Allowed.";
            return true;
        }

        private void RefreshObstacleAreaCount()
        {
            obstacleAreaCount = 0;
            if (obstacleAreas == null)
                return;

            for (var i = 0; i < obstacleAreas.Length; i++)
            {
                if (obstacleAreas[i] != null)
                    obstacleAreaCount++;
            }
        }
    }

    internal static class MovementConstraintBoxUtility
    {
        public static bool ContainsWorldPointXZ(Transform areaTransform, Vector3 worldPoint)
        {
            if (areaTransform == null)
                return false;

            var localPoint = areaTransform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(localPoint.x) <= 0.5f
                && Mathf.Abs(localPoint.z) <= 0.5f;
        }

        public static bool ContainsWorldCircleXZ(Transform areaTransform, Vector3 worldCenter, float radius)
        {
            if (areaTransform == null)
                return false;

            radius = Mathf.Max(0f, radius);
            if (radius <= 0f)
                return ContainsWorldPointXZ(areaTransform, worldCenter);

            var localPoint = areaTransform.InverseTransformPoint(worldCenter);
            var scale = areaTransform.lossyScale;
            var marginX = radius / Mathf.Max(0.0001f, Mathf.Abs(scale.x));
            var marginZ = radius / Mathf.Max(0.0001f, Mathf.Abs(scale.z));

            return Mathf.Abs(localPoint.x) <= 0.5f - marginX
                && Mathf.Abs(localPoint.z) <= 0.5f - marginZ;
        }

        public static bool IntersectsWorldCircleXZ(Transform areaTransform, Vector3 worldCenter, float radius)
        {
            if (areaTransform == null)
                return false;

            radius = Mathf.Max(0f, radius);
            if (radius <= 0f)
                return ContainsWorldPointXZ(areaTransform, worldCenter);

            var localPoint = areaTransform.InverseTransformPoint(worldCenter);
            var scale = areaTransform.lossyScale;
            var outsideX = Mathf.Max(0f, Mathf.Abs(localPoint.x) - 0.5f) * Mathf.Abs(scale.x);
            var outsideZ = Mathf.Max(0f, Mathf.Abs(localPoint.z) - 0.5f) * Mathf.Abs(scale.z);

            return outsideX * outsideX + outsideZ * outsideZ <= radius * radius;
        }

        public static void DrawLocalBox(Transform areaTransform, Color color)
        {
            if (areaTransform == null)
                return;

            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;

            Gizmos.color = color;
            Gizmos.matrix = areaTransform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
