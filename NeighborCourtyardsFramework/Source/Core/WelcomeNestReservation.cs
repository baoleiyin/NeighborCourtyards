using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace NeighborCourtyards
{
    public static class WelcomeNestReservation
    {
        static PetArrivalState State(Building_Bed bed)=>bed.Map?.GetComponent<MapComponent_PetArrivals>().arrivals.FirstOrDefault(a=>a.bed==bed);
        static Pawn LegacyResident(Building_Bed bed,CourtyardPetArrivalDef profile,PetArrivalState state)
        {
            // A running invitation cannot already have a welcomed resident. Completed legacy
            // invitations saved no identity, so retain an existing pet from this species' options.
            if(state!=null&&(state.progress>0||state.delay>0))return null;
            return bed.OwnersForReading.FirstOrDefault(p=>!p.Dead&&DefDatabase<CourtyardPetArrivalDef>.AllDefsListForReading.Any(a=>a.species==profile.species&&a.pet==p.kindDef));
        }
        public static bool CanUse(Building_Bed bed,Pawn pawn)
        {
            if(bed==null)return true;
            var profile=MapComponent_PetArrivals.Profile(bed);
            if(profile==null)return true;
            if(!bed.Spawned)return false;
            var state=State(bed);
            return pawn!=null&&(state?.ownershipInitialized==true?state.resident==pawn:LegacyResident(bed,profile,state)==pawn);
        }
        static void StopUninvitedSleep(Building_Bed bed,Pawn resident)
        {
            for(int i=0;i<bed.SleepingSlotsCount;i++)
            {
                var pawn=bed.GetCurOccupant(i);
                // Do not force a downed patient to stand up. Actual occupancy still blocks arrival.
                if(pawn!=null&&pawn!=resident&&!pawn.Downed&&pawn.jobs?.curDriver!=null)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
        public static void Prepare(PetArrivalState state,CourtyardPetArrivalDef profile)
        {
            var bed=state.bed;
            if(!state.ownershipInitialized){state.resident=LegacyResident(bed,profile,state);state.ownershipInitialized=true;}
            if(state.resident!=null&&(state.resident.Destroyed||state.resident.Dead||state.resident.ownership?.OwnedBed!=bed))state.resident=null;
            foreach(var owner in bed.OwnersForReading.Where(p=>p!=state.resident).ToList())owner.ownership.UnclaimBed();
            StopUninvitedSleep(bed,state.resident);
        }
        public static void Vacate(Building_Bed bed)
        {
            if(bed?.Spawned!=true||bed.Faction!=Faction.OfPlayer||MapComponent_PetArrivals.Profile(bed)==null)return;
            var component=bed.Map.GetComponent<MapComponent_PetArrivals>();
            var state=State(bed);
            if(state==null){state=new PetArrivalState{bed=bed};component.arrivals.Add(state);}
            state.resident=null;state.ownershipInitialized=true;state.progress=state.delay=0;
            foreach(var owner in bed.OwnersForReading.ToList())owner.ownership.UnclaimBed();
            StopUninvitedSleep(bed,null);
        }
    }

    // The native bed gate covers rest searches and rescue/bed-use checks.
    [HarmonyPatch(typeof(RestUtility),nameof(RestUtility.CanUseBedNow))]
    static class WelcomeNestRest
    {
        static void Postfix(Thing bedThing,Pawn sleeper,ref bool __result)
        {if(__result&&bedThing is Building_Bed bed)__result=WelcomeNestReservation.CanUse(bed,sleeper);}
    }
    [HarmonyPatch(typeof(Pawn_Ownership),nameof(Pawn_Ownership.ClaimBedIfNonMedical))]
    static class WelcomeNestClaim
    {
        static bool Prefix(Building_Bed __0,Pawn ___pawn,ref bool __result)
        {if(WelcomeNestReservation.CanUse(__0,___pawn))return true;__result=false;return false;}
    }
    [HarmonyPatch(typeof(Building_Bed),nameof(Building_Bed.GetGizmos))]
    static class WelcomeNestVacateGizmo
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values,Building_Bed __instance)
        {
            foreach(var gizmo in values)yield return gizmo;
            if(__instance.Faction!=Faction.OfPlayer||MapComponent_PetArrivals.Profile(__instance)==null||
                (__instance.OwnersForReading.Count==0&&!Enumerable.Range(0,__instance.SleepingSlotsCount).Any(i=>__instance.GetCurOccupant(i)!=null)))yield break;
            yield return new Command_Action{defaultLabel="NC_VacateWelcomeNest".Translate(),defaultDesc="NC_VacateWelcomeNestDesc".Translate(),icon=TexCommand.ClearPrioritizedWork,action=()=>WelcomeNestReservation.Vacate(__instance)};
        }
    }
}
