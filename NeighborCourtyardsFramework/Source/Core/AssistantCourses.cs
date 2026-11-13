using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace NeighborCourtyards
{
    public sealed class AssistantCourseStage
    {
        public int doses;
        public ResearchProjectDef research;
        // Each stage describes the total effect, not an increment over earlier stages.
        public List<StatModifier> statOffsets = new List<StatModifier>();
    }

    public sealed class AssistantCourseDef : Def
    {
        public ThingDef ingestible;
        public int doseInterval = 60000;
        public float searchRadius = 60f;
        public bool adultsOnly = true;
        public List<AssistantCourseStage> stages = new List<AssistantCourseStage>();
        // Optional legacy keys let extensions move existing courses without losing saves.
        public string enabledSaveKey, dosesSaveKey, lastDoseSaveKey;
        public int TotalDoses => stages.Count == 0 ? 0 : stages[stages.Count - 1]?.doses ?? 0;
        public int ResearchCap
        {
            get
            {
                int cap = 0;
                for (int i = 0; i < stages.Count; i++)
                {
                    var stage = stages[i];
                    if (stage.research != null && !stage.research.IsFinished) break;
                    cap = Math.Max(cap, stage.doses);
                }
                return cap;
            }
        }
        public int StageForDoses(int doses)
        {
            int stage = 0;
            while (stage < stages.Count && doses >= stages[stage].doses) stage++;
            return stage;
        }
        public bool CanAdvance(int doses, int lastTick, int tick, int cap) => doses >= 0 && doses < Math.Min(TotalDoses, cap) &&
            (lastTick < 0 || (long)tick - lastTick >= doseInterval);
        public float Offset(int stage, StatDef stat)
        {
            if (stage <= 0 || stage > stages.Count) return 0f;
            var offsets = stages[stage - 1].statOffsets;
            double total = 0d;
            for (int i = 0; i < offsets.Count; i++)
                if (offsets[i].stat == stat) total += offsets[i].value;
            return (float)total;
        }
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (ingestible?.ingestible == null) yield return "Course requires an ingestible item.";
            else if (ingestible.ingestible.outcomeDoers?.OfType<IngestionOutcomeDoer_AssistantCourse>().Any() != true)
                yield return "Course item requires IngestionOutcomeDoer_AssistantCourse.";
            if (ingestible?.ingestible != null && ingestible.ingestible.maxNumToIngestAtOnce != 1)
                yield return "Course item must set maxNumToIngestAtOnce to 1.";
            if (doseInterval < 1 || searchRadius <= 0) yield return "Course interval and search radius must be positive.";
            if (stages.Count == 0) yield return "Course requires at least one stage.";
            int previous = 0;
            foreach (var stage in stages)
            {
                if (stage.doses <= previous) yield return "Stage doses must be positive and strictly increasing.";
                previous = stage.doses;
            }
            if (DefDatabase<AssistantCourseDef>.AllDefsListForReading.Any(d => d != this && d.ingestible == ingestible))
                yield return "Each course item must belong to only one course.";
        }
    }

    public class CompProperties_AssistantCourse : CompProperties
    {
        public AssistantCourseDef course;
        public CompProperties_AssistantCourse() { compClass = typeof(CompAssistantCourse); }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (var error in base.ConfigErrors(parentDef)) yield return error;
            if (course == null) yield return "Assistant course is missing.";
            if (parentDef.comps.OfType<CompProperties_AssistantCourse>().Count(c => c.course == course) > 1)
                yield return "Do not attach the same course twice.";
        }
    }

    public class CompAssistantCourse : ThingComp
    {
        public bool courseEnabled;
        protected int doses;
        protected int lastDoseTick = -1;
        public AssistantCourseDef Course => ((CompProperties_AssistantCourse)props).course;
        public Pawn Pawn => parent as Pawn;
        public int Doses => doses;
        public int LastDoseTick => lastDoseTick;
        public bool Eligible => Pawn != null && !Pawn.Dead && (!Course.adultsOnly || Pawn.DevelopmentalStage.Adult());
        public int Stage => Eligible ? Course.StageForDoses(doses) : 0;
        public bool CanTakeNow => Eligible && Course.CanAdvance(doses, lastDoseTick, Find.TickManager?.TicksGame ?? 0, Course.ResearchCap);
        public bool TryTakeDose()
        {
            if (!CanTakeNow) return false;
            int oldStage = Stage;
            doses++; lastDoseTick = Find.TickManager.TicksGame;
            if (Stage != oldStage)
                foreach (StatDef stat in Course.stages.SelectMany(s => s.statOffsets).Select(s => s.stat).Distinct())
                    stat.Worker.ClearCacheForThing(parent);
            return true;
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref courseEnabled, Course.enabledSaveKey ?? Course.defName + "Enabled", false);
            Scribe_Values.Look(ref doses, Course.dosesSaveKey ?? Course.defName + "Doses", 0);
            Scribe_Values.Look(ref lastDoseTick, Course.lastDoseSaveKey ?? Course.defName + "LastDoseTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) doses = Math.Max(0, Math.Min(Course.TotalDoses, doses));
        }
        public Job AutoJob()
        {
            if (!courseEnabled || !CanTakeNow || !Pawn.Spawned || Pawn.Downed || Pawn.InMentalState || Pawn.IsBurning() ||
                Pawn.GetComp<CompAssistant>()?.Home?.shelter == true) return null;
            Thing dose = GenClosest.ClosestThingReachable(Pawn.Position, Pawn.Map, ThingRequest.ForDef(Course.ingestible),
                PathEndMode.ClosestTouch, TraverseParms.For(Pawn, Danger.None), Course.searchRadius,
                t => !t.IsForbidden(Pawn) && !t.Position.IsForbidden(Pawn) && Pawn.CanReserve(t, 1, 1));
            if (dose == null) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, dose); job.count = 1; return job;
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Eligible && parent.Faction == Faction.OfPlayer)
                yield return new Command_AssistantCourse {
                    defaultLabel = Course.LabelCap, defaultDesc = Course.description, icon = Course.ingestible.uiIcon,
                    isActive = () => courseEnabled, toggleAction = () => courseEnabled = !courseEnabled
                };
        }
        public override string CompInspectStringExtra()
        {
            if (!Eligible || doses == 0) return null;
            string progress = "NC_CourseProgress".Translate(Course.LabelCap, doses, Course.TotalDoses);
            return doses >= Course.TotalDoses || CanTakeNow ? progress : progress + "\n" + AssistantCourses.UnavailableReason(Pawn, Course);
        }
    }

    public class Command_AssistantCourse : Command_Toggle { }
    public class IngestionOutcomeDoer_AssistantCourse : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (ingestedCount > 0) AssistantCourses.For(pawn, AssistantCourses.For(ingested.def))?.TryTakeDose();
        }
    }

    public static class AssistantCourses
    {
        static readonly Dictionary<ThingDef, AssistantCourseDef> byItem = new Dictionary<ThingDef, AssistantCourseDef>();
        public static void Initialize()
        {
            byItem.Clear();
            foreach (var course in DefDatabase<AssistantCourseDef>.AllDefsListForReading)
            {
                if (course.ingestible != null) byItem[course.ingestible] = course;
                foreach (var stat in course.stages.SelectMany(s => s.statOffsets).Select(s => s.stat).Distinct())
                {
                    if (stat.parts == null) stat.parts = new List<StatPart>();
                    if (!stat.parts.Any(p => p is StatPart_AssistantCourse)) stat.parts.Add(new StatPart_AssistantCourse { parentStat = stat });
                }
            }
        }
        public static AssistantCourseDef For(ThingDef item) => item != null && byItem.TryGetValue(item, out var course) ? course : null;
        // Native administration recipes identify their ingestible through the first ingredient.
        public static AssistantCourseDef For(RecipeDef recipe) => recipe?.Worker is Recipe_AdministerIngestible ?
            For(recipe.ingredients.FirstOrDefault()?.filter.AllowedThingDefs.FirstOrDefault()) : null;
        public static CompAssistantCourse For(Pawn pawn, AssistantCourseDef course)
        {
            if (pawn == null || course == null) return null;
            var comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
                if (comps[i] is CompAssistantCourse comp && comp.Course == course) return comp;
            return null;
        }
        public static bool CanTake(Pawn pawn, AssistantCourseDef course) => For(pawn, course)?.CanTakeNow == true;
        public static bool AdultDoctor(Pawn pawn) => pawn?.RaceProps?.Humanlike == true && pawn.DevelopmentalStage.Adult() && !pawn.Dead;
        public static Job AutoJob(Pawn pawn)
        {
            var comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
                if (comps[i] is CompAssistantCourse comp)
                { Job job = comp.AutoJob(); if (job != null) return job; }
            return null;
        }
        public static string UnavailableReason(Pawn pawn, AssistantCourseDef course)
        {
            var comp = For(pawn, course);
            if (comp?.Eligible != true) return "NC_CourseIneligible".Translate();
            if (comp.Doses >= course.TotalDoses) return "NC_CourseComplete".Translate();
            if (comp.Doses >= course.ResearchCap)
                return "NC_CourseResearch".Translate(course.stages.First(s => s.doses > comp.Doses).research?.LabelCap ?? "");
            return "NC_CourseWait".Translate(Math.Max(0, comp.LastDoseTick + course.doseInterval - (Find.TickManager?.TicksGame ?? 0)).ToStringTicksToPeriod());
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    static class CourseIngestionGuard
    {
        static bool Prefix(Thing __instance, Pawn ingester, ref float __result)
        {
            var course = AssistantCourses.For(__instance.def);
            if (course == null || AssistantCourses.CanTake(ingester, course)) return true;
            __result = 0f; return false;
        }
    }
    [HarmonyPatch(typeof(JobDriver_Ingest), nameof(JobDriver_Ingest.TryMakePreToilReservations))]
    static class CourseJobGuard
    {
        static bool Prefix(JobDriver_Ingest __instance, ref bool __result)
        {
            var course = AssistantCourses.For(__instance.job?.targetA.Thing?.def);
            if (course == null || AssistantCourses.CanTake(__instance.pawn, course)) return true;
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(JobDriver_DoBill), nameof(JobDriver_DoBill.TryMakePreToilReservations))]
    static class CourseBillGuard
    {
        static bool Prefix(JobDriver_DoBill __instance, ref bool __result)
        {
            var course = AssistantCourses.For(__instance.job?.bill?.recipe);
            if (course == null || (AssistantCourses.CanTake(__instance.job.targetA.Thing as Pawn, course) && AssistantCourses.AdultDoctor(__instance.pawn))) return true;
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(Pawn_DrugPolicyTracker), nameof(Pawn_DrugPolicyTracker.AllowedToTakeScheduledNow))]
    static class CourseScheduledGuard
    {
        static void Postfix(ThingDef thingDef, Pawn ___pawn, ref bool __result)
        {
            var course = AssistantCourses.For(thingDef);
            if (__result && course != null) __result = AssistantCourses.CanTake(___pawn, course);
        }
    }
    [HarmonyPatch(typeof(Recipe_AdministerIngestible), nameof(Recipe_AdministerIngestible.AvailableOnNow))]
    static class CourseAdministrationAvailability
    {
        static bool Prefix(Recipe_AdministerIngestible __instance, Thing thing, ref bool __result)
        {
            var course = AssistantCourses.For(__instance.recipe);
            if (course == null || AssistantCourses.CanTake(thing as Pawn, course)) return true;
            __result = false; return false;
        }
    }
    [HarmonyPatch(typeof(Recipe_AdministerIngestible), nameof(Recipe_AdministerIngestible.ApplyOnPawn))]
    static class CourseAdministrationGuard
    {
        static bool Prefix(Pawn pawn, Pawn billDoer, List<Thing> ingredients) => ingredients?.All(t =>
            AssistantCourses.For(t.def) == null || (AssistantCourses.CanTake(pawn, AssistantCourses.For(t.def)) &&
            (billDoer == null || AssistantCourses.AdultDoctor(billDoer)))) != false;
    }
    [HarmonyPatch(typeof(FloatMenuOptionProvider_Ingest), "GetSingleOptionFor")]
    static class CourseMenuGuard
    {
        static void Postfix(Thing clickedThing, FloatMenuContext context, ref FloatMenuOption __result)
        {
            var course = AssistantCourses.For(clickedThing?.def); Pawn pawn = context?.FirstSelectedPawn;
            if (course != null && pawn != null && !AssistantCourses.CanTake(pawn, course))
                __result = new FloatMenuOption("NC_CourseTakeBlocked".Translate(AssistantCourses.UnavailableReason(pawn, course)), null);
        }
    }
    public sealed class StatPart_AssistantCourse : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is Pawn pawn)) return;
            var comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
                if (comps[i] is CompAssistantCourse comp) val += comp.Course.Offset(comp.Stage, parentStat);
        }
        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn pawn)) return null;
            var lines = pawn.AllComps.OfType<CompAssistantCourse>().Where(c => c.Course.Offset(c.Stage, parentStat) != 0f)
                .Select(c => c.Course.LabelCap + ": " + c.Course.Offset(c.Stage, parentStat).ToStringByStyle(parentStat.toStringStyle, ToStringNumberSense.Offset));
            return string.Join("\n", lines);
        }
    }
}
