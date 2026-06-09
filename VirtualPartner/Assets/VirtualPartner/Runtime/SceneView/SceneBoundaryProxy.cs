using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualPartner.Runtime
{
    public enum SceneBoundaryProxyShape
    {
        FloorSkirt,
        RoomSilhouette,
        ClosedBoundsShell
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SceneBoundaryProxy : MonoBehaviour
    {
        private const string MeshName = "SceneBoundaryProxyMesh";

        [Header("Source")]
        [SerializeField] private Transform sourceRendererRoot;

        [Header("Shape")]
        [SerializeField] private SceneBoundaryProxyShape shape = SceneBoundaryProxyShape.RoomSilhouette;
        [SerializeField] private float height = 0.18f;
        [SerializeField] private float verticalOffset = 0.01f;
        [SerializeField] private float outset = 0.08f;
        [SerializeField] private bool useProjectedSilhouette = true;
        [SerializeField] private Camera silhouetteCamera;
        [SerializeField] private float silhouetteHeight = 2.4f;
        [SerializeField] private float topOffset = 0.02f;
        [SerializeField] private bool includeSideWalls;
        [SerializeField] private bool includeRightSideWall = true;
        [SerializeField] private int rightSideWallEdgeIndex = -1;
        [SerializeField] private bool includeOpenEdgeRightCap = true;
        [SerializeField, Range(0f, 1f)] private float openEdgeRightCapStart = 0.5f;
        [SerializeField] private float edgeRibbonThickness = 0.02f;
        [SerializeField] private float rightCapTopLift = 0.035f;
        [SerializeField] private Transform openEdgeReference;
        [SerializeField] private int openEdgeIndex = -1;
        [SerializeField] private Vector3[] manualFootprintPoints = Array.Empty<Vector3>();

        [Header("Runtime")]
        [SerializeField] private Mesh generatedMesh;

        private readonly List<Vector3> scratchPoints = new();
        private Matrix4x4 lastSilhouetteViewProjection;

        public Transform SourceRendererRoot
        {
            get => sourceRendererRoot;
            set
            {
                sourceRendererRoot = value;
                Rebuild();
            }
        }

        public SceneBoundaryProxyShape Shape
        {
            get => shape;
            set
            {
                shape = value;
                Rebuild();
            }
        }

        public float Height
        {
            get => height;
            set
            {
                height = Mathf.Max(0.01f, value);
                Rebuild();
            }
        }

        public float VerticalOffset
        {
            get => verticalOffset;
            set
            {
                verticalOffset = value;
                Rebuild();
            }
        }

        public float Outset
        {
            get => outset;
            set
            {
                outset = Mathf.Max(0f, value);
                Rebuild();
            }
        }

        public bool UseProjectedSilhouette
        {
            get => useProjectedSilhouette;
            set
            {
                useProjectedSilhouette = value;
                Rebuild();
            }
        }

        public Camera SilhouetteCamera
        {
            get => silhouetteCamera;
            set
            {
                silhouetteCamera = value;
                Rebuild();
            }
        }

        public float SilhouetteHeight
        {
            get => silhouetteHeight;
            set
            {
                silhouetteHeight = Mathf.Max(0.01f, value);
                Rebuild();
            }
        }

        public float TopOffset
        {
            get => topOffset;
            set
            {
                topOffset = value;
                Rebuild();
            }
        }

        public bool IncludeSideWalls
        {
            get => includeSideWalls;
            set
            {
                includeSideWalls = value;
                Rebuild();
            }
        }

        public bool IncludeRightSideWall
        {
            get => includeRightSideWall;
            set
            {
                includeRightSideWall = value;
                Rebuild();
            }
        }

        public int RightSideWallEdgeIndex
        {
            get => rightSideWallEdgeIndex;
            set
            {
                rightSideWallEdgeIndex = value;
                Rebuild();
            }
        }

        public bool IncludeOpenEdgeRightCap
        {
            get => includeOpenEdgeRightCap;
            set
            {
                includeOpenEdgeRightCap = value;
                Rebuild();
            }
        }

        public float OpenEdgeRightCapStart
        {
            get => openEdgeRightCapStart;
            set
            {
                openEdgeRightCapStart = Mathf.Clamp01(value);
                Rebuild();
            }
        }

        public float EdgeRibbonThickness
        {
            get => edgeRibbonThickness;
            set
            {
                edgeRibbonThickness = Mathf.Max(0.001f, value);
                Rebuild();
            }
        }

        public float RightCapTopLift
        {
            get => rightCapTopLift;
            set
            {
                rightCapTopLift = Mathf.Max(0f, value);
                Rebuild();
            }
        }

        public Transform OpenEdgeReference
        {
            get => openEdgeReference;
            set
            {
                openEdgeReference = value;
                Rebuild();
            }
        }

        public int OpenEdgeIndex
        {
            get => openEdgeIndex;
            set
            {
                openEdgeIndex = value;
                Rebuild();
            }
        }

        public void SetFootprint(Vector3[] worldPoints)
        {
            manualFootprintPoints = worldPoints == null ? Array.Empty<Vector3>() : (Vector3[])worldPoints.Clone();
            Rebuild();
        }

        public void Rebuild()
        {
            EnsureMesh();
            ConfigureRenderer();

            scratchPoints.Clear();
            var sourceBounds = default(Bounds);
            var hasSourceBounds = false;
            if (manualFootprintPoints != null && manualFootprintPoints.Length >= 3)
            {
                scratchPoints.AddRange(manualFootprintPoints);
                sourceBounds = GetPointBounds(scratchPoints);
                if (sourceBounds.size.y < 0.01f)
                    sourceBounds.Expand(new Vector3(0f, silhouetteHeight, 0f));
                hasSourceBounds = true;
            }
            else if (!TryBuildFootprintFromBounds(scratchPoints, out sourceBounds))
            {
                generatedMesh.Clear();
                return;
            }
            else
                hasSourceBounds = true;

            if (shape == SceneBoundaryProxyShape.ClosedBoundsShell)
                BuildClosedBoundsShellMesh(hasSourceBounds, sourceBounds);
            else if (shape == SceneBoundaryProxyShape.RoomSilhouette)
                BuildRoomSilhouetteMesh(scratchPoints, hasSourceBounds, sourceBounds);
            else
                BuildSkirtMesh(scratchPoints);
        }

        private void Reset()
        {
            shape = SceneBoundaryProxyShape.ClosedBoundsShell;
            height = 0.18f;
            verticalOffset = 0.01f;
            outset = 0.08f;
            useProjectedSilhouette = false;
            silhouetteCamera = Camera.main;
            silhouetteHeight = 2.4f;
            topOffset = 0.02f;
            includeSideWalls = false;
            includeRightSideWall = true;
            rightSideWallEdgeIndex = -1;
            includeOpenEdgeRightCap = true;
            openEdgeRightCapStart = 0.5f;
            edgeRibbonThickness = 0.02f;
            rightCapTopLift = 0.035f;
            openEdgeIndex = -1;
            Rebuild();
        }

        private void OnValidate()
        {
            height = Mathf.Max(0.01f, height);
            outset = Mathf.Max(0f, outset);
            silhouetteHeight = Mathf.Max(0.01f, silhouetteHeight);
            openEdgeRightCapStart = Mathf.Clamp01(openEdgeRightCapStart);
            edgeRibbonThickness = Mathf.Max(0.001f, edgeRibbonThickness);
            rightCapTopLift = Mathf.Max(0f, rightCapTopLift);
            Rebuild();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void LateUpdate()
        {
            if (!useProjectedSilhouette || shape != SceneBoundaryProxyShape.RoomSilhouette)
                return;

            var camera = ResolveSilhouetteCamera();
            if (camera == null)
                return;

            var viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            if (Approximately(lastSilhouetteViewProjection, viewProjection))
                return;

            Rebuild();
        }

        private void EnsureMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = MeshName,
                    hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
                };
            }

            meshFilter.sharedMesh = generatedMesh;
        }

        private void ConfigureRenderer()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.allowOcclusionWhenDynamic = false;
        }

        private bool TryBuildFootprintFromBounds(List<Vector3> output, out Bounds bounds)
        {
            bounds = default;
            if (sourceRendererRoot == null)
                return false;

            var renderers = sourceRendererRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return false;

            var hasBounds = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds)
                return false;

            var min = bounds.min;
            var max = bounds.max;
            var center = bounds.center;
            var y = min.y + verticalOffset;

            output.Add(OutsetPoint(new Vector3(min.x, y, min.z), center));
            output.Add(OutsetPoint(new Vector3(max.x, y, min.z), center));
            output.Add(OutsetPoint(new Vector3(max.x, y, max.z), center));
            output.Add(OutsetPoint(new Vector3(min.x, y, max.z), center));
            return true;
        }

        private Vector3 OutsetPoint(Vector3 point, Vector3 center)
        {
            var direction = point - center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return point;

            return point + direction.normalized * outset;
        }

        private static Bounds GetPointBounds(IReadOnlyList<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < points.Count; i++)
                bounds.Encapsulate(points[i]);
            return bounds;
        }

        private void BuildSkirtMesh(IReadOnlyList<Vector3> worldPoints)
        {
            var count = worldPoints.Count;
            if (count < 3)
            {
                generatedMesh.Clear();
                return;
            }

            var vertices = new Vector3[count * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[count * 6];
            var baseY = transform.InverseTransformPoint(worldPoints[0]).y;

            for (var i = 0; i < count; i++)
            {
                var local = transform.InverseTransformPoint(worldPoints[i]);
                local.y = baseY;
                vertices[i * 2] = local;
                vertices[i * 2 + 1] = local + Vector3.up * height;
                uvs[i * 2] = new Vector2(i / (float)count, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)count, 1f);
            }

            var triangleIndex = 0;
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var bottomA = i * 2;
                var topA = bottomA + 1;
                var bottomB = next * 2;
                var topB = bottomB + 1;

                triangles[triangleIndex++] = bottomA;
                triangles[triangleIndex++] = topA;
                triangles[triangleIndex++] = topB;
                triangles[triangleIndex++] = bottomA;
                triangles[triangleIndex++] = topB;
                triangles[triangleIndex++] = bottomB;
            }

            generatedMesh.Clear();
            generatedMesh.SetVertices(vertices);
            generatedMesh.SetUVs(0, uvs);
            generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();
        }

        private void BuildRoomSilhouetteMesh(IReadOnlyList<Vector3> worldPoints, bool hasSourceBounds, Bounds sourceBounds)
        {
            var count = worldPoints.Count;
            if (count < 3)
            {
                generatedMesh.Clear();
                return;
            }

            var localFootprint = new Vector3[count];
            var baseY = transform.InverseTransformPoint(worldPoints[0]).y;
            for (var i = 0; i < count; i++)
            {
                localFootprint[i] = transform.InverseTransformPoint(worldPoints[i]);
                localFootprint[i].y = baseY;
            }

            var topY = hasSourceBounds
                ? transform.InverseTransformPoint(new Vector3(sourceBounds.center.x, sourceBounds.max.y + topOffset, sourceBounds.center.z)).y
                : baseY + silhouetteHeight;
            topY = Mathf.Max(baseY + 0.01f, topY);

            if (useProjectedSilhouette && BuildProjectedSilhouetteMesh(localFootprint, baseY, topY))
                return;

            var openEdge = ResolveOpenEdgeIndex(worldPoints);
            var vertices = new List<Vector3>(count * 4);
            var triangles = new List<int>((count - 2) * 3 + count * 6);

            // A continuous floor fill prevents the outline pass from treating the floor border as separate strips.
            var floorStart = vertices.Count;
            for (var i = 0; i < count; i++)
                vertices.Add(localFootprint[i]);

            for (var i = 1; i < count - 1; i++)
            {
                triangles.Add(floorStart);
                triangles.Add(floorStart + i);
                triangles.Add(floorStart + i + 1);
            }

            var backWallEdge = count == 4 && openEdge >= 0
                ? (openEdge + 2) % count
                : -1;
            var rightWallEdge = count == 4 && openEdge >= 0
                ? ResolveRightSideEdge(localFootprint, openEdge, backWallEdge, rightSideWallEdgeIndex)
                : -1;
            for (var i = 0; i < count; i++)
            {
                if (i == openEdge)
                    continue;
                if (!includeSideWalls &&
                    backWallEdge >= 0 &&
                    i != backWallEdge &&
                    (!includeRightSideWall || i != rightWallEdge))
                    continue;

                var next = (i + 1) % count;
                var bottomA = localFootprint[i];
                var bottomB = localFootprint[next];
                var topA = bottomA;
                var topB = bottomB;
                topA.y = topY;
                topB.y = topY;

                var wallStart = vertices.Count;
                vertices.Add(bottomA);
                vertices.Add(topA);
                vertices.Add(topB);
                vertices.Add(bottomB);

                triangles.Add(wallStart);
                triangles.Add(wallStart + 1);
                triangles.Add(wallStart + 2);
                triangles.Add(wallStart);
                triangles.Add(wallStart + 2);
                triangles.Add(wallStart + 3);
            }

            if (includeOpenEdgeRightCap && openEdge >= 0 && count == 4)
                AddOpenEdgeRightCap(vertices, triangles, localFootprint, openEdge, topY);

            generatedMesh.Clear();
            generatedMesh.SetVertices(vertices);
            generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();
        }

        private void BuildClosedBoundsShellMesh(bool hasSourceBounds, Bounds sourceBounds)
        {
            if (!hasSourceBounds)
            {
                generatedMesh.Clear();
                return;
            }

            var min = sourceBounds.min;
            var max = sourceBounds.max;
            var horizontalOutset = Mathf.Max(0f, outset);
            min.x -= horizontalOutset;
            min.z -= horizontalOutset;
            max.x += horizontalOutset;
            max.z += horizontalOutset;
            min.y += verticalOffset;
            max.y += topOffset;
            if (max.y <= min.y + 0.01f)
                max.y = min.y + Mathf.Max(0.01f, silhouetteHeight);

            var worldVertices = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z)
            };

            var vertices = new Vector3[worldVertices.Length];
            for (var i = 0; i < worldVertices.Length; i++)
                vertices[i] = transform.InverseTransformPoint(worldVertices[i]);

            // The shell is intentionally closed. The mask should describe the whole room volume,
            // so screen-space dilation only sees the outer silhouette instead of interior seams.
            var triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };

            generatedMesh.Clear();
            generatedMesh.SetVertices(vertices);
            generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();
        }

        private bool BuildProjectedSilhouetteMesh(IReadOnlyList<Vector3> localFootprint, float baseY, float topY)
        {
            var camera = ResolveSilhouetteCamera();
            if (camera == null || localFootprint.Count < 3)
                return false;

            var projected = new List<ProjectedPoint>(localFootprint.Count * 2);
            for (var i = 0; i < localFootprint.Count; i++)
            {
                var bottom = localFootprint[i];
                bottom.y = baseY;
                AddProjectedPoint(projected, camera, bottom);

                var top = localFootprint[i];
                top.y = topY;
                AddProjectedPoint(projected, camera, top);
            }

            if (projected.Count < 3)
                return false;

            var hull = BuildScreenHull(projected);
            if (hull.Count < 3)
                return false;

            var vertices = new List<Vector3>(hull.Count + 1);
            var triangles = new List<int>(hull.Count * 3);
            var center = Vector3.zero;
            for (var i = 0; i < hull.Count; i++)
                center += hull[i].Local;
            center /= hull.Count;

            vertices.Add(center);
            for (var i = 0; i < hull.Count; i++)
                vertices.Add(hull[i].Local);

            for (var i = 0; i < hull.Count; i++)
            {
                triangles.Add(0);
                triangles.Add(i + 1);
                triangles.Add((i + 1) % hull.Count + 1);
            }

            generatedMesh.Clear();
            generatedMesh.SetVertices(vertices);
            generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();
            lastSilhouetteViewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            return true;
        }

        private void AddProjectedPoint(List<ProjectedPoint> projected, Camera camera, Vector3 local)
        {
            var world = transform.TransformPoint(local);
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
                return;

            projected.Add(new ProjectedPoint(new Vector2(screen.x, screen.y), local));
        }

        private static List<ProjectedPoint> BuildScreenHull(List<ProjectedPoint> points)
        {
            points.Sort((a, b) =>
            {
                var xCompare = a.Screen.x.CompareTo(b.Screen.x);
                return xCompare != 0 ? xCompare : a.Screen.y.CompareTo(b.Screen.y);
            });

            var unique = new List<ProjectedPoint>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                if (unique.Count > 0 && (unique[unique.Count - 1].Screen - points[i].Screen).sqrMagnitude < 0.01f)
                    continue;

                unique.Add(points[i]);
            }

            if (unique.Count <= 3)
                return unique;

            var hull = new List<ProjectedPoint>(unique.Count * 2);
            for (var i = 0; i < unique.Count; i++)
            {
                while (hull.Count >= 2 && ScreenCross(hull[hull.Count - 2], hull[hull.Count - 1], unique[i]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(unique[i]);
            }

            var lowerCount = hull.Count;
            for (var i = unique.Count - 2; i >= 0; i--)
            {
                while (hull.Count > lowerCount && ScreenCross(hull[hull.Count - 2], hull[hull.Count - 1], unique[i]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(unique[i]);
            }

            if (hull.Count > 1)
                hull.RemoveAt(hull.Count - 1);

            return hull;
        }

        private static float ScreenCross(ProjectedPoint origin, ProjectedPoint a, ProjectedPoint b)
        {
            var oa = a.Screen - origin.Screen;
            var ob = b.Screen - origin.Screen;
            return oa.x * ob.y - oa.y * ob.x;
        }

        private Camera ResolveSilhouetteCamera()
        {
            if (silhouetteCamera != null)
                return silhouetteCamera;

            return Camera.main;
        }

        private static bool Approximately(Matrix4x4 a, Matrix4x4 b)
        {
            for (var i = 0; i < 16; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > 0.0001f)
                    return false;
            }

            return true;
        }

        private void AddOpenEdgeRightCap(List<Vector3> vertices, List<int> triangles, IReadOnlyList<Vector3> localFootprint, int openEdge, float topY)
        {
            var backEdge = (openEdge + 2) % localFootprint.Count;
            var backNext = (backEdge + 1) % localFootprint.Count;
            var backA = localFootprint[backEdge];
            var backB = localFootprint[backNext];
            var backRight = backA.x >= backB.x ? backA : backB;
            var backLeft = backA.x >= backB.x ? backB : backA;
            var start = Vector3.Lerp(backLeft, backRight, openEdgeRightCapStart);
            var thickness = Mathf.Max(0.001f, edgeRibbonThickness);

            var topStart = start;
            var topRight = backRight;
            topStart.y = topY + rightCapTopLift;
            topRight.y = topY + rightCapTopLift;

            var topStartLower = topStart + Vector3.down * thickness;
            var topRightLower = topRight + Vector3.down * thickness;
            AddQuad(vertices, triangles, topStart, topRight, topRightLower, topStartLower);

            var openNext = (openEdge + 1) % localFootprint.Count;
            var openA = localFootprint[openEdge];
            var openB = localFootprint[openNext];
            var openRight = openA.x >= openB.x ? openA : openB;
            var bottomRight = openRight;
            bottomRight.y = localFootprint[openEdge].y;
            var verticalInset = Vector3.left * thickness;

            var verticalTop = openRight;
            verticalTop.y = topY + rightCapTopLift;
            AddQuad(vertices, triangles, verticalTop, bottomRight, bottomRight + verticalInset, verticalTop + verticalInset);
        }

        private static int ResolveRightSideEdge(IReadOnlyList<Vector3> localFootprint, int openEdge, int backWallEdge, int preferredEdge)
        {
            if (preferredEdge >= 0 &&
                preferredEdge < localFootprint.Count &&
                preferredEdge != openEdge &&
                preferredEdge != backWallEdge)
                return preferredEdge;

            var bestEdge = -1;
            var bestX = float.NegativeInfinity;
            for (var i = 0; i < localFootprint.Count; i++)
            {
                if (i == openEdge || i == backWallEdge)
                    continue;

                var next = (i + 1) % localFootprint.Count;
                var averageX = (localFootprint[i].x + localFootprint[next].x) * 0.5f;
                if (averageX <= bestX)
                    continue;

                bestX = averageX;
                bestEdge = i;
            }

            return bestEdge;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private int ResolveOpenEdgeIndex(IReadOnlyList<Vector3> worldPoints)
        {
            var count = worldPoints.Count;
            if (count < 3)
                return -1;

            if (openEdgeIndex >= 0 && openEdgeIndex < count)
                return openEdgeIndex;

            var reference = openEdgeReference != null
                ? openEdgeReference.position
                : Camera.main != null
                    ? Camera.main.transform.position
                    : Vector3.zero;
            reference.y = 0f;

            var closestEdge = 0;
            var closestDistance = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var midpoint = (worldPoints[i] + worldPoints[next]) * 0.5f;
                midpoint.y = 0f;
                var distance = (midpoint - reference).sqrMagnitude;
                if (distance >= closestDistance)
                    continue;

                closestDistance = distance;
                closestEdge = i;
            }

            return closestEdge;
        }

        private readonly struct ProjectedPoint
        {
            public ProjectedPoint(Vector2 screen, Vector3 local)
            {
                Screen = screen;
                Local = local;
            }

            public Vector2 Screen { get; }
            public Vector3 Local { get; }
        }
    }
}
