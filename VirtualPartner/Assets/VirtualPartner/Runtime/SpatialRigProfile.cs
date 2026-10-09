using System;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [CreateAssetMenu(menuName = "VirtualPartner/Spatial Rig Profile")]
    public sealed class SpatialRigProfile : ScriptableObject
    {
        public string characterId;
        public Vector3 bodyForward = Vector3.forward;
        public Vector3 bodyUp = Vector3.up;
        public float positionTolerance = .03f;
        public float maximumPositionCorrection = .05f;
        public float orientationTolerance = 10;
        public float maximumOrientationCorrection = 15;
        public float footTolerance = .01f;
        public float returnSeconds = .35f;
        public float maxPelvisOffset = .2f;
        public float torsoClearance = .065f;
        public float headClearance = .15f;
        public SpatialJointCalibration[] joints = Array.Empty<SpatialJointCalibration>();
        public SpatialLimbCalibration[] limbs = Array.Empty<SpatialLimbCalibration>();
        public SpatialTrackDto[] thinkingTracks = Array.Empty<SpatialTrackDto>();
    }
    [Serializable] public sealed class SpatialJointCalibration
    {
        public string bone;
        public string side;
        public Quaternion restRotation;
        public Vector3 restPosition;
        public Vector3 minimum = new Vector3(-90,-90,-90);
        public Vector3 maximum = new Vector3(90,90,90);
    }
    [Serializable] public sealed class SpatialLimbCalibration
    {
        public string group;
        public float upperLength, lowerLength;
        public Vector3 palmLocal, fingersLocal, soleLocal;
        public float minBend = 2, maxBend = 155;
    }
}
