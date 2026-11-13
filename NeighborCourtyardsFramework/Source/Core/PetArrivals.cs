using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace NeighborCourtyards
{
    public sealed class CourtyardPetArrivalDef : Def
    {
        public AssistantSpeciesDef species;
        public PawnKindDef pet;
        public ThingDef nest;
        public override IEnumerable<string> ConfigErrors(){foreach(var error in base.ConfigErrors())yield return error;if(species==null||pet==null||nest==null)yield return "Pet arrival needs species, pet and nest.";}
    }
    public sealed class PetArrivalState : IExposable
    {
        public Building_Bed bed;
        public int progress,delay;
        public Pawn resident;
        public bool ownershipInitialized;
        public void ExposeData(){Scribe_References.Look(ref bed,"bed");Scribe_Values.Look(ref progress,"progress");Scribe_Values.Look(ref delay,"delay");Scribe_References.Look(ref resident,"welcomedResident");Scribe_Values.Look(ref ownershipInitialized,"ownershipInitialized");}
    }
    public sealed class MapComponent_PetArrivals : MapComponent
    {
        public List<PetArrivalState> arrivals=new List<PetArrivalState>();
        public MapComponent_PetArrivals(Map map):base(map){}
        public override void ExposeData(){Scribe_Collections.Look(ref arrivals,"ncPetArrivals",LookMode.Deep);if(arrivals==null)arrivals=new List<PetArrivalState>();}
        public static CourtyardPetArrivalDef Profile(Building_Bed bed)=>DefDatabase<CourtyardPetArrivalDef>.AllDefsListForReading.FirstOrDefault(p=>p.nest==bed.def);
        // Hay is a harvested item with FoodTypeFlags.Plant. Filter growing plants by thing category, not food type.
        public static Thing Food(Building_Bed bed,CourtyardPetArrivalDef profile)=>GenRadial.RadialDistinctThingsAround(bed.Position,bed.Map,3f,true).Where(t=>
            t.Spawned&&t.def.category==ThingCategory.Item&&!t.IsForbidden(Faction.OfPlayer)&&t.def.IsIngestible&&!t.def.IsDrug&&t.GetStatValue(StatDefOf.Nutrition)>0&&profile.pet.race.race.CanEverEat(t.def)&&
            t.Position.DistanceToSquared(bed.Position)<=9&&bed.Map.reachability.CanReach(bed.Position,t,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors,Danger.None))).OrderBy(t=>t.Position.DistanceToSquared(bed.Position)).ThenBy(t=>t.thingIDNumber).FirstOrDefault();
        public static string BlockReason(Building_Bed bed,CourtyardPetArrivalDef profile)
        {
            if(!bed.Spawned||bed.Faction!=Faction.OfPlayer||bed.IsForbidden(Faction.OfPlayer)||bed.IsBurning()||bed.Medical)return "NC_NestUnavailable".Translate().ToString();
            var home=MainTabWindow_Assistants.PrimaryHome(bed.Map,profile.species);
            if(home==null)return "NC_NeedsWelcomePoint".Translate().ToString();
            if(home.shelter)return "NC_Sheltering".Translate().ToString();
            if(!home.welcome)return "NC_WelcomePaused".Translate().ToString();
            string safety=WelcomeSafety.BlockReason(bed,profile.pet.race);
            if(safety!=null)return safety;
            if(!WelcomeSafety.SafeBed(bed,profile.pet.race))return "NC_UnsafeNest".Translate().ToString();
            if(bed.OwnersForReading.Count>=bed.SleepingSlotsCount||Enumerable.Range(0,bed.SleepingSlotsCount).Count(i=>bed.GetCurOccupant(i)!=null)>=bed.SleepingSlotsCount)return "NC_FullNest".Translate().ToString();
            if(!bed.Map.reachability.CanReach(home.parent.Position,bed,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors,Danger.None)))return "NC_UnreachableNest".Translate().ToString();
            if(Food(bed,profile)==null)return "NC_PetNeedsFood".Translate().ToString();
            return null;
        }
        public override void MapComponentTick(){if(Find.TickManager.TicksGame%250==0)Advance(250);}
        public void Advance(int ticks)
        {
            if(ticks<=0)return;
            arrivals.RemoveAll(a=>a.bed==null||!a.bed.Spawned||a.bed.Map!=map);
            foreach(var bed in map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().ToList())
            {
                var profile=Profile(bed);if(profile==null)continue;
                var state=arrivals.FirstOrDefault(a=>a.bed==bed);
                if(state==null){state=new PetArrivalState{bed=bed};arrivals.Add(state);}
                WelcomeNestReservation.Prepare(state,profile);
                if(BlockReason(bed,profile)!=null)continue;
                if(state.delay==0)state.delay=Rand.RangeInclusive(1,120000);
                state.progress=Math.Min(120000,state.progress+ticks);
                if(state.progress<state.delay)continue;
                var food=Food(bed,profile);if(food==null)continue;
                if(!ArrivalWalk.TryEntry(bed,out IntVec3 entry,profile.pet.race))continue;
                var pet=PawnGenerator.GeneratePawn(new PawnGenerationRequest(profile.pet,Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:1));
                GenSpawn.Spawn(pet,entry,map);
                state.resident=pet;
                if(!pet.ownership.ClaimBedIfNonMedical(bed)){state.resident=null;pet.Destroy();continue;}
                food.SplitOff(1).Destroy();state.progress=state.delay=0;
                ArrivalWalk.Start(pet,bed);
                Messages.Message("NC_PetArrived".Translate(pet.LabelShort).ToString(),pet,MessageTypeDefOf.PositiveEvent);
            }
        }
    }
    [HarmonyPatch(typeof(Building_Bed),nameof(Building_Bed.GetInspectString))]
    static class PetArrivalInspect
    {
        static void Postfix(Building_Bed __instance,ref string __result)
        {
            var profile=MapComponent_PetArrivals.Profile(__instance);if(profile==null||!__instance.Spawned)return;
            __result+=(string.IsNullOrEmpty(__result)?"":"\n")+"NC_PetWelcomeInspect".Translate(profile.pet.LabelCap, MapComponent_PetArrivals.BlockReason(__instance,profile) ?? "NC_PetArrivalTime".Translate().ToString()).ToString();
        }
    }
}
