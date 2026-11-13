using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards.Family
{
    public sealed class LifeStageSpriteDef : Def
    {
        public string texture;
        public float height = 1;
        public int front = 0, side = 1, back = 2, curled = 3, sleeping = 4, playing = 5;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texture.NullOrEmpty() || float.IsNaN(height) || float.IsInfinity(height) || height <= 0)
                yield return "Life-stage sprites need a texture and finite positive height.";
            foreach (int frame in new[] { front, side, back, curled, sleeping, playing })
                if (frame < 0 || frame > 5) yield return "Life-stage sprite frames must be within 0..5 in the 3 by 2 atlas.";
        }
    }
    public sealed class FamilyAppearanceExtension : DefModExtension
    { public LifeStageSpriteDef baby, child; }
    public sealed class FamilyBedExtension : DefModExtension
    { public DevelopmentalStage allowedStages = DevelopmentalStage.Child; public ThingDef race; }
    public interface IFamilyPoseDriver { bool DrawExternalPose { get; } }

    [StaticConstructorOnStartup]
    public static class FamilyBootstrap
    {
        static FamilyBootstrap() { new Harmony("local.neighborcourtyards.family").PatchAll(Assembly.GetExecutingAssembly()); }
    }
    public sealed class CompProperties_GrowingNeighbor : CompProperties
    { public CompProperties_GrowingNeighbor() { compClass = typeof(CompGrowingNeighbor); } }
    public sealed class CompGrowingNeighbor : ThingComp
    {
        int lastStage = -1, nextCheck;
        public override void PostExposeData() { Scribe_Values.Look(ref lastStage, "ncAppearanceLifeStage", -1); }
        public override void PostSpawnSetup(bool respawningAfterLoad) { Poll(); }
        public override void CompTick() { Poll(); }
        public override void CompTickInterval(int delta) { Poll(); }
        void Poll()
        {
            var p = (Pawn)parent;
            if (p.Dead || Find.TickManager == null || Find.TickManager.TicksGame < nextCheck) return;
            nextCheck = Find.TickManager.TicksGame + 60;
            int stage = (int)p.DevelopmentalStage;
            if (stage == lastStage) return;
            lastStage = stage;
            p.Drawer?.renderer?.SetAllGraphicsDirty(); PortraitsCache.SetDirty(p);
            var bed = p.ownership?.OwnedBed;
            if (bed != null && !FamilyBedRules.Allowed(p, bed.def))
            {
                p.ownership.UnclaimBed();
                if (p.CurJobDef == JobDefOf.LayDown && p.CurrentBed() == bed) p.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class FamilySprites
    {
        static readonly Mesh[,] meshes = new Mesh[6, 2];
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly MaterialPropertyBlock opacity = new MaterialPropertyBlock();
        public static LifeStageSpriteDef Style(Pawn pawn)
        {
            var e = pawn?.def.GetModExtension<FamilyAppearanceExtension>();
            return pawn?.DevelopmentalStage.Baby() == true ? e?.baby : pawn?.DevelopmentalStage.Child() == true ? e?.child : null;
        }
        public static bool External(Pawn p) => p?.jobs?.curDriver is IFamilyPoseDriver d && d.DrawExternalPose;
        public static Mesh Mesh(int frame, bool flip = false)
        {
            frame = Mathf.Clamp(frame, 0, 5); int f = flip ? 1 : 0;
            if (meshes[frame, f] != null) return meshes[frame, f];
            float left = frame % 3 / 3f, right = left + 1 / 3f, bottom = 1 - (frame / 3 + 1) / 2f, top = bottom + .5f;
            if (flip) { float t = left; left = right; right = t; }
            var mesh = new Mesh { name = "Neighbor whole-pose atlas " + frame + (flip ? " flipped" : "") };
            mesh.vertices = new[] { new Vector3(-.5f, 0, 0), new Vector3(-.5f, 0, 1), new Vector3(.5f, 0, 1), new Vector3(.5f, 0, 0) };
            mesh.uv = new[] { new Vector2(left, bottom), new Vector2(left, top), new Vector2(right, top), new Vector2(right, bottom) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return meshes[frame, f] = mesh;
        }
        public static Material Material(string texture)
        {
            if (!materials.TryGetValue(texture, out var mat)) materials[texture] = mat = MaterialPool.MatFrom(texture, ShaderDatabase.Transparent, Color.white);
            return mat;
        }
        public static void Draw(string texture, int frame, Vector3 at, float height, float angle = 0, float alpha = 1, bool flip = false)
        {
            if (alpha <= .001f) return;
            opacity.SetColor("_Color", new Color(1, 1, 1, Mathf.Clamp01(alpha)));
            Graphics.DrawMesh(Mesh(frame, flip), Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(height, 1, height)), Material(texture), 0, null, 0, opacity);
        }
        public static int Frame(Pawn p, Rot4 facing, bool portrait)
        {
            var s = Style(p);
            if (!portrait && p.GetPosture() != PawnPosture.Standing) return p.Awake() ? s.curled : s.sleeping;
            return facing == Rot4.North ? s.back : facing.IsHorizontal ? s.side : s.front;
        }
    }

    public sealed class PawnRenderNode_YoungNeighbor : PawnRenderNode
    {
        public PawnRenderNode_YoungNeighbor(Pawn p, PawnRenderNodeProperties props, PawnRenderTree tree) : base(p, props, tree) { }
        public override Graphic GraphicFor(Pawn p)
        {
            var s = FamilySprites.Style(p);
            return s == null ? null : GraphicDatabase.Get<Graphic_Single>(s.texture, ShaderDatabase.Cutout, Vector2.one, Color.white);
        }
        public override GraphicMeshSet MeshSetFor(Pawn p) => new GraphicMeshSet(1, 1);
        public override Mesh GetMesh(PawnDrawParms parms) => FamilySprites.Mesh(FamilySprites.Frame(parms.pawn, parms.facing, parms.Portrait), parms.facing == Rot4.West);
    }
    public sealed class PawnRenderNodeWorker_YoungNeighbor : PawnRenderNodeWorker
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) => FamilySprites.Style(parms.pawn) != null &&
            (parms.Portrait || parms.Cache || !FamilySprites.External(parms.pawn)) && base.CanDrawNow(node, parms);
        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        { float height = FamilySprites.Style(parms.pawn)?.height ?? 1; return new Vector3(height, 1, height); }
        public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
        {
            pivot = Vector3.zero; var p = parms.pawn; var s = FamilySprites.Style(p);
            if (s == null || parms.Portrait || parms.Cache) return Vector3.zero;
            var bed = p.CurrentBed();
            if (bed?.def.GetModExtension<FamilyBedExtension>() != null)
            {
                Vector3 at = bed.DrawPos - p.DrawPos; at.y = 0;
                at.z += -.4f*s.height + Mathf.Sin(((Find.TickManager?.TicksGame??0)+p.thingIDNumber*11)*.02f)*.004f;
                return at;
            }
            if (p.DevelopmentalStage.Baby() && p.GetPosture() != PawnPosture.Standing) return new Vector3(0, 0, -s.height * .35f);
            float t = (Find.TickManager?.TicksGame ?? 0) + p.thingIDNumber * 11;
            return new Vector3(0, 0, p.pather?.Moving == true ? Mathf.Abs(Mathf.Sin(t * .13f)) * .016f : Mathf.Sin(t * .025f) * .003f);
        }
        public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
        {
            if (parms.Portrait || parms.Cache || parms.pawn.GetPosture() != PawnPosture.Standing) return Quaternion.identity;
            float t = (Find.TickManager?.TicksGame ?? 0) + parms.pawn.thingIDNumber * 11;
            return Quaternion.AngleAxis(parms.pawn.pather?.Moving == true ? Mathf.Sin(t * .13f) * 1.2f : 0, Vector3.up);
        }
    }

    public static class FamilyBedRules
    {
        public static bool Allowed(Pawn p, ThingDef def)
        {
            var e = def.GetModExtension<FamilyBedExtension>();
            return e == null || ((e.allowedStages & p.DevelopmentalStage) != 0 && (e.race == null || p.def == e.race));
        }
    }
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedEver))]
    static class AgeAppropriateBed
    { static void Postfix(Pawn p, ThingDef bedDef, ref bool __result) { __result &= FamilyBedRules.Allowed(p, bedDef); } }
    [HarmonyPatch(typeof(Building_Bed), "SleepingSlotsCount", MethodType.Getter)]
    static class SingleChildSlot
    { static void Postfix(Building_Bed __instance, ref int __result) { if (__instance.def.GetModExtension<FamilyBedExtension>() != null) __result = 1; } }
    [HarmonyPatch(typeof(PawnRenderer), "BodyAngle")]
    static class AuthoredBabySleepingOrientation
    {
        static void Postfix(Pawn ___pawn, ref float __result)
        {
            if (FamilySprites.Style(___pawn) != null && (___pawn.DevelopmentalStage.Baby() || ___pawn.CurrentBed()?.def.GetModExtension<FamilyBedExtension>() != null)) __result = 0;
        }
    }
}
