using System.Collections.Generic;
using UnityEngine;

namespace A3R.ARGuidance
{
    /// <summary>
    /// ProceduralMeshUtility — Generates modern, polished AR guidance geometry and emissive materials
    /// for directional chevron paths, reticles, beacons, and target highlights.
    /// </summary>
    public static class ProceduralMeshUtility
    {
        /// <summary>
        /// Creates a modern, open-center V-shaped AR navigation chevron mesh in the XZ plane pointing along +Z.
        /// Features two tapered angled arms, an open center notch, and a clean extruded 3D bevel.
        /// </summary>
        public static Mesh CreateChevronMesh(
            float width = 0.055f,
            float length = 0.045f,
            float armThickness = 0.012f,
            float height = 0.005f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "A3R_ModernChevronMesh";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float notchZ = length - (armThickness * 1.45f);

            // 6 boundary vertices of the V-shape (in 2D XZ)
            // 0: Forward Tip
            // 1: Right Outer Wing
            // 2: Right Inner Wing
            // 3: Center Inner Notch
            // 4: Left Inner Wing
            // 5: Left Outer Wing
            Vector2[] pts = new Vector2[]
            {
                new Vector2(0f, length),                            // 0
                new Vector2(halfW, 0f),                             // 1
                new Vector2(halfW - armThickness, 0f),              // 2
                new Vector2(0f, notchZ),                            // 3
                new Vector2(-halfW + armThickness, 0f),             // 4
                new Vector2(-halfW, 0f)                             // 5
            };

            // Top Cap (+Y)
            int topStart = verts.Count;
            for (int i = 0; i < pts.Length; i++)
            {
                verts.Add(new Vector3(pts[i].x, halfH, pts[i].y));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(pts[i].x / width + 0.5f, pts[i].y / length));
            }

            // Right Arm Quads (top): (0, 1, 2) and (0, 2, 3)
            tris.Add(topStart + 0); tris.Add(topStart + 1); tris.Add(topStart + 2);
            tris.Add(topStart + 0); tris.Add(topStart + 2); tris.Add(topStart + 3);

            // Left Arm Quads (top): (0, 3, 4) and (0, 4, 5)
            tris.Add(topStart + 0); tris.Add(topStart + 3); tris.Add(topStart + 4);
            tris.Add(topStart + 0); tris.Add(topStart + 4); tris.Add(topStart + 5);

            // Bottom Cap (-Y)
            int botStart = verts.Count;
            for (int i = 0; i < pts.Length; i++)
            {
                verts.Add(new Vector3(pts[i].x, -halfH, pts[i].y));
                norms.Add(Vector3.down);
                uvs.Add(new Vector2(pts[i].x / width + 0.5f, pts[i].y / length));
            }

            // Right Arm (bottom, reversed winding)
            tris.Add(botStart + 0); tris.Add(botStart + 2); tris.Add(botStart + 1);
            tris.Add(botStart + 0); tris.Add(botStart + 3); tris.Add(botStart + 2);

            // Left Arm (bottom, reversed winding)
            tris.Add(botStart + 0); tris.Add(botStart + 4); tris.Add(botStart + 3);
            tris.Add(botStart + 0); tris.Add(botStart + 5); tris.Add(botStart + 4);

