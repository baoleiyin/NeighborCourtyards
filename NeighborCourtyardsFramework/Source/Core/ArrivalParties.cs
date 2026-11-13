using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace NeighborCourtyards
{
    public sealed partial class CompCourtyard
    {
        void ResetFixedRules()
        {
            int oldMaximum=ArrivalTicks;
            arrivalTicksOverride=mealCostOverride=mealReserveOverride=-1;mealRadiusOverride=-1;
            progress=Mathf.Clamp(Mathf.RoundToInt((float)progress/oldMaximum*ArrivalTicks),0,ArrivalTicks);
            if(arrivalDelayTicks>0)arrivalDelayTicks=Mathf.Clamp(Mathf.RoundToInt((float)arrivalDelayTicks/oldMaximum*ArrivalTicks),Math.Min(progress+1,ArrivalTicks),ArrivalTicks);
            populationLimit=Species.defaultPopulationLimit;desiredRole=-1;requestedRole=null;serviceArea=null;
        }
        public List<Building_Bed> PartyBeds(int count)
        {
            if(count<1||count>4||!parent.Spawned)return null;
            var guests=Enumerable.Range(0,count).Select(i=>BedCandidate(i>=2)).ToList();
            return PartyBeds(guests,true);
        }
        Pawn adultBedCandidate,childBedCandidate;
        Pawn BedCandidate(bool child)
        {
            // Unspawned, unsaved samples carry the race's comps and life stage so the
            // native bed gates (including extension patches) can evaluate compatibility.
            var candidate=child?childBedCandidate:adultBedCandidate;
            if(candidate!=null)return candidate;
            candidate=(Pawn)ThingMaker.MakeThing(Species.race);
            candidate.kindDef=Species.pawnKind;
            PawnComponentsUtility.CreateInitialComponents(candidate);
            candidate.ageTracker.AgeBiologicalTicks=(long)((child?Mathf.Clamp(10f,Species.childMinAge,Species.childMaxAge):Species.maxAge)*GenDate.TicksPerYear);
            candidate.gender=Species.fixedGender??Gender.Female;
            // Aging a player pawn from baby to child sends a growth letter. Samples
            // must finish aging before faction assignment so no unsaved pawn leaks.
            candidate.SetFactionDirect(Faction.OfPlayer);
            if(child)childBedCandidate=candidate;else adultBedCandidate=candidate;
            return candidate;
        }
        static bool CanAssignBed(Pawn pawn,Building_Bed bed) => RestUtility.CanUseBedEver(pawn,bed.def)&&
            (bed.GetComp<CompAssignableToPawn_Bed>()?.CanAssignTo(pawn).Accepted??false);
        List<Building_Bed> PartyBeds(List<Pawn> guests,bool planning=false)
        {
            var available=parent.Map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Where(b=>
                b.def.building.bed_humanlike==Species.race.race.Humanlike&&!b.def.building.bed_crib&&!b.Medical&&!b.ForPrisoners&&!b.ForSlaves&&
                b.OwnersForReading.Count==0&&!Enumerable.Range(0,b.SleepingSlotsCount).Any(i=>b.GetCurOccupant(i)!=null)&&
                !b.IsForbidden(Faction.OfPlayer)&&!b.IsBurning()&&WelcomeSafety.SafeBed(b,Species.race)&&(serviceArea==null||serviceArea[b.Position])&&
                parent.Map.reachability.CanReach(parent.Position,b,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors,Danger.None))).OrderBy(b=>b.thingIDNumber).ToList();
            var options=guests.Select(p=>available.Where(b=>CanAssignBed(p,b)).ToList()).ToArray();
            var result=new Building_Bed[guests.Count];
            var used=new HashSet<Building_Bed>();
            bool AssignSingles(int start)
            {
                // Match the most restricted guest first; a child-only bed must remain
                // usable even when compatible ordinary beds appear earlier in the list.
                int next=Enumerable.Range(start,guests.Count-start).Where(i=>result[i]==null)
                    .OrderBy(i=>options[i].Count(b=>!used.Contains(b))).DefaultIfEmpty(-1).First();
                if(next<0)return true;
                foreach(var bed in options[next])
                {
                    if(!used.Add(bed))continue;
                    result[next]=bed;if(AssignSingles(start))return true;
                    result[next]=null;used.Remove(bed);
                }
                return false;
            }
            if(guests.Count>=2&&(planning||BedUtility.WillingToShareBed(guests[0],guests[1])))
                foreach(var bed in options[0].Where(b=>b.SleepingSlotsCount>=2&&options[1].Contains(b)))
                {
                    result[0]=result[1]=bed;used.Add(bed);
                    if(AssignSingles(2))return result.ToList();
                    result[0]=result[1]=null;used.Remove(bed);
                }
            return AssignSingles(0)?result.ToList():null;
        }
        void EnsureArrivalParty()
        {
            if(pendingPartySize>0)return;
            int maximum=Species.helperMode?1:Species.familyChildren&&ModsConfig.BiotechActive?4:2;
            var possible=Enumerable.Range(1,maximum).Where(n=>Residents.Count()+n<=populationLimit&&PartyBeds(n)!=null&&Meals().Sum(t=>t.stackCount)>=MealCost*n+MealReserve).ToList();
            if(possible.Count==0)return;
            pendingPartySize=possible.RandomElement();
            pendingPetKind=Species.companionPets.Count>0&&Rand.Chance(.35f)?Species.companionPets.RandomElement():null;
        }
        Pawn CreateMember(int role,float age,Gender? gender,bool child)
        {
            for(int attempt=0;attempt<12;attempt++)
            {
                var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(Species.pawnKind,Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false,allowAddictions:false,
                    fixedBiologicalAge:age,fixedGender:gender,developmentalStages:child?DevelopmentalStage.Child:DevelopmentalStage.Adult,fixedIdeo:Faction.OfPlayer.ideos?.PrimaryIdeo));
                bool configured = false;
                try
                {
                    if (!Species.helperMode && !child && p.WorkTypeIsDisabled(Species.roles[role].primaryWork)) continue;
                    var comp = p.GetComp<CompAssistant>();
                    if (comp == null || comp.Settings.species != Species)
                        throw new InvalidOperationException(Species.defName + ": generated pawn needs a matching CompAssistant.");
                    comp.assignedRole = role;
                    comp.assignedRoleDef = Species.roles[role];
                    comp.home = parent;
                    Species.Adapter.ConfigureAppearance(p, role);
                    AssistantAbilities.EnsureTraits(p);
                    AssistantAbilities.EnsureSkills(p, true);
                    configured = true;
                    return p;
                }
                finally
                {
                    // A failing extension must not leave a generated pawn outside the caller's rollback list.
                    if (!configured && !p.Destroyed) p.Destroy();
                }
            }
            return null;
        }
        bool WelcomeParty(out Pawn first)
        {
            first=null;if(BlockReason()!=null)return false;
            EnsureArrivalParty();if(pendingPartySize==0||BlockReason()!=null)return false;
            var races=pendingPetKind==null?new[]{Species.race}:new[]{Species.race,pendingPetKind.race};
            if(!ArrivalWalk.TryEntry(parent,out IntVec3 entry,races))return false;
            var members=new List<Pawn>();Pawn pet=null;
            bool committed = false;
            try
            {
                for(int i=0;i<pendingPartySize;i++)
                {
                    bool child=i>=2;
                    int role=child?Roles.Role(members[Rand.Range(0,2)]):Enumerable.Range(0,Species.roles.Count).OrderBy(r=>Residents.Count(p=>Roles.Role(p)==r)+members.Count(p=>Roles.Role(p)==r)).First();
                    float parentMinimum = Math.Min(Species.maxAge, Math.Max(26, Species.minAge));
                    float age=child?Rand.Range(Species.childMinAge,Species.childMaxAge):Rand.Range(pendingPartySize>=3?parentMinimum:Species.minAge,Species.maxAge);
                    Gender? gender=Species.fixedGender??(pendingPartySize>=2&&!child?(i==0?Gender.Female:Gender.Male):(Gender?)null);
                    Pawn p=CreateMember(role,age,gender,child);
                    if (p == null) return false;
                    members.Add(p);
                }
                if(!Species.helperMode&&members.Count>=2)members[0].relations.AddDirectRelation(PawnRelationDefOf.Spouse,members[1]);
                foreach(var child in members.Skip(2)){child.relations.AddDirectRelation(PawnRelationDefOf.Parent,members[0]);child.relations.AddDirectRelation(PawnRelationDefOf.Parent,members[1]);}
                var beds=PartyBeds(members);
                if (beds == null) return false;
                if(pendingPetKind!=null)pet=PawnGenerator.GeneratePawn(new PawnGenerationRequest(pendingPetKind,Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:1));
                foreach(var p in members)
                {
                    GenSpawn.Spawn(p,entry,parent.Map);
                    p.GetComp<CompAssistant>().Refresh();
                    var bed=beds[members.IndexOf(p)];
                    if(!CanAssignBed(p,bed)||!p.ownership.ClaimBedIfNonMedical(bed)||p.ownership.OwnedBed!=bed)
                    {
                        return false;
                    }
                }
                foreach(var p in members)ArrivalWalk.Start(p,parent);
                if(pet!=null)
                {
                    GenSpawn.Spawn(pet,entry,parent.Map);
                    ArrivalWalk.Start(pet,parent);
                    pet.relations.AddDirectRelation(PawnRelationDefOf.Bond,members[0]);
                    var petBed=parent.Map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().FirstOrDefault(b=>!b.def.building.bed_humanlike&&!b.Medical&&!b.IsBurning()&&!b.IsForbidden(Faction.OfPlayer)&&b.OwnersForReading.Count<b.SleepingSlotsCount&&b.def.building.bed_maxBodySize>=pet.BodySize&&
                        MapComponent_PetArrivals.Profile(b)==null&&
                        pet.CanReach(b,PathEndMode.Touch,Danger.None));
                    if(petBed!=null)pet.ownership.ClaimBedIfNonMedical(petBed);
                }
                string group=ArrivalGroupLabel;
                string names=string.Join("\n",members.Select(p=>"• "+AssistantNames.Display(p)));
                string text="NC_ArrivalLetterBody".Translate(Species.LabelCap,group,names);
                var targets=members.Cast<Thing>().ToList();
                if(pet!=null)
                {
                    text+="\n\n"+"NC_ArrivalLetterPet".Translate(pet.LabelShortCap,pet.kindDef.LabelCap);
                    targets.Add(pet);
                }
                var meals = Meals();
                int remaining = MealCost * members.Count;
                if (meals.Sum(t => t.stackCount) < remaining + MealReserve) return false;
                // All fallible species configuration and job setup finishes before charging meals.
                foreach (var meal in meals)
                {
                    if (remaining <= 0) break;
                    int take = Math.Min(remaining, meal.stackCount);
                    meal.SplitOff(take).Destroy();
                    remaining -= take;
                }
                first = members[0];
                progress = arrivalDelayTicks = pendingPartySize = 0;
                pendingPetKind = null;
                committed = true;
                foreach (var member in members)
                {
                    try { Species.Adapter.OnWelcomed(member, this); }
                    catch (Exception error)
                    {
                        Log.Error("[NeighborCourtyardsCore] OnWelcomed failed for " + Species.defName + ": " + error);
                    }
                }
                Find.LetterStack.ReceiveLetter("NC_ArrivalLetterLabel".Translate(),text,LetterDefOf.PositiveEvent,new LookTargets(targets));
                return true;
            }
            finally
            {
                if (!committed)
                {
                    foreach (var member in members.Where(p => !p.Destroyed))
                    {
                        parent.Map?.GetComponent<MapComponent_ArrivalWalk>().Finish(member);
                        member.ownership?.UnclaimBed();
                        member.Destroy();
                    }
                    if (pet != null && !pet.Destroyed)
                    {
                        parent.Map?.GetComponent<MapComponent_ArrivalWalk>().Finish(pet);
                        pet.ownership?.UnclaimBed();
                        pet.Destroy();
                    }
                }
            }
        }
    }
}
