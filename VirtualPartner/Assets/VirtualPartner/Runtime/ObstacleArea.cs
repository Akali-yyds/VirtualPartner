using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ObstacleArea : MonoBehaviour
    {
        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            return MovementConstraintBoxUtility.ContainsWorldPoint(transform, worldPoint);
        }

        private void OnDrawGizmos()
        {
            MovementConstraintBoxUtility.DrawLocalBox(transform, new Color(0.95f, 0.25f, 0.15f, 0.9f));
        }
    }
}
