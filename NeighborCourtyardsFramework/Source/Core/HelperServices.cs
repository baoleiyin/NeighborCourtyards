using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace NeighborCourtyards
{
    public static class NeighborRegistry
    {
        // AllPawns also includes carried residents; carrying a helper must not free an arrival slot.
        public static IEnumerable<Pawn> Residents(Map map) => map == null ? Enumerable.Empty<Pawn>() :
            map.mapPawns.AllPawns.Where(p => !p.Dead && !p.Destroyed && Roles.Managed(p));
    }

    public static class HelperServices
    {
        public static WorkTypeDef WorkFor(Pawn pawn, Job job)
        {
            if (job == null) return null;
            if (job.def == JobDefOf.Clean) return WorkTypeDefOf.Cleaning;
            if (job.def == JobDefOf.HaulToCell || job.def == JobDefOf.HaulToContainer) return WorkTypeDefOf.Hauling;
            return Roles.Profile(pawn)?.Adapter.WorkTypeForJob(pawn, job);
        }

        public static Job TryService(Pawn pawn)
        {
            if (!Roles.Helper(pawn) || !Roles.Adult(pawn) || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.InMentalState || pawn.IsBurning()) return null;
            var state = pawn.GetComp<CompAssistant>();
            if (state.Home?.shelter == true) return null;
            int now = Find.TickManager.TicksGame;
            if (now < state.nextHelperWorkCheck) return null;
            state.nextHelperWorkCheck = now + 180;
            var special = Roles.Profile(pawn).Adapter.SpecialJob(pawn);
            if (special != null)
            {
                var work = WorkFor(pawn, special);
                if (work != null && Roles.Priority(pawn, work) > 0) return special;
            }
            foreach (var work in state.ConfigurableWorkTypes().Where(w => Roles.Priority(pawn, w) > 0).OrderBy(w => Roles.Priority(pawn, w)))
            {
                if (work == WorkTypeDefOf.Cleaning)
                {
                    var giver = new WorkGiver_CleanFilth { def = DefDatabase<WorkGiverDef>.GetNamed("CleanFilth") };
                    foreach (var target in giver.PotentialWorkThingsGlobal(pawn).Where(t => !t.IsForbidden(pawn)).OrderBy(t => t.Position.DistanceToSquared(pawn.Position)))
                    {
                        if (!giver.HasJobOnThing(pawn, target, false)) continue;
                        var job = giver.JobOnThing(pawn, target, false);
                        if (job != null) { job.workGiverDef = giver.def; return job; }
                    }
                }
                else if (work == WorkTypeDefOf.Hauling)
                {
                    foreach (var thing in pawn.Map.listerHaulables.ThingsPotentiallyNeedingHauling().Where(t => t.Spawned && !t.IsForbidden(pawn)).OrderBy(t => t.Position.DistanceToSquared(pawn.Position)))
                    {
                        if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, thing, false)) continue;
                        var job = HaulAIUtility.HaulToStorageJob(pawn, thing, false);
                        if (job != null) return job;
                    }
                }
            }
            return null;
        }
    }

    // Only opt-in helpers use these insertion hooks. Native animal needs and threat
    // responses run unchanged; work is inserted after needs and before wandering.
    public sealed class JobGiver_HelperService : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => HelperServices.TryService(pawn);
    }
    public sealed class JobGiver_HelperShelter : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!Roles.Helper(pawn) || pawn.Downed || pawn.InMentalState) return null;
            var home = pawn.GetComp<CompAssistant>().Home;
            // Basic needs can still run while sheltered, but services stay disabled.
            if (home?.shelter != true || pawn.needs.food?.CurLevelPercentage < .3f || pawn.needs.rest?.CurLevelPercentage < .2f) return null;
            return home.ShelterJob(pawn);
        }
    }
}