            // Side Walls around the 6 edges
            for (int i = 0; i < pts.Length; i++)
            {
                int next = (i + 1) % pts.Length;
                Vector3 p1Top = new Vector3(pts[i].x, halfH, pts[i].y);
                Vector3 p2Top = new Vector3(pts[next].x, halfH, pts[next].y);
                Vector3 p1Bot = new Vector3(pts[i].x, -halfH, pts[i].y);
                Vector3 p2Bot = new Vector3(pts[next].x, -halfH, pts[next].y);

                Vector3 edge = p2Top - p1Top;
                Vector3 sideNorm = Vector3.Cross(edge, Vector3.up).normalized;

                int wStart = verts.Count;
                verts.Add(p1Top); norms.Add(sideNorm); uvs.Add(new Vector2(0f, 1f));
                verts.Add(p2Top); norms.Add(sideNorm); uvs.Add(new Vector2(1f, 1f));
                verts.Add(p2Bot); norms.Add(sideNorm); uvs.Add(new Vector2(1f, 0f));
                verts.Add(p1Bot); norms.Add(sideNorm); uvs.Add(new Vector2(0f, 0f));

                tris.Add(wStart + 0); tris.Add(wStart + 1); tris.Add(wStart + 2);
                tris.Add(wStart + 0); tris.Add(wStart + 2); tris.Add(wStart + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a modern vertical holographic target beacon mesh featuring a thin vertical beam
        /// and a top floating diamond marker.
        /// </summary>
        public static Mesh CreateBeaconMesh(float height = 0.080f, float beamRadius = 0.0015f, float markerRadius = 0.006f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "A3R_TargetBeaconMesh";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // 1. Thin vertical cylinder beam (8 segments)
            int segments = 8;
            int beamStart = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                Vector3 norm = new Vector3(cos, 0f, sin);

                verts.Add(new Vector3(cos * beamRadius, 0f, sin * beamRadius));
                norms.Add(norm);
                uvs.Add(new Vector2((float)i / segments, 0f));

                verts.Add(new Vector3(cos * beamRadius, height, sin * beamRadius));
                norms.Add(norm);
                uvs.Add(new Vector2((float)i / segments, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int c0 = beamStart + i * 2;
                int c1 = beamStart + i * 2 + 1;
                int n0 = beamStart + (i + 1) * 2;
                int n1 = beamStart + (i + 1) * 2 + 1;

                tris.Add(c0); tris.Add(c1); tris.Add(n1);
                tris.Add(c0); tris.Add(n1); tris.Add(n0);
            }

            // 2. Top floating diamond marker (Octahedron at height)
            Vector3 center = new Vector3(0f, height + markerRadius * 1.1f, 0f);
            Vector3 top = center + Vector3.up * markerRadius * 1.5f;
            Vector3 bottom = center + Vector3.down * markerRadius * 1.5f;
            Vector3[] equator = new Vector3[]
            {
                center + Vector3.forward * markerRadius,
                center + Vector3.right * markerRadius,
                center + Vector3.back * markerRadius,
                center + Vector3.left * markerRadius
            };

            int topIdx = verts.Count;
            verts.Add(top); norms.Add(Vector3.up); uvs.Add(new Vector2(0.5f, 1f));
            int botIdx = verts.Count;
            verts.Add(bottom); norms.Add(Vector3.down); uvs.Add(new Vector2(0.5f, 0f));

            int eqStart = verts.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = (equator[i] - center).normalized;
                verts.Add(equator[i]);
                norms.Add(dir);
                uvs.Add(new Vector2(i * 0.25f, 0.5f));
            }

            for (int i = 0; i < 4; i++)
            {
                int cur = eqStart + i;
                int next = eqStart + ((i + 1) % 4);

                // Top cone
                tris.Add(topIdx); tris.Add(cur); tris.Add(next);
                // Bottom cone
                tris.Add(botIdx); tris.Add(next); tris.Add(cur);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a modern AR target reticle mesh featuring:
        /// - 4 segmented outer arcs with corner gaps
        /// - 4 outer corner framing brackets (L-brackets)
        /// - 1 inner concentric alignment ring
        /// - 4 cardinal crosshair alignment ticks
        /// - 1 central target anchor dot
        /// </summary>
        public static Mesh CreateReticleMesh(
            float innerRadius = 0.018f,
            float outerRadius = 0.038f,
            float thickness = 0.003f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "A3R_ModernARReticle";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // 1. Outer 4-Segmented Ring (Quadrant Arcs from 12° to 78° per quadrant)
            int arcSegments = 10;
            float outR1 = outerRadius - thickness;
            float outR2 = outerRadius;

            for (int q = 0; q < 4; q++)
            {
                float baseDeg = q * 90f;
                float startAngle = (baseDeg + 12f) * Mathf.Deg2Rad;
                float endAngle = (baseDeg + 78f) * Mathf.Deg2Rad;

                int arcStart = verts.Count;
                for (int i = 0; i <= arcSegments; i++)
                {
                    float t = (float)i / arcSegments;
                    float angle = Mathf.Lerp(startAngle, endAngle, t);
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    verts.Add(new Vector3(cos * outR1, 0f, sin * outR1));
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(t, 0f));

                    verts.Add(new Vector3(cos * outR2, 0f, sin * outR2));
                    norms.Add(Vector3.up);
                    uvs.Add(new Vector2(t, 1f));
                }

                for (int i = 0; i < arcSegments; i++)
                {
                    int c0 = arcStart + i * 2;
                    int c1 = arcStart + i * 2 + 1;
                    int n0 = arcStart + (i + 1) * 2;
                    int n1 = arcStart + (i + 1) * 2 + 1;

                    tris.Add(c0); tris.Add(c1); tris.Add(n1);
                    tris.Add(c0); tris.Add(n1); tris.Add(n0);
                }
            }

            // 2. Corner Framing Brackets (L-brackets at 45°, 135°, 225°, 315°)
            float bracketRadius = outerRadius + 0.005f;
            float bracketArm = 0.008f;
            float bracketThick = thickness * 0.85f;

            for (int q = 0; q < 4; q++)
            {
                float cornerAngle = (45f + q * 90f) * Mathf.Deg2Rad;
                Vector3 cornerPos = new Vector3(Mathf.Cos(cornerAngle) * bracketRadius, 0f, Mathf.Sin(cornerAngle) * bracketRadius);

                // Arm 1 (tangential)
                Vector3 tanDir = new Vector3(-Mathf.Sin(cornerAngle), 0f, Mathf.Cos(cornerAngle));
                // Arm 2 (radial inward)
                Vector3 radDir = -cornerPos.normalized;

                // Arm 1 quad
                int b1Start = verts.Count;
                verts.Add(cornerPos - tanDir * bracketArm * 0.5f - radDir * bracketThick * 0.5f); norms.Add(Vector3.up); uvs.Add(new Vector2(0f, 0f));
                verts.Add(cornerPos + tanDir * bracketArm * 0.5f - radDir * bracketThick * 0.5f); norms.Add(Vector3.up); uvs.Add(new Vector2(1f, 0f));
                verts.Add(cornerPos + tanDir * bracketArm * 0.5f + radDir * bracketThick * 0.5f); norms.Add(Vector3.up); uvs.Add(new Vector2(1f, 1f));
                verts.Add(cornerPos - tanDir * bracketArm * 0.5f + radDir * bracketThick * 0.5f); norms.Add(Vector3.up); uvs.Add(new Vector2(0f, 1f));

                tris.Add(b1Start + 0); tris.Add(b1Start + 1); tris.Add(b1Start + 2);
                tris.Add(b1Start + 0); tris.Add(b1Start + 2); tris.Add(b1Start + 3);
            }

            // 3. Inner Ring (Thin continuous circle)
            int innerSegments = 32;
            float inR1 = innerRadius;
            float inR2 = innerRadius + thickness * 0.6f;
            int inStart = verts.Count;

            for (int i = 0; i <= innerSegments; i++)
            {
                float angle = (float)i / innerSegments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts.Add(new Vector3(cos * inR1, 0f, sin * inR1));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2((float)i / innerSegments, 0f));

                verts.Add(new Vector3(cos * inR2, 0f, sin * inR2));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2((float)i / innerSegments, 1f));
            }

            for (int i = 0; i < innerSegments; i++)
            {
                int c0 = inStart + i * 2;
                int c1 = inStart + i * 2 + 1;
                int n0 = inStart + (i + 1) * 2;
                int n1 = inStart + (i + 1) * 2 + 1;

                tris.Add(c0); tris.Add(c1); tris.Add(n1);
                tris.Add(c0); tris.Add(n1); tris.Add(n0);
            }

            // 4. Four Inward Crosshair Ticks (At 0°, 90°, 180°, 270°)
            float tickLen = (outR1 - inR2) * 0.7f;
            float halfTickW = thickness * 0.45f;
            float tickRStart = inR2 + (outR1 - inR2) * 0.15f;
            float tickREnd = tickRStart + tickLen;

            for (int q = 0; q < 4; q++)
            {
                float angle = q * 90f * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 right = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                Vector3 p0 = forward * tickRStart - right * halfTickW;
                Vector3 p1 = forward * tickRStart + right * halfTickW;
                Vector3 p2 = forward * tickREnd + right * halfTickW;
                Vector3 p3 = forward * tickREnd - right * halfTickW;

                int tStart = verts.Count;
                verts.Add(p0); norms.Add(Vector3.up); uvs.Add(new Vector2(0f, 0f));
                verts.Add(p1); norms.Add(Vector3.up); uvs.Add(new Vector2(1f, 0f));
                verts.Add(p2); norms.Add(Vector3.up); uvs.Add(new Vector2(1f, 1f));
                verts.Add(p3); norms.Add(Vector3.up); uvs.Add(new Vector2(0f, 1f));

                tris.Add(tStart + 0); tris.Add(tStart + 1); tris.Add(tStart + 2);
                tris.Add(tStart + 0); tris.Add(tStart + 2); tris.Add(tStart + 3);
            }

            // 5. Center Target Dot
            int dotSegments = 16;
            float dotR = thickness * 1.2f;
            int dotCenter = verts.Count;
            verts.Add(Vector3.zero);
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int dotRingStart = verts.Count;
            for (int i = 0; i <= dotSegments; i++)
            {
                float angle = (float)i / dotSegments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * dotR, 0f, Mathf.Sin(angle) * dotR));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2((Mathf.Cos(angle) + 1f) * 0.5f, (Mathf.Sin(angle) + 1f) * 0.5f));
            }

