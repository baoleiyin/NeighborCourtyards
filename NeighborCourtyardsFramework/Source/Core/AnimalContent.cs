using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NeighborCourtyards
{
    public sealed class CompProperties_AnimalWelcome : CompProperties
    {
        public CourtyardContentDef content;
        public PawnKindDef animal;
        public ThingDef food;
        public int foodCount = 20, waitTicks = 60000, populationLimit = 2;
        public float arrivalAge = .25f;
        public CompProperties_AnimalWelcome() { compClass = typeof(CompAnimalWelcome); }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach(var e in base.ConfigErrors(parentDef)) yield return e;
            if(content == null || animal == null || food == null || foodCount <= 0 || waitTicks <= 0 || populationLimit < 1)
                yield return "Animal welcome needs content, animal, food and positive limits.";
            if(animal != null && animal.race?.race?.Animal != true)
                yield return "Animal welcome must reference an animal PawnKindDef.";
            if(parentDef.thingClass == null || !typeof(Building).IsAssignableFrom(parentDef.thingClass) || parentDef.tickerType != TickerType.Rare)
                yield return "Animal welcome requires a building with Rare ticking.";
            if(!DefinitionValidation.Nonnegative(arrivalAge)) yield return "Animal arrival age must be finite and nonnegative.";
        }
    }

    public sealed class CompAnimalWelcome : ThingComp
    {
        public int progress;
        public bool enabled = true;
        public Gender preferredGender = Gender.None;
        int populationLimit = -1;
        public int PopulationLimit { get => populationLimit < 0 ? Props.populationLimit : populationLimit; set => populationLimit = Math.Max(0,value); }
        public CompProperties_AnimalWelcome Props => (CompProperties_AnimalWelcome)props;
        public IEnumerable<Building_Bed> FreeBeds => parent.Map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>()
            .Where(b => b.def.GetModExtension<CourtyardBedSlots>()?.animal == Props.animal && !b.Medical && !b.IsForbidden(Faction.OfPlayer) && !b.IsBurning() &&
                b.OwnersForReading.Count < b.SleepingSlotsCount && b.AnyUnoccupiedSleepingSlot &&
                parent.Map.reachability.CanReach(parent.Position,b,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors,Danger.None)))
            .OrderBy(b => b.Position.DistanceToSquared(parent.Position)).ThenBy(b => b.thingIDNumber);
        public Building_Bed Bed => parent.Spawned ? FreeBeds.FirstOrDefault(b=>WelcomeSafety.SafeBed(b,Props.animal.race)) : null;
        public IEnumerable<Pawn> Residents => PawnsFinder.AllMapsWorldAndTemporary_Alive.Where(p => p.Faction == Faction.OfPlayer && p.kindDef == Props.animal);
        public static CompAnimalWelcome PrimaryFor(Map map,PawnKindDef animal) => map.listerBuildings.allBuildingsColonist
            .Where(b=>b.TryGetComp<CompAnimalWelcome>()?.Props.animal==animal).OrderBy(b=>b.thingIDNumber).FirstOrDefault()?.TryGetComp<CompAnimalWelcome>();
        public List<Thing> Foods => GenRadial.RadialDistinctThingsAround(parent.Position,parent.Map,6f,true)
            .Where(t => t.Spawned && t.def == Props.food && !t.IsForbidden(Faction.OfPlayer) && !t.IsBurning() &&
                parent.Map.reachability.CanReach(parent.Position,t,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors,Danger.None)))
            .OrderBy(t=>t.thingIDNumber).ToList();
        public string BlockReason()
        {
            if(!parent.Spawned || parent.Faction != Faction.OfPlayer || parent.IsForbidden(Faction.OfPlayer) || parent.IsBurning()) return "NC_AnimalWelcomeUnavailable".Translate();
            if(!enabled) return "NC_AnimalWelcomePaused".Translate();
            if(PrimaryFor(parent.Map,Props.animal)!=this) return "NC_AnimalWelcomePrimary".Translate();
            if(Residents.Count() >= PopulationLimit) return "NC_AnimalWelcomeLimit".Translate(PopulationLimit);
            if(!FreeBeds.Any()) return "NC_AnimalWelcomeNoBed".Translate();
            string safety = WelcomeSafety.BlockReason(parent,Props.animal.race);
            if(safety != null) return safety;
            if(Bed==null) return "NC_AnimalWelcomeUnsafeBed".Translate();
            if(Foods.Sum(t=>t.stackCount) < Props.foodCount) return "NC_AnimalWelcomeFood".Translate(Props.food.label,Props.foodCount);
            return null;
        }
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref progress,"ncAnimalWelcomeProgress");
            Scribe_Values.Look(ref enabled,"ncAnimalWelcomeEnabled",true);
            Scribe_Values.Look(ref preferredGender,"ncAnimalWelcomeGender",Gender.None);
            Scribe_Values.Look(ref populationLimit,"ncAnimalWelcomeLimit",-1);
        }
        public override void CompTickRare() => Advance(250);
        public void Advance(int ticks)
        {
            if(ticks <= 0 || BlockReason()!=null) return;
            progress = (int)Math.Min(Props.waitTicks, (long)progress + ticks);
            if(progress >= Props.waitTicks) TryWelcome(out _);
        }
        public bool TryWelcome(out Pawn animal)
        {
            animal = null;
            if(BlockReason()!=null || !ArrivalWalk.TryEntry(parent,out IntVec3 entry,Props.animal.race)) return false;
            var foods=Foods;
            var bed=Bed;
            if(bed==null)return false;
            if(foods.Sum(t=>t.stackCount)<Props.foodCount) return false;
            Gender sex=preferredGender;
            if(sex==Gender.None) sex=Residents.Count(p=>p.gender==Gender.Female)<=Residents.Count(p=>p.gender==Gender.Male)?Gender.Female:Gender.Male;
            Pawn candidate=PawnGenerator.GeneratePawn(new PawnGenerationRequest(Props.animal,Faction.OfPlayer,forceGenerateNewPawn:true,
                canGeneratePawnRelations:false,fixedBiologicalAge:Props.arrivalAge,fixedChronologicalAge:Props.arrivalAge,fixedGender:sex));
            GenSpawn.Spawn(candidate,entry,parent.Map);
            if(!candidate.ownership.ClaimBedIfNonMedical(bed)){candidate.Destroy();return false;}
            int remaining=Props.foodCount;
            foreach(var food in foods){int take=Math.Min(remaining,food.stackCount);food.SplitOff(take).Destroy();remaining-=take;if(remaining==0)break;}
            progress=0;animal=candidate;
            // Penned animals use the native handler's roping work when the gate blocks an unroped path.
            if(candidate.CanReach(parent,PathEndMode.Touch,Danger.None)) ArrivalWalk.Start(candidate,parent);
            Messages.Message("NC_AnimalWelcomeArrived".Translate(candidate.LabelShort),candidate,MessageTypeDefOf.PositiveEvent);
            return true;
        }
        public string GenderLabel => (preferredGender==Gender.None?"NC_AnimalWelcomeBalanced":preferredGender==Gender.Female?"NC_CreatureFemale":"NC_CreatureMale").Translate();
        public void ChooseGender() => Find.WindowStack.Add(new CourtyardMenu(new List<FloatMenuOption>{new FloatMenuOption("NC_AnimalWelcomeBalanced".Translate(),()=>preferredGender=Gender.None),new FloatMenuOption("NC_CreatureFemale".Translate(),()=>preferredGender=Gender.Female),new FloatMenuOption("NC_CreatureMale".Translate(),()=>preferredGender=Gender.Male)}));
        public override string CompInspectStringExtra() => "NC_AnimalWelcomeProgress".Translate(BlockReason()??((progress/(float)Props.waitTicks).ToStringPercent()))+"\n"+"NC_AnimalWelcomePopulation".Translate(Props.animal.LabelCap,Residents.Count(),PopulationLimit);
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if(parent.Faction!=Faction.OfPlayer) yield break;
            yield return new Command_Toggle{defaultLabel="NC_AnimalWelcomeToggle".Translate(),defaultDesc="NC_AnimalWelcomeToggleDesc".Translate(),icon=TexCommand.ForbidOff,isActive=()=>enabled,toggleAction=()=>enabled=!enabled};
            yield return new Command_Action{defaultLabel="NC_AnimalWelcomeGender".Translate(GenderLabel),icon=TexCommand.ForbidOff,action=ChooseGender};
            yield return new Command_Action{defaultLabel="NC_Welcome".Translate(),icon=TexCommand.ForbidOff,action=()=>MainTabWindow_Assistants.Open(parent.Map,page:2)};
        }
    }

    public sealed class CourtyardBedSlots : DefModExtension
    {
        public List<IntVec3> offsets = new List<IntVec3>();
        public PawnKindDef animal;
    }
    [HarmonyPatch(typeof(Building_Bed),"get_SleepingSlotsCount")]
    static class AnimalBedSlotCount
    {
        static void Postfix(Building_Bed __instance,ref int __result)
        {var slots=__instance.def.GetModExtension<CourtyardBedSlots>();if(slots?.offsets.Count>0)__result=slots.offsets.Count;}
    }
    [HarmonyPatch(typeof(Building_Bed),nameof(Building_Bed.GetSleepingSlotPos))]
    static class AnimalBedSlotPosition
    {
        static bool Prefix(Building_Bed __instance,int index,ref IntVec3 __result)
        {
            var slots=__instance.def.GetModExtension<CourtyardBedSlots>();
            if(slots==null)return true;
            if(index<0||index>=slots.offsets.Count)return true;
            __result=__instance.Position+slots.offsets[index].RotatedBy(__instance.Rotation);return false;
        }
    }
    // Each custom sleeping slot occupies one cell, even when the nest is larger.
    // Keep the native standability/door filtering, but avoid its 1x1/2xN-only offsets.
    sealed class AnimalBedInteractionPattern : CellSearchPattern
    {
        public override void AddCellsToList(List<IntVec3> cells,Thing thing,CellRect rect,IntVec3 focus,Rot4 rotation)
        {
            BedInteractionCellSearchPattern.BedCellOffsets1x1(cells);
            for(int i=0;i<cells.Count;i++)cells[i]=focus+cells[i].RotatedBy(rotation);
        }
    }
    [HarmonyPatch(typeof(Building_Bed),nameof(Building_Bed.FindPreferredInteractionCell))]
    static class AnimalBedInteractionCells
    {
        static readonly CellSearchPattern pattern=new AnimalBedInteractionPattern();
        static void Prefix(Building_Bed __instance,ref CellSearchPattern customSearchPattern)
        {
            if(customSearchPattern==null&&__instance.def.GetModExtension<CourtyardBedSlots>()!=null)
                customSearchPattern=pattern;
        }
    }
    [HarmonyPatch(typeof(RestUtility),nameof(RestUtility.CanUseBedNow))]
    static class AnimalWelcomeBedSpecies
    {
        static void Postfix(Thing bedThing,Pawn sleeper,ref bool __result)
        {var animal=bedThing.def.GetModExtension<CourtyardBedSlots>()?.animal;if(animal!=null&&sleeper?.kindDef!=animal)__result=false;}
    }
}
