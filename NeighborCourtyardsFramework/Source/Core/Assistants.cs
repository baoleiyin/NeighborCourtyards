using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

[assembly: AssemblyVersion("0.6.8.0")]
[assembly: AssemblyFileVersion("0.8.0.1")]
[assembly: AssemblyInformationalVersion("0.8.0-dev.2")]
namespace NeighborCourtyards
{
    public sealed class CompProperties_Assistant : CompProperties
    {
        public AssistantSpeciesDef species;
        public int defaultUnskilledThreshold = 6;
        public CompProperties_Assistant() { compClass = typeof(CompAssistant); }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (var error in base.ConfigErrors(parentDef)) yield return error;
            if (species == null || species.race != parentDef) yield return "Assistant component must reference its own race's AssistantSpeciesDef.";
            if (defaultUnskilledThreshold < 0 || defaultUnskilledThreshold > 20) yield return "defaultUnskilledThreshold must be between 0 and 20.";
            if (parentDef.thingClass == null || !typeof(Pawn).IsAssignableFrom(parentDef.thingClass)) yield return "Assistant component requires a Pawn.";
        }
    }
    public sealed partial class CompAssistant : ThingComp
    {
        public Thing home;
        public int assignedRole;
        public AssistantRoleDef assignedRoleDef;
        public string personalName, namePoolKey;
        public int namingVersion;
        public int skillProfileVersion;
        public bool grewUpInColony;
        public CompProperties_Assistant Settings => (CompProperties_Assistant)props;
        int configuredStage = -1, configuredRole = -1, nextCheck;
        public int nextSupplyCheck;
        bool loadingSpawn;
        public Pawn Pawn => (Pawn)parent;
        public CompCourtyard Home => home?.Spawned == true && home.Map == Pawn.MapHeld ? home.TryGetComp<CompCourtyard>() : null;
        public override void PostExposeData()
        {
            Scribe_References.Look(ref home, "ncHome");
            Scribe_Values.Look(ref assignedRole, "ncAssignedRole");
            Scribe_Defs.Look(ref assignedRoleDef, "ncAssignedRoleDef");
            Scribe_Values.Look(ref personalName,"ncPersonalName");
            Scribe_Values.Look(ref namePoolKey,"ncNamePool");
            Scribe_Values.Look(ref namingVersion,"ncNamingVersion");
            Scribe_Values.Look(ref skillProfileVersion,"ncSkillProfileVersion");
            Scribe_Values.Look(ref grewUpInColony,"ncGrewUpInColony");
            Scribe_Values.Look(ref configuredStage, "ncStage", -1);
            Scribe_Values.Look(ref configuredRole, "ncRole", -1);
            Scribe_Values.Look(ref nextSupplyCheck, "ncNextSupplyCheck");
            ExposeRestrictions();
        }
        public override void PostSpawnSetup(bool respawningAfterLoad) { base.PostSpawnSetup(respawningAfterLoad); loadingSpawn=respawningAfterLoad;try{Refresh();}finally{loadingSpawn=false;} }
        public override void CompTick() { Poll(); }
        public override void CompTickInterval(int delta) { Poll(); }
        void Poll()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick < nextCheck) return;
            nextCheck = tick + 2500;
            Refresh();
        }
        public void Refresh()
        {
            Pawn p = Pawn;
            if (!Roles.Managed(p) || p.Dead || !p.Spawned) return;
            if (!p.DevelopmentalStage.Adult()) grewUpInColony = true;
            AssistantAbilities.EnsureTraits(p);
            AssistantAbilities.EnsureSkills(p,false);
            if(!loadingSpawn)AssistantNames.Ensure(p);
            int stage = p.DevelopmentalStage.Baby() ? 0 : p.DevelopmentalStage.Child() ? 1 : 2;
            int role = Roles.Role(p);
            if (Home == null)
                home = p.Map.listerThings.AllThings.Where(t => t.Faction == Faction.OfPlayer && t.TryGetComp<CompCourtyard>()?.RaceName == p.def.defName).OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (stage != configuredStage || role != configuredRole)
            {
                bool keepGrowingSchedule = Settings.species.preserveGrowingSchedules && configuredStage >= 0 && configuredStage < 2;
                configuredStage = stage; configuredRole = role;
                if (stage == 2 && p.timetable != null && !keepGrowingSchedule)
                    for (int h = 0; h < 24; h++) p.timetable.SetAssignment(h, TimeAssignmentDefOf.Anything);
            }
            if (p.playerSettings != null)
            {
                p.playerSettings.hostilityResponse = HostilityResponseMode.Flee;
                RefreshArea();
            }
            if (p.workSettings == null) return;
            p.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            foreach (WorkTypeDef w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                int value = p.WorkTypeIsDisabled(w) ? 0 : Roles.Priority(p, w);
                if (p.workSettings.GetPriority(w) != value) p.workSettings.SetPriority(w, value);
            }
            p.workSettings.Notify_UseWorkPrioritiesChanged();
        }
        public override string CompInspectStringExtra()
        {
            if (!Roles.Managed(Pawn)) return null;
            if (!Pawn.DevelopmentalStage.Adult()) return "NC_Growing".Translate().ToString();
            var role = Roles.For(Pawn);
            return role.label + (role.tool == null ? "" : "\n" + "NC_ToolInspect".Translate(Roles.HasTool(Pawn) ? role.tool.LabelCap.ToString() : "NC_NotCollected".Translate().ToString()).ToString()) +
                (Home == null ? "NC_NoHomeLine".Translate().ToString() : Home.shelter ? "NC_ShelteringLine".Translate().ToString() : "");
        }
    }

    public sealed class CompProperties_Courtyard : CompProperties
    {
        public AssistantSpeciesDef species;
        public CompProperties_Courtyard() { compClass = typeof(CompCourtyard); }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (var error in base.ConfigErrors(parentDef)) yield return error;
            if (species == null) yield return "Courtyard component needs a resolved AssistantSpeciesDef.";
            if (parentDef.thingClass == null || !typeof(Building).IsAssignableFrom(parentDef.thingClass) || parentDef.tickerType != TickerType.Rare)
                yield return "Courtyard component requires a building with Rare ticking.";
        }
    }
    public sealed partial class CompCourtyard : ThingComp
    {
        public bool welcome = true, shelter;
        public int progress, desiredRole = -1, populationLimit = 6;
        int arrivalDelayTicks, welcomeRuleVersion = 2;
        int pendingPartySize;
        PawnKindDef pendingPetKind;
        public int PendingPartySize => pendingPartySize;
        public string ArrivalGroupLabel => pendingPartySize == 0 ? "NC_RandomGuests".Translate().ToString() : pendingPartySize == 1 ? "NC_SingleGuest".Translate().ToString() : pendingPartySize == 2 ? "NC_Couple".Translate().ToString() : pendingPartySize == 3 ? "NC_FamilyThree".Translate().ToString() : "NC_FamilyFour".Translate().ToString();
        public int ScheduledArrivalTicks => arrivalDelayTicks;
        public int arrivalTicksOverride = -1, mealCostOverride = -1, mealReserveOverride = -1;
        public float mealRadiusOverride = -1;
        public Area serviceArea;
        AssistantRoleDef requestedRole;
        public AssistantSpeciesDef Species => ((CompProperties_Courtyard)props).species;
        public string RaceName => Species.race.defName;
        public int ArrivalTicks => arrivalTicksOverride > 0 ? arrivalTicksOverride : Species.arrivalTicks;
        public int MealCost => mealCostOverride >= 0 ? mealCostOverride : Species.mealCost;
        public int MealReserve => mealReserveOverride >= 0 ? mealReserveOverride : Species.mealReserve;
        public float MealRadius => mealRadiusOverride > 0 ? mealRadiusOverride : Species.mealRadius;
        public void ConfigureWelcome(int ticks, int cost, int reserve, float radius, int limit)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
            int oldMaximum = ArrivalTicks;
            float fraction = (float)progress / oldMaximum;
            arrivalTicksOverride = Mathf.Clamp(ticks, 15000, 600000);
            mealCostOverride = Mathf.Clamp(cost, 0, 100); mealReserveOverride = Mathf.Clamp(reserve, 0, 100);
            mealRadiusOverride = Mathf.Clamp(radius, 2, 30); populationLimit = Mathf.Clamp(limit, 0, 50);
            progress = Mathf.Clamp(Mathf.RoundToInt(fraction * ArrivalTicks), 0, ArrivalTicks);
            if (arrivalDelayTicks > 0) arrivalDelayTicks = Mathf.Clamp(Mathf.RoundToInt((float)arrivalDelayTicks / oldMaximum * ArrivalTicks), Math.Min(progress + 1, ArrivalTicks), ArrivalTicks);
        }
        public override void PostPostMake() { base.PostPostMake(); populationLimit = Species.defaultPopulationLimit; }
        public override void PostExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) requestedRole = desiredRole >= 0 && desiredRole < Species.roles.Count ? Species.roles[desiredRole] : null;
            Scribe_Defs.Look(ref requestedRole, "ncRequestedRole");
            Scribe_Values.Look(ref welcome, "ncWelcome", true); Scribe_Values.Look(ref shelter, "ncShelter");
            Scribe_Values.Look(ref progress, "ncArrivalProgress"); Scribe_Values.Look(ref desiredRole, "ncDesiredRole", -1);
            Scribe_Values.Look(ref populationLimit, "ncLimit", 6); Scribe_References.Look(ref serviceArea, "ncArea");
            ExposeAreas();
            Scribe_Values.Look(ref arrivalTicksOverride, "ncArrivalTicksOverride", -1);
            Scribe_Values.Look(ref mealCostOverride, "ncMealCostOverride", -1);
            Scribe_Values.Look(ref mealReserveOverride, "ncMealReserveOverride", -1);
            Scribe_Values.Look(ref mealRadiusOverride, "ncMealRadiusOverride", -1);
            Scribe_Values.Look(ref arrivalDelayTicks, "ncArrivalDelayTicks");
            Scribe_Values.Look(ref pendingPartySize, "ncArrivalPartySize");
            Scribe_Defs.Look(ref pendingPetKind, "ncArrivalPet");
            Scribe_Values.Look(ref welcomeRuleVersion, "ncWelcomeRuleVersion", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Migrate the former default pair, including defaults saved by the settings panel.
                if (welcomeRuleVersion < 1 && (mealCostOverride < 0 || mealCostOverride == 5) && (mealReserveOverride < 0 || mealReserveOverride == 5))
                    mealCostOverride = mealReserveOverride = -1;
                populationLimit = Mathf.Clamp(populationLimit, 0, 50); progress = Mathf.Clamp(progress, 0, ArrivalTicks);
                arrivalDelayTicks = Mathf.Clamp(arrivalDelayTicks, 0, ArrivalTicks);
                desiredRole = requestedRole != null ? Species.roles.IndexOf(requestedRole) : Mathf.Clamp(desiredRole, -1, Species.roles.Count - 1);
                if (welcomeRuleVersion < 2) ResetFixedRules();
                welcomeRuleVersion = 2;
                pendingPartySize = Mathf.Clamp(pendingPartySize, 0, Species.familyChildren && ModsConfig.BiotechActive ? 4 : 2);
            }
        }
        // Being carried or held in a map building does not free an arrival slot.
        public IEnumerable<Pawn> Residents => NeighborRegistry.Residents(parent.Map).Where(p => p.def.defName == RaceName);
        public bool Primary => !parent.Map.listerThings.AllThings.Any(t => t != parent && t.Spawned && t.Faction == Faction.OfPlayer && t.thingIDNumber < parent.thingIDNumber && t.TryGetComp<CompCourtyard>()?.RaceName == RaceName);
        public List<Thing> Meals() => parent.Map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Where(t =>
            t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.def.IsIngestible && !t.def.IsDrug && t.def.ingestible.foodType.HasFlag(FoodTypeFlags.Meal) &&
            t.GetStatValue(StatDefOf.Nutrition) >= Species.minimumMealNutrition && t.Position.DistanceToSquared(parent.Position) <= MealRadius * MealRadius &&
            parent.Map.reachability.CanReach(parent.Position, t, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.None))).OrderBy(t => t.thingIDNumber).ToList();
        public Building_Bed FreeBed() => PartyBeds(1)?.FirstOrDefault();
        public string BlockReason()
        {
            if (!parent.Spawned || parent.Faction != Faction.OfPlayer) return "NC_HomeUnavailable".Translate().ToString();
            if (!Primary) return "NC_PrimaryHomeBusy".Translate().ToString();
            if (!welcome) return "NC_WelcomePaused".Translate().ToString();
            if (shelter) return "NC_Sheltering".Translate().ToString();
            string safety = WelcomeSafety.BlockReason(parent, Species.race);
            if (safety != null) return safety;
            int count = Math.Max(1, pendingPartySize);
            if (Residents.Count() + count > populationLimit) return "NC_NoCapacity".Translate().ToString();
            if (parent.IsBurning()) return "NC_HomeBurning".Translate().ToString();
            if (parent.TryGetComp<CompPowerTrader>() is CompPowerTrader power && !power.PowerOn) return "NC_HomeNoPower".Translate().ToString();
            if (PartyBeds(count) == null) return "NC_BedsNeeded".Translate(count).ToString();
            if (Meals().Sum(t => t.stackCount) < MealCost * count + MealReserve) return "NC_MealsNeeded".Translate(MealRadius, MealCost * count + MealReserve).ToString();
            return null;
        }
        public override void CompTickRare() { Advance(250); }
        public void Advance(int ticks)
        {
            if (ticks <= 0 || BlockReason() != null) return;
            EnsureArrivalParty();
            if (arrivalDelayTicks == 0) arrivalDelayTicks = Rand.RangeInclusive(Math.Min(progress + 1, ArrivalTicks), ArrivalTicks);
            progress = (int)Math.Min(ArrivalTicks, (long)progress + ticks);
            if (progress >= arrivalDelayTicks) TryWelcome(out _);
        }
        bool welcoming;
        public bool TryWelcome(out Pawn pawn)
        {
            pawn = null;
            if (welcoming) return false;
            welcoming = true;
            try { return WelcomeParty(out pawn); }
            finally { welcoming = false; }
        }
        public override string CompInspectStringExtra() => "NC_WelcomeInspect".Translate(BlockReason() ?? "NC_AwaitingArrival".Translate().ToString(), (ArrivalTicks / 60000f).ToString("0.##"), (progress / 60000f).ToString("0.##"), Residents.Count(), populationLimit).ToString();
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Action { defaultLabel = "NC_Management".Translate().ToString(), defaultDesc = "", icon = TexCommand.Install, action = () => MainTabWindow_Assistants.Open(parent.Map, Species, 2) };
            yield return new Command_Toggle { defaultLabel = (welcome ? "NC_PauseWelcome" : "NC_ResumeWelcome").Translate().ToString(), defaultDesc = "", icon = TexCommand.ForbidOff, isActive = () => welcome, toggleAction = () => welcome = !welcome };
            yield return new Command_Toggle { defaultLabel = "NC_Shelter".Translate().ToString(), defaultDesc = "", icon = TexCommand.Draft, isActive = () => shelter, toggleAction = () => {
                SetShelter(!shelter);
            } };
        }
        public void SetShelter(bool value)
        {
            shelter = value;
            ApplyAreaPolicy(true);
        }
        public void SetArea(Area area) { if (!AcceptArea(area)) return; serviceArea = area; manageArea = true; ApplyAreaPolicy(false); }
    }

    public sealed class AssistantSupplyJob : DefModExtension { }

    [HarmonyPatch(typeof(JobDriver), "SetupToils")]
    static class ShelterSupplyJobs
    {
        static void Postfix(JobDriver __instance)
        {
            if (__instance.job?.def.GetModExtension<AssistantSupplyJob>() == null) return;
            __instance.AddEndCondition(() => __instance.pawn.GetComp<CompAssistant>()?.Home?.shelter == true ?
                JobCondition.InterruptForced : JobCondition.Ongoing);
        }
    }

    public sealed class JobDriver_CollectTool : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !Roles.Adult(pawn) || Roles.HasTool(pawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            Toil take = ToilMaker.MakeToil("CollectCourtyardTool");
            take.initAction = () => {
                Thing item = job.targetA.Thing;
                if (item.def != Roles.For(pawn).tool || !item.Spawned || !pawn.Position.AdjacentTo8WayOrInside(item.Position)) { EndJobWith(JobCondition.Incompletable); return; }
                Thing unit = item.SplitOff(1);
                if (!pawn.inventory.innerContainer.TryAdd(unit, false)) GenPlace.TryPlaceThing(unit, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            };
            take.defaultCompleteMode = ToilCompleteMode.Instant; yield return take;
        }
    }

    public static class AssistantJobs
    {
        public static Job Extra(Pawn p)
        {
            if (!Roles.Adult(p) || !p.Spawned || p.Downed || p.InMentalState || p.IsBurning()) return null;
            CompAssistant state = p.GetComp<CompAssistant>();
            if (state.Home?.shelter == true)
            {
                return state.Home.ShelterJob(p);
            }
            int tick = Find.TickManager.TicksGame;
            if (tick < state.nextSupplyCheck) return null;
            state.nextSupplyCheck = tick + 600;
            if (Roles.For(p).tool != null && !Roles.HasTool(p))
            {
                Thing tool = GenClosest.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForDef(Roles.For(p).tool), PathEndMode.ClosestTouch, TraverseParms.For(p, Danger.None), 60f,
                    t => !t.IsForbidden(p) && p.CanReserve(t, 1, 1));
                if (tool != null) return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("NC_CollectTool"), tool);
            }
            return AssistantCourses.AutoJob(p) ?? Roles.Profile(p).Adapter.SpecialJob(p);
        }
    }

    public sealed class StatPart_AssistantTool : StatPart
    {
        float Factor(Pawn p) => Roles.HasTool(p) ? Roles.For(p).toolStats.FirstOrDefault(b => b.stat == parentStat)?.factor ?? 1f : 1f;
        public override void TransformValue(StatRequest req, ref float val) { if (req.Thing is Pawn p) val *= Factor(p); }
        public override string ExplanationPart(StatRequest req) => req.Thing is Pawn p && Factor(p) != 1f ? "NC_RoleToolFactor".Translate(Factor(p).ToString("P0")).ToString() : null;
    }
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            new Harmony("local.neighborcourtyards.assistants").PatchAll(typeof(Bootstrap).Assembly);
            AssistantCourses.Initialize();
            foreach (StatDef stat in DefDatabase<AssistantRoleDef>.AllDefsListForReading.SelectMany(r => r.toolStats).Select(b => b.stat).Distinct())
            {
                if (stat.parts == null) stat.parts = new List<StatPart>();
                stat.parts.Add(new StatPart_AssistantTool { parentStat = stat });
            }
            Log.Message("[NeighborCourtyardsCore] " + NeighborFramework.Version + "; loaded species profiles: " + DefDatabase<AssistantSpeciesDef>.DefCount);

        }
    }
    [HarmonyPatch(typeof(Pawn), "get_CanTakeOrder")]
    static class NoOrders { static void Postfix(Pawn __instance, ref bool __result) { if (Roles.Managed(__instance)) __result = false; } }
    [HarmonyPatch(typeof(Pawn_WorkSettings), nameof(Pawn_WorkSettings.GetPriority))]
    static class RolePriorities
    {
        static void Postfix(Pawn ___pawn, WorkTypeDef __0, ref int __result)
        {
            if (Roles.Managed(___pawn)) __result = ___pawn.WorkTypeIsDisabled(__0) ? 0 : Roles.Priority(___pawn, __0);
        }
    }
    [HarmonyPatch(typeof(Pawn_DraftController), "set_Drafted")]
    static class NoDraft { static bool Prefix(Pawn ___pawn, bool value) => !value || !Roles.Managed(___pawn); }
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    static class NoForcedJob { static bool Prefix(Pawn ___pawn, ref bool __result) { if (!Roles.Managed(___pawn)) return true; __result = false; return false; } }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    static class NoCommandGizmos
    {
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (Roles.Managed(__instance))
                __result = __result.Where(g => g is Command_EmotionBubbleToggle || g is Command_AssistantCourse || Roles.Profile(__instance).Adapter.PreserveGizmo(__instance, g));
        }
    }
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    static class KeepSpeciesStartingApparel
    {
        static bool Prefix(Pawn pawn, ref Job __result) { if (!Roles.Managed(pawn) || !Roles.Profile(pawn).keepStartingApparel) return true; __result = null; return false; }
    }
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    static class RestrictJobs { static void Postfix(Pawn pawn, WorkGiver giver, ref bool __result) { if (Roles.Managed(pawn) && !Roles.Allowed(pawn, giver)) __result = false; } }
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage))]
    static class ExtraJobs
    {
        static bool Prefix(JobGiver_Work __instance, Pawn pawn, ref ThinkResult __result)
        {
            if (!Roles.Adult(pawn) || __instance.emergency) return true;
            Job job = AssistantJobs.Extra(pawn);
            if (job == null) return true;
            __result = new ThinkResult(job, __instance); return false;
        }
    }
}