            for (int i = 0; i < dotSegments; i++)
            {
                tris.Add(dotCenter);
                tris.Add(dotRingStart + i);
                tris.Add(dotRingStart + i + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates an arrow mesh for backward compatibility.
        /// </summary>
        public static Mesh CreateArrowMesh(
            float length = 0.085f,
            float width = 0.026f,
            float stemWidth = 0.010f,
            float thickness = 0.007f,
            float headRatio = 0.55f)
        {
            return CreateChevronMesh(width * 2f, length, stemWidth * 1.5f, thickness);
        }

        public static Mesh CreateArrowMesh(
            float shaftLength,
            float shaftRadius,
            float headLength,
            float headRadius,
            int segments)
        {
            return CreateChevronMesh(headRadius * 3.5f, shaftLength + headLength, shaftRadius * 2.5f, 0.006f);
        }

        public static Mesh CreateRingMesh(float innerRadius = 0.02f, float outerRadius = 0.035f, int segments = 32)
        {
            return CreateReticleMesh(innerRadius, outerRadius, (outerRadius - innerRadius) * 0.25f);
        }

        public static Material CreateMaterial(Color color, bool transparent = false, bool emissive = false)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = null;
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                mat = new Material(Shader.Find("Hidden/InternalErrorShader")); // absolute fallback
            }

            // Force fully opaque and visible
            color.a = 1.0f; 
            
            if (mat != null)
            {
                mat.color = color;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            }
            
            return mat;
        }

