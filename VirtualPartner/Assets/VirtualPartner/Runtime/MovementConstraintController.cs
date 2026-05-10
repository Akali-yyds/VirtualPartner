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

            if (!roomMoveArea.ContainsWorldPoint(proposedPosition))
            {
                reason = "Root would leave RoomMoveArea.";
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

                if (!obstacleArea.ContainsWorldPoint(proposedPosition))
                    continue;

                reason = $"Root would enter ObstacleArea '{obstacleArea.name}'.";
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
        public static bool ContainsWorldPoint(Transform areaTransform, Vector3 worldPoint)
        {
            if (areaTransform == null)
                return false;

            var localPoint = areaTransform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(localPoint.x) <= 0.5f
                && Mathf.Abs(localPoint.y) <= 0.5f
                && Mathf.Abs(localPoint.z) <= 0.5f;
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
