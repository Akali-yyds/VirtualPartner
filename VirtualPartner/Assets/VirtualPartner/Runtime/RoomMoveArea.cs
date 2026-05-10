using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RoomMoveArea : MonoBehaviour
    {
        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            return MovementConstraintBoxUtility.ContainsWorldPointXZ(transform, worldPoint);
        }

        public bool ContainsWorldCircle(Vector3 worldCenter, float radius)
        {
            return MovementConstraintBoxUtility.ContainsWorldCircleXZ(transform, worldCenter, radius);
        }

        private void OnDrawGizmos()
        {
            MovementConstraintBoxUtility.DrawLocalBox(transform, new Color(0.1f, 0.8f, 0.35f, 0.9f));
        }
    }
}