        public static void SetMaterialColor(Material mat, Color color, float emissionMult = 1.6f)
        {
            if (mat == null) return;

            mat.color = color;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                Color em = new Color(color.r, color.g, color.b, 1.0f) * emissionMult;
                mat.SetColor("_EmissionColor", em);
            }
        }

        /// <summary>
        /// Creates a clean, flat rectangular outline mesh in the XZ plane with normal pointing +Y.
        /// Perfect for lightweight, mobile-safe markerless AR placement previews.
        /// </summary>
        public static Mesh CreateFootprintOutlineMesh(float width, float depth, float lineThickness = 0.0025f, float yElevation = 0f)
        {
            Mesh mesh = new Mesh();
            mesh.name = "A3R_BreadboardFootprintOutline";

            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            float t = lineThickness;
            float y = yElevation; // Explicit elevation in mesh local space

            void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int start = verts.Count;
                verts.Add(p0); norms.Add(Vector3.up); uvs.Add(new Vector2(0, 0));
                verts.Add(p1); norms.Add(Vector3.up); uvs.Add(new Vector2(1, 0));
                verts.Add(p2); norms.Add(Vector3.up); uvs.Add(new Vector2(1, 1));
                verts.Add(p3); norms.Add(Vector3.up); uvs.Add(new Vector2(0, 1));

                // Front face
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);

                // Back face (double-sided for mobile AR visibility at any camera angle)
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
            }

            // Outer 4 perimeter bars (exact footprint boundary: -halfW..+halfW x -halfD..+halfD)
            // Top (+Z)
            AddQuad(new Vector3(-halfW, y, halfD - t), new Vector3(halfW, y, halfD - t), new Vector3(halfW, y, halfD), new Vector3(-halfW, y, halfD));
            // Bottom (-Z)
            AddQuad(new Vector3(-halfW, y, -halfD), new Vector3(halfW, y, -halfD), new Vector3(halfW, y, -halfD + t), new Vector3(-halfW, y, -halfD + t));
            // Left (-X)
            AddQuad(new Vector3(-halfW, y, -halfD + t), new Vector3(-halfW + t, y, -halfD + t), new Vector3(-halfW + t, y, halfD - t), new Vector3(-halfW, y, halfD - t));
            // Right (+X)
            AddQuad(new Vector3(halfW - t, y, -halfD + t), new Vector3(halfW, y, -halfD + t), new Vector3(halfW, y, halfD - t), new Vector3(halfW - t, y, halfD - t));

            // Center crosshair / alignment reticle
            float cSize = 0.010f;
            AddQuad(new Vector3(-cSize, y, -t * 0.5f), new Vector3(cSize, y, -t * 0.5f), new Vector3(cSize, y, t * 0.5f), new Vector3(-cSize, y, t * 0.5f));
            AddQuad(new Vector3(-t * 0.5f, y, -cSize), new Vector3(t * 0.5f, y, -cSize), new Vector3(t * 0.5f, y, cSize), new Vector3(-t * 0.5f, y, cSize));

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
