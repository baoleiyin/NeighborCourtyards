using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public enum NeighborPose { Idle, Walk, Work, Clean, Haul, Eat, Sleep, Downed }
    public static class NeighborPresentation
    {
        public static NeighborPose Pose(Pawn pawn)
        {
            if (pawn.Downed) return NeighborPose.Downed;
            if (pawn.pather?.Moving == true) return NeighborPose.Walk;
            if (pawn.CurJobDef == JobDefOf.LayDown && pawn.GetPosture() != PawnPosture.Standing) return NeighborPose.Sleep;
            if (pawn.CurJobDef == JobDefOf.Ingest) return NeighborPose.Eat;
            if (pawn.CurJobDef == JobDefOf.Clean) return NeighborPose.Clean;
            if (pawn.CurJobDef == JobDefOf.HaulToCell || pawn.CurJobDef == JobDefOf.HaulToContainer) return NeighborPose.Haul;
            return pawn.CurJob?.workGiverDef != null ? NeighborPose.Work : NeighborPose.Idle;
        }
    }
    public sealed class NeighborSpriteAnimation
    {
        public NeighborPose pose;
        public int ticksPerFrame = 12;
        // Zero-based row-major cell indexes. West mirrors east.
        public List<int> south = new List<int>(), east = new List<int>(), north = new List<int>();
    }
    public sealed class NeighborSpriteProperties : PawnRenderNodeProperties
    {
        public int columns = 3, rows = 2, south = 0, east = 1, north = 2;
        public Vector2 meshSize = new Vector2(1, 1.5f);
        public Vector3 headOffset = new Vector3(0, 0, .30f);
        public List<NeighborSpriteAnimation> animations = new List<NeighborSpriteAnimation>();
        internal readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        public NeighborSpriteProperties() { nodeClass = typeof(NeighborSpriteNode); }
    }
    public sealed class NeighborSpriteNode : PawnRenderNode
    {
        public NeighborSpriteNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree) { }
        public override Graphic GraphicFor(Pawn pawn) => GraphicDatabase.Get<Graphic_Single>(Props.texPath, ShaderDatabase.Cutout, Vector2.one, ColorFor(pawn));
        public override GraphicMeshSet MeshSetFor(Pawn pawn) => MeshPool.GetMeshSetForSize(1, 1);
        public override Mesh GetMesh(PawnDrawParms parms)
        {
            var p = (NeighborSpriteProperties)Props;
            int columns = Mathf.Max(1, p.columns), rows = Mathf.Max(1, p.rows);
            bool west = parms.facing == Rot4.West;
            int cell = parms.facing == Rot4.North ? p.north : parms.facing.IsHorizontal ? p.east : p.south;
            var animation = parms.Portrait ? null : p.animations?.FirstOrDefault(a => a != null && a.pose == NeighborPresentation.Pose(parms.pawn));
            var frames = parms.facing == Rot4.North ? animation?.north : parms.facing.IsHorizontal ? animation?.east : animation?.south;
            if (frames?.Count > 0)
                cell = frames[((Find.TickManager?.TicksGame ?? 0) / Math.Max(1, animation.ticksPerFrame)) % frames.Count];
            if (p.columns < 1 || p.rows < 1 || cell < 0 || cell >= (long)columns * rows || !DefinitionValidation.Positive(p.meshSize.x) || !DefinitionValidation.Positive(p.meshSize.y))
            {
                Log.ErrorOnce("Invalid neighbor sprite grid, frame or mesh size for " + parms.pawn.def.defName, parms.pawn.def.shortHash + 762913);
                cell = 0;
            }
            int key = cell * 2 + (west ? 1 : 0);
            if (p.meshes.TryGetValue(key, out var mesh)) return mesh;
            float left = (cell % columns) / (float)columns, right = left + 1f / columns;
            float top = 1f - (cell / columns) / (float)rows, bottom = top - 1f / rows;
            if (west) { float swap = left; left = right; right = swap; }
            float x = DefinitionValidation.Positive(p.meshSize.x) ? p.meshSize.x / 2 : .5f;
            float z = DefinitionValidation.Positive(p.meshSize.y) ? p.meshSize.y / 2 : .75f;
            mesh = new Mesh { name = "Neighbor sprite " + key };
            mesh.vertices = new[] { new Vector3(-x, 0, -z), new Vector3(-x, 0, z), new Vector3(x, 0, z), new Vector3(x, 0, -z) };
            mesh.uv = new[] { new Vector2(left, bottom), new Vector2(left, top), new Vector2(right, top), new Vector2(right, bottom) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            p.meshes.Add(key, mesh); return mesh;
        }
    }
    // Animal helpers do not have story/headType. Their fixed head offset belongs to
    // the rendering configuration rather than the human head worker.
    public sealed class NeighborHeadWorker : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) => base.CanDrawNow(node, parms) && !parms.flags.FlagSet(PawnRenderFlags.HeadStump);
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot) =>
            base.OffsetFor(node, parms, out pivot) + ((NeighborSpriteProperties)node.Props).headOffset;
    }
}
