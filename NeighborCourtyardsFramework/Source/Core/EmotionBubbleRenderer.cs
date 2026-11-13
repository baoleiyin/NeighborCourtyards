using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    // World overlays only. A pawn's portrait and appearance meshes are never changed.
    [StaticConstructorOnStartup]
    public static class EmotionBubbleRenderer
    {
        sealed class Display
        {
            public EmotionBubbleDef emotion;
            public int startedAt, side;
            public bool visible;
            public Mesh outline, fill, icon;
            public Material outlineMaterial, fillMaterial, iconMaterial;
            public void Dispose()
            {
                if (outline != null) UnityEngine.Object.Destroy(outline);
                if (fill != null) UnityEngine.Object.Destroy(fill);
                if (icon != null) UnityEngine.Object.Destroy(icon);
                if (outlineMaterial != null) UnityEngine.Object.Destroy(outlineMaterial);
                if (fillMaterial != null) UnityEngine.Object.Destroy(fillMaterial);
                if (iconMaterial != null) UnityEngine.Object.Destroy(iconMaterial);
                outline = fill = icon = null;
                outlineMaterial = fillMaterial = iconMaterial = null;
            }
        }
        struct Slot { public Rect bounds; public Vector3 position; }
        static readonly Dictionary<CompEmotionBubble, Display> displays = new Dictionary<CompEmotionBubble, Display>();
        static readonly List<CompEmotionBubble> candidates = new List<CompEmotionBubble>();
        static readonly List<CompEmotionBubble> expired = new List<CompEmotionBubble>();
        static readonly List<Slot> occupied = new List<Slot>();
        static readonly Comparison<CompEmotionBubble> rank = (a, b) => {
            int order = b.Active.priority.CompareTo(a.Active.priority);
            return order != 0 ? order : a.ParentPawn.thingIDNumber.CompareTo(b.ParentPawn.thingIDNumber);
        };
        static Map currentMap;
        static int layoutTick = -1;
        static CellRect layoutView;
        static Texture2D toggleIcon;
        public static Texture2D ToggleIcon => toggleIcon ?? (toggleIcon = MakeToggleIcon());

        static Texture2D MakeToggleIcon()
        {
            const int size = 64;
            Color ink = new Color(.30f, .19f, .13f), cream = new Color(1f, .98f, .91f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Color sum = Color.clear;
                int covered = 0;
                // Four coverage samples keep the small command icon smooth without an asset.
                for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++)
                {
                    Vector2 point = new Vector2((x + (sx + .5f) / 2f) / size * 1.24f - .62f, (y + (sy + .5f) / 2f) / size * 1.24f - .62f);
                    if (!InsideBubble(point, 0f)) continue;
                    bool dot = false;
                    for (int d = -1; d <= 1; d++)
                        if ((point - new Vector2(d * .19f, 0f)).sqrMagnitude <= .0025f) { dot = true; break; }
                    sum += dot || !InsideBubble(point, .045f) ? ink : cream;
                    covered++;
                }
                Color pixel = covered == 0 ? ink : sum / covered;
                pixel.a = covered / 4f; pixels[y * size + x] = pixel;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "NC_EmotionBubbleToggle", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }

        static bool InsideBubble(Vector2 point, float inset)
        {
            float radius = .25f - inset;
            Vector2 q = new Vector2(Mathf.Abs(point.x) - .25f, Mathf.Abs(point.y) - .17f);
            float dx = Mathf.Max(q.x, 0f), dy = Mathf.Max(q.y, 0f);
            if (Mathf.Sqrt(dx * dx + dy * dy) + Mathf.Min(Mathf.Max(q.x, q.y), 0f) <= radius) return true;
            Vector2 a = new Vector2(-.07f, -.42f + inset), b = new Vector2(-.25f + inset, -.55f + inset * 1.8f), c = new Vector2(-.24f + inset, -.42f + inset);
            float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b), ca = Cross(a - c, point - c);
            return ab >= 0f && bc >= 0f && ca >= 0f || ab <= 0f && bc <= 0f && ca <= 0f;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static void Release(CompEmotionBubble comp)
        {
            if (comp != null && displays.TryGetValue(comp, out Display display))
            { display.Dispose(); displays.Remove(comp); layoutTick = -1; }
        }

        static bool Eligible(CompEmotionBubble comp, Map map)
        {
            Pawn pawn = comp.ParentPawn;
            return comp.Active != null && comp.CanDisplay && comp.Props.style != null && pawn.Map == map &&
                !pawn.Position.Fogged(map) && (Find.CameraDriver == null || Find.CameraDriver.CurrentViewRect.ExpandedBy(2).Contains(pawn.Position));
        }

        static Vector3 Position(CompEmotionBubble comp, int side)
        {
            EmotionBubbleStyleDef style = comp.Props.style;
            Vector3 position = style.Anchor.Position(comp.ParentPawn, style);
            if (side < 0) position.x = comp.ParentPawn.DrawPos.x * 2f - position.x;
            position.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            return position;
        }

        static bool Available(Vector3 position, float size, out Slot slot)
        {
            slot = new Slot { position = position, bounds = new Rect(position.x - size * .58f, position.z - size * .65f, size * 1.16f, size * 1.21f) };
            int neighbours = 0;
            for (int i = 0; i < occupied.Count; i++)
            {
                Slot other = occupied[i];
                if (other.bounds.Overlaps(slot.bounds)) return false;
                float dx = position.x - other.position.x, dz = position.z - other.position.z;
                if (dx * dx + dz * dz < 4f && ++neighbours >= 3) return false;
            }
            return true;
        }

        static void Layout(Map map, int tick)
        {
            if (currentMap != map)
            {
                foreach (Display display in displays.Values) display.Dispose();
                displays.Clear(); currentMap = map; layoutTick = -1;
            }
            CellRect view = Find.CameraDriver == null ? new CellRect(0, 0, map.Size.x, map.Size.z) : Find.CameraDriver.CurrentViewRect;
            if (layoutTick == tick && layoutView.Equals(view)) return;
            layoutTick = tick; layoutView = view; candidates.Clear(); occupied.Clear(); expired.Clear();
            foreach (var pair in displays)
            {
                pair.Value.visible = false;
                if (!Eligible(pair.Key, map)) expired.Add(pair.Key);
            }
            for (int i = 0; i < expired.Count; i++)
            { displays[expired[i]].Dispose(); displays.Remove(expired[i]); }
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                CompEmotionBubble comp = pawns[i].TryGetComp<CompEmotionBubble>();
                if (comp != null && Eligible(comp, map)) candidates.Add(comp);
            }
            candidates.Sort(rank);
            for (int i = 0; i < candidates.Count; i++)
            {
                CompEmotionBubble comp = candidates[i];
                if (!displays.TryGetValue(comp, out Display display)) displays[comp] = display = new Display();
                if (display.emotion != comp.Active || display.startedAt != comp.StartedAt)
                {
                    display.Dispose(); display.emotion = comp.Active; display.startedAt = comp.StartedAt; display.side = 0;
                }
                float size = comp.Props.style.size * comp.Props.style.Anchor.Scale(comp.ParentPawn);
                // Once chosen, the side remains fixed for this bubble. Moving crowds suppress
                // overlaps rather than making the bubble jump between a pawn's ears.
                int side = display.side == 0 ? 1 : display.side;
                bool available = Available(Position(comp, side), size, out Slot slot);
                if (!available && display.side == 0)
                { side = -1; available = Available(Position(comp, side), size, out slot); }
                if (!available) continue;
                display.side = side; display.visible = true; occupied.Add(slot);
            }
        }

        public static void Draw(CompEmotionBubble comp)
        {
            Map map = comp.ParentPawn?.Map;
            if (map == null || map != Find.CurrentMap || !Eligible(comp, map)) { Release(comp); return; }
            Layout(map, Find.TickManager.TicksGame);
            if (!displays.TryGetValue(comp, out Display display) || !display.visible || comp.Opacity <= 0f) return;
            if (display.icon == null && !Create(display, comp.Props.style)) return;
            EmotionBubbleStyleDef style = comp.Props.style;
            float scale = style.size * style.Anchor.Scale(comp.ParentPawn) * comp.VisualScale;
            Vector3 position = Position(comp, display.side);
            Color outer = style.outlineColor, inner = style.fillColor;
            outer.a *= comp.Opacity; inner.a *= comp.Opacity;
            display.outlineMaterial.color = outer; display.fillMaterial.color = inner;
            display.iconMaterial.color = new Color(1f, 1f, 1f, comp.Opacity);
            Vector3 dimensions = new Vector3(scale, 1f, scale);
            Graphics.DrawMesh(display.outline, Matrix4x4.TRS(position, Quaternion.identity, dimensions), display.outlineMaterial, 0);
            position.y += .001f;
            Graphics.DrawMesh(display.fill, Matrix4x4.TRS(position, Quaternion.identity, dimensions), display.fillMaterial, 0);
            position.y += .001f;
            Graphics.DrawMesh(display.icon, Matrix4x4.TRS(position, Quaternion.identity, dimensions), display.iconMaterial, 0);
        }

        static bool Create(Display display, EmotionBubbleStyleDef style)
        {
            Texture2D texture = ContentFinder<Texture2D>.Get(display.emotion.texture, false);
            if (texture == null)
            {
                Log.WarningOnce("[NeighborCourtyards] Missing emotion bubble icon: " + display.emotion.texture, 918043 + display.emotion.shortHash);
                return false;
            }
            float inset = style.outline / style.size;
            display.outline = Bubble(0f, display.side, "EmotionBubble_Outline");
            display.fill = Bubble(inset, display.side, "EmotionBubble_Fill");
            Rect uv = display.emotion.uvRect;
            float radius = style.iconScale * .5f;
            float pixelWidth = texture.width * uv.width, pixelHeight = texture.height * uv.height;
            float longest = Mathf.Max(pixelWidth, pixelHeight);
            float rx = radius * pixelWidth / longest, rz = radius * pixelHeight / longest;
            display.icon = new Mesh {
                name = "EmotionBubble_Icon",
                vertices = new[] { new Vector3(-rx, 0f, -rz), new Vector3(-rx, 0f, rz), new Vector3(rx, 0f, rz), new Vector3(rx, 0f, -rz) },
                uv = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            Finish(display.icon);
            // Each displayed pawn owns its colors. No animated color is added to MaterialPool.
            display.outlineMaterial = Material(Texture2D.whiteTexture, "EmotionBubble_Outline");
            display.fillMaterial = Material(Texture2D.whiteTexture, "EmotionBubble_Fill");
            display.iconMaterial = Material(texture, "EmotionBubble_Icon");
            return true;
        }

        static Material Material(Texture2D texture, string name) => new Material(ShaderDatabase.Transparent) { name = name, mainTexture = texture, color = Color.white };

        static Mesh Bubble(float inset, int side, string name)
        {
            const int steps = 7;
            float w = .5f - inset, h = .42f - inset, radius = Mathf.Max(.03f, .25f - inset);
            var vertices = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { new Vector2(.5f, .5f) };
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = corner < 2 ? w - radius : -w + radius;
                float cz = corner == 0 || corner == 3 ? h - radius : -h + radius;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (90f - 90f * corner - i * 90f / steps) * Mathf.Deg2Rad;
                    vertices.Add(new Vector3((cx + radius * Mathf.Cos(angle)) * side, 0f, cz + radius * Mathf.Sin(angle)));
                    uv.Add(new Vector2(.5f, .5f));
                }
                if (corner == 1)
                {
                    vertices.Add(new Vector3(-.07f * side, 0f, -h));
                    vertices.Add(new Vector3((-.25f + inset) * side, 0f, -.55f + inset * 1.8f));
                    vertices.Add(new Vector3((-.24f + inset) * side, 0f, -h));
                    uv.Add(new Vector2(.5f, .5f)); uv.Add(new Vector2(.5f, .5f)); uv.Add(new Vector2(.5f, .5f));
                }
            }
            int count = vertices.Count - 1;
            var triangles = new int[count * 3];
            for (int i = 0; i < count; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = side > 0 ? i + 1 : (i + 1) % count + 1;
                triangles[i * 3 + 2] = side > 0 ? (i + 1) % count + 1 : i + 1;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.triangles = triangles; Finish(mesh); return mesh;
        }

        static void Finish(Mesh mesh)
        {
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshPool.EnsureMetaDataCached(mesh, new Vector2(mesh.bounds.size.x, mesh.bounds.size.z), false);
        }
    }
}
