using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    // A detached FK buffer: preflight never changes scene transforms.
    public sealed class SpatialPoseBuffer
    {
        private readonly Dictionary<Transform, Quaternion> rotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Vector3> positions = new Dictionary<Transform, Vector3>();
        public void Capture(Transform[] bones, ActionCoordinator coordinator, bool visible)
        {
            rotations.Clear(); positions.Clear();
            foreach (var b in bones)
            { rotations[b] = visible ? coordinator.ReadVisibleRotation(b) : b.localRotation; positions[b] = visible ? coordinator.ReadVisiblePosition(b) : b.localPosition; }
        }
        public Quaternion LocalRotation(Transform b) => rotations.TryGetValue(b, out var q) ? q : b.localRotation;
        public Vector3 LocalPosition(Transform b) => positions.TryGetValue(b, out var p) ? p : b.localPosition;
        public Quaternion Rotation(Transform b) => b.parent == null ? LocalRotation(b) : Rotation(b.parent) * LocalRotation(b);
        public Vector3 Position(Transform b) => b.parent == null ? LocalPosition(b) : Matrix(b.parent).MultiplyPoint3x4(LocalPosition(b));
        private Matrix4x4 Matrix(Transform b) => (b.parent == null ? Matrix4x4.identity : Matrix(b.parent)) * Matrix4x4.TRS(LocalPosition(b), LocalRotation(b), b.localScale);
        public void SetRotation(Transform b, Quaternion world) => rotations[b] = b.parent == null ? world : Quaternion.Inverse(Rotation(b.parent)) * world;
        public void SetLocal(Transform b, Quaternion rotation, Vector3 position) { rotations[b] = rotation; positions[b] = position; }
        public void SetPosition(Transform b, Vector3 world) => positions[b] = b.parent == null ? world : Matrix(b.parent).inverse.MultiplyPoint3x4(world);

        public bool SolveLimb(Transform root, Transform mid, Transform tip, Vector3 target, Vector3 hint,
            Quaternion? orientation, SpatialLimbCalibration calibration, SpatialRigProfile limits, out float error, out string failure)
        {
            failure = null; error = 0;
            Vector3 a = Position(root), b = Position(mid), c = Position(tip);
            float upper = Vector3.Distance(a,b), lower = Vector3.Distance(b,c), length = upper+lower;
            if (length < .00001f) { failure = "Degenerate limb."; return false; }
            Vector3 offset = target-a;
            float distance = offset.magnitude;
            if (distance < .000001f) { failure = "Target coincides with limb root."; return false; }
            float min = Mathf.Sqrt(upper*upper+lower*lower+2*upper*lower*Mathf.Cos(calibration.maxBend*Mathf.Deg2Rad));
            float max = Mathf.Sqrt(upper*upper+lower*lower+2*upper*lower*Mathf.Cos(calibration.minBend*Mathf.Deg2Rad));
            float reachable = Mathf.Clamp(distance, Mathf.Max(min,.00001f), max);
            error = Mathf.Abs(reachable-distance)/length;
            if (error > limits.maximumPositionCorrection) { failure = "Target outside calibrated reach (relative error " + error.ToString("F3") + ")."; return false; }
            Vector3 direction = offset/distance;
            Vector3 pole = Vector3.ProjectOnPlane(hint-a, direction);
            if (pole.sqrMagnitude < .000001f) pole = Vector3.ProjectOnPlane(b-a, direction);
            if (pole.sqrMagnitude < .000001f) { failure = "Elbow/knee hint is singular."; return false; }
            float along = (upper*upper-lower*lower+reachable*reachable)/(2*reachable);
            Vector3 elbow = a+direction*along+pole.normalized*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            SetRotation(root, Quaternion.FromToRotation(b-a,elbow-a)*Rotation(root));
            Vector3 currentTip = Position(tip), currentMid = Position(mid);
            SetRotation(mid,Quaternion.FromToRotation(currentTip-currentMid,a+direction*reachable-currentMid)*Rotation(mid));
            if (orientation.HasValue) SetRotation(tip,orientation.Value);
            error = Vector3.Distance(Position(tip),target)/length;
            if (error > limits.positionTolerance) { failure = "Solved limb exceeds position tolerance."; return false; }
            if (orientation.HasValue && Quaternion.Angle(Rotation(tip), orientation.Value) > limits.orientationTolerance)
            { failure = "Solved limb exceeds orientation tolerance."; return false; }
            return true;
        }

        public static Vector3 Hermite(Vector3 p0, Vector3 p1, Vector3 v0, Vector3 v1, float t, float duration)
        {
            float t2=t*t,t3=t2*t;
            return (2*t3-3*t2+1)*p0+(t3-2*t2+t)*duration*v0+(-2*t3+3*t2)*p1+(t3-t2)*duration*v1;
        }
        public static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        { var d=b-a; return Vector3.Distance(p,a+d*Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(d.sqrMagnitude,.0000001f))); }
    }
}
