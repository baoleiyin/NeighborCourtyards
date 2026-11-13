using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using NeighborCourtyards;
using NeighborCourtyards.Family;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

public sealed class ProbeAdapter : AssistantSpeciesAdapter
{
    public static Action<Pawn> Configure;
    public static Action<Pawn, CompCourtyard> Arrived;
    public override void ConfigureAppearance(Pawn pawn, int role) => Configure?.Invoke(pawn);
    public override void OnWelcomed(Pawn pawn, CompCourtyard courtyard) => Arrived?.Invoke(pawn, courtyard);
}

public abstract class AbstractAdapter : AssistantSpeciesAdapter { }

[StaticConstructorOnStartup]
public static class FrameworkProbe
{
    static readonly List<string> checks = new List<string>();
    static readonly List<string> errors = new List<string>();
    static readonly List<string> expectedErrors = new List<string>();
    static bool started, done, busy;
    static bool sawCleaning;
    static int stage, frames;
    static Map map, savedMap;
    static Pawn neighbor;
    static string neighborId;
    static CompCourtyard home;
    static AssistantSpeciesDef species;
    static Building_Bed bed;
    static string PathFor(string name) => Path.Combine(GenFilePaths.SaveDataFolderPath, name);

    static FrameworkProbe()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (done || (type != LogType.Error && type != LogType.Exception)) return;
            if (message.Contains("QA_EXPECTED_CALLBACK")) expectedErrors.Add(message);
            else errors.Add(message + "\n" + stack);
        };
        var harmony = new Harmony("local.neighborcourtyards.frameworkqa");
        harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(FrameworkProbe), nameof(Entry)));
        harmony.Patch(AccessTools.Method(typeof(Root_Play), nameof(Root_Play.SetupForQuickTestPlay)), postfix: new HarmonyMethod(typeof(FrameworkProbe), nameof(MapSize)));
        harmony.Patch(AccessTools.Method(typeof(Map), nameof(Map.MapUpdate)), postfix: new HarmonyMethod(typeof(FrameworkProbe), nameof(Update)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)), prefix: new HarmonyMethod(typeof(FrameworkProbe), nameof(JobStarted)));
    }

    public static void MapSize() => Find.GameInitData.mapSize = 100;

    public static void JobStarted(Pawn ___pawn, Job newJob)
    {
        if (___pawn == neighbor && newJob?.def == JobDefOf.Clean) sawCleaning = true;
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message);
        File.AppendAllText(PathFor("progress.txt"), "PASS " + message + "\n");
    }

    static void Invalid(Def definition, string fragment)
    {
        var messages = definition.ConfigErrors().ToList();
        Check(messages.Any(m => m.Contains(fragment)), "invalid definition diagnosed: " + fragment);
    }

    static void DefinitionChecks()
    {
        Check(NeighborFramework.ContractVersion == 2, "public contract version is available");
        foreach (var profile in DefDatabase<AssistantSpeciesDef>.AllDefsListForReading)
            Check(!profile.ConfigErrors().Any(), "loaded species validates: " + profile.defName);
        foreach (var role in DefDatabase<AssistantRoleDef>.AllDefsListForReading)
            Check(!role.ConfigErrors().Any(), "loaded role validates: " + role.defName);
        Invalid(new AssistantRoleDef { defName = "QA_NullWork", work = null }, "work must");
        Invalid(new AssistantRoleDef { defName = "QA_NullSkill", skills = new List<AssistantSkill> { null } }, "skills must");
        Invalid(new AssistantRoleDef { defName = "QA_Nan", toolStats = new List<ToolStatBonus> { new ToolStatBonus { stat = StatDefOf.MoveSpeed, factor = float.NaN } } }, "toolStats must");
        Invalid(new AssistantRoleDef { defName = "QA_Duplicates", work = new List<WorkPriority> { new WorkPriority { workType = WorkTypeDefOf.Cleaning }, new WorkPriority { workType = WorkTypeDefOf.Cleaning } } }, "duplicate work");
        Invalid(new AssistantSpeciesDef { defName = "QA_NullRoles", roles = null }, "roles must");
        Invalid(new AssistantSpeciesDef { defName = "QA_Adapter", adapterClass = typeof(AbstractAdapter) }, "adapterClass");
        Invalid(new AssistantSpeciesDef { defName = "QA_Meals", mealCost = int.MaxValue }, "overflow");
        Invalid(new AssistantSpeciesDef { defName = "QA_Age", minAge = float.NaN }, "adult arrival age");
        Invalid(new AssistantSpeciesDef { defName = "QA_Radius", mealRadius = float.PositiveInfinity }, "Meal radius");
        Invalid(new AssistantNameSetDef { defName = "QA_Names", pools = new List<AssistantNamePool> { null } }, "pools must");
        Invalid(new LifeStageSpriteDef { defName = "QA_Frames", texture = "placeholder", sleeping = 6 }, "0..5");
        Invalid(new LifeStageSpriteDef { defName = "QA_Height", texture = "placeholder", height = float.PositiveInfinity }, "finite positive");
        Invalid(new CourtyardBuildingSetDef { defName = "QA_Groups", groups = null }, "Building groups");
        Invalid(new CourtyardContentDef { defName = "QA_Animals", animals = null }, "animals must");
        var international = new AssistantNamePool { key = "all", requireTwoChineseCharacters = false, names = new List<string> { "Hazel", "小松果", "Étoile" } };
        Check(international.names.All(international.Accepts), "international names are accepted by opt-in pools");
        Check(!international.Accepts("<b>Hazel</b>") && !international.Accepts("Hazel\nClover"), "name pools reject markup and multiline names");
        Check(new AssistantNamePool().Accepts("松果") && !new AssistantNamePool().Accepts("Hazel"), "legacy two-character name policy remains the default");
        Check(DefDatabase<AssistantSpeciesDef>.GetNamedSilentFail("NCS_Species") != null, "pure XML starter definition loads");
        Check(DefDatabase<AssistantSpeciesDef>.GetNamedSilentFail("NCT_Species") != null, "generated second species has independent definitions");
        Check(BuildingCatalog.Entries.Any(e => e.def.defName == "NCS_WelcomeLamp"), "starter appears in building catalog");
        Check(CourtyardEncyclopedia.Entries.Any(e => e.article?.defName == "NCS_Introduction"), "starter appears in encyclopedia");
        File.WriteAllText(PathFor("definitions-complete.txt"), checks.Count.ToString());
    }

    public static void Entry()
    {
        if (started || done || LongEventHandler.AnyEventNowOrWaiting || ++frames < 20) return;
        started = true;
        try
        {
            DefinitionChecks();
            LongEventHandler.QueueLongEvent(() => { }, "Play", "GeneratingMap", false, null);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Tick(int ticks)
    {
        for (int index = 0; index < ticks; index++) Find.TickManager.DoSingleTick();
    }

    static Thing Build(string name, IntVec3 cell)
    {
        var definition = ThingDef.Named(name);
        var thing = ThingMaker.MakeThing(definition, definition.MadeFromStuff ? ThingDefOf.WoodLog : null);
        thing.SetFaction(Faction.OfPlayer);
        return GenSpawn.Spawn(thing, cell, map);
    }

    static void Setup()
    {
        var center = map.Center;
        // Keep the generated fixture temperate while exercising the real safety gates.
        map.Biome.constantOutdoorTemperature = 21;
        map.weatherManager.TransitionTo(WeatherDefOf.Clear);
        map.fogGrid.ClearAllFog();
        foreach (var cell in CellRect.CenteredOn(center, 12))
        {
            foreach (var thing in cell.GetThingList(map).ToList())
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy();
                else thing.DeSpawn();
            }
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            map.roofGrid.SetRoof(cell, null);
            map.areaManager.Home[cell] = true;
        }
        foreach (var pawn in map.mapPawns.FreeColonists)
        {
            pawn.needs.food.CurLevelPercentage = 1;
            pawn.needs.rest.CurLevelPercentage = 1;
            var wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 60000;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
        }
        species = DefDatabase<AssistantSpeciesDef>.GetNamed("NCS_Species");
        home = Build("NCS_WelcomeLamp", center).TryGetComp<CompCourtyard>();
        foreach (var room in map.regionGrid.AllRooms) room.Temperature = 21;
        home.populationLimit = 1;
        int previousTicks = home.ArrivalTicks;
        bool rejectedRadius = false;
        try { home.ConfigureWelcome(15000, 3, 0, float.NaN, 2); }
        catch (ArgumentOutOfRangeException) { rejectedRadius = true; }
        Check(rejectedRadius && home.ArrivalTicks == previousTicks && home.populationLimit == 1, "invalid welcome radius is rejected before settings change");
        bed = (Building_Bed)Build("Bed", center + new IntVec3(3, 0, 0));
        var meal = ThingMaker.MakeThing(ThingDefOf.MealSimple);
        meal.stackCount = meal.def.stackLimit;
        GenSpawn.Spawn(meal, center + new IntVec3(1, 0, 0), map);
        Check(home.BlockReason() == null, "native welcome prerequisites pass: " + home.BlockReason() +
            " outdoor=" + map.mapTemperature.OutdoorTemp + " cell=" + GenTemperature.GetTemperatureForCell(center, map) + " safe=" + GenTemperature.SafeTemperatureRange(species.race));

        AccessTools.Field(typeof(AssistantSpeciesDef), "adapter").SetValue(species, new ProbeAdapter());
        Pawn failedPawn = null;
        ProbeAdapter.Configure = pawn => { failedPawn = pawn; throw new InvalidOperationException("QA configuration failure"); };
        int beforeMeals = home.Meals().Sum(t => t.stackCount);
        bool failed = false;
        try { home.TryWelcome(out _); }
        catch (InvalidOperationException exception) { failed = exception.Message == "QA configuration failure"; }
        Check(failed && failedPawn != null && failedPawn.Destroyed, "failed appearance configuration discards its generated pawn");
        Check(home.Meals().Sum(t => t.stackCount) == beforeMeals && !home.Residents.Any(), "failed arrival keeps food and resident count consistent");
        Check(bed.OwnersForReading.Count == 0, "failed arrival leaves bed ownership available");

        int reentries = 0, callbacks = 0;
        ProbeAdapter.Configure = pawn => { if (!home.TryWelcome(out _)) reentries++; };
        ProbeAdapter.Arrived = (pawn, courtyard) => { callbacks++; throw new InvalidOperationException("QA_EXPECTED_CALLBACK"); };
        Check(home.TryWelcome(out neighbor), "arrival can retry successfully after configuration failure");
        Check(reentries == 1, "reentrant arrival is rejected without generating another party");
        Check(callbacks == 1 && expectedErrors.Count == 1, "post-commit callback failure is reported once");
        Check(neighbor.Spawned && !neighbor.Destroyed && home.Residents.Count() == 1, "post-commit callback failure preserves the resident");
        Check(home.Meals().Sum(t => t.stackCount) == beforeMeals - species.mealCost, "successful arrival charges exactly one meal cost");
        Check(neighbor.ownership.OwnedBed == bed, "arrival claims the native bed");
        Check(!home.TryWelcome(out _) && callbacks == 1, "capacity blocks repeat arrivals and callbacks");
        Check(Roles.For(neighbor).defName == "NCS_Helper" && Roles.Managed(neighbor), "arrival registers its role and management state");
        ProbeAdapter.Configure = null;
        ProbeAdapter.Arrived = null;
        neighborId = neighbor.ThingID;
        neighbor.needs.food.CurLevelPercentage = 1;
        neighbor.needs.rest.CurLevelPercentage = 1;
        neighbor.needs.joy.CurLevelPercentage = 1;
        Check(!neighbor.CanTakeOrder && !neighbor.Drafted, "default neighbor command restrictions hold");
        SecondSpecies();
    }

    static void SecondSpecies()
    {
        var second = DefDatabase<AssistantSpeciesDef>.GetNamed("NCT_Species");
        var point = map.Center + new IntVec3(-7, 0, 0);
        var secondHome = Build("NCT_WelcomeLamp", point).TryGetComp<CompCourtyard>();
        int partySize = ModsConfig.BiotechActive ? 4 : 1;
        second.familyChildren = true;
        second.minAge = 20;
        second.maxAge = 24;
        second.childMinAge = 4;
        second.childMaxAge = 5;
        secondHome.populationLimit = partySize;
        for (int index = 0; index < partySize; index++) Build("Bed", point + new IntVec3(index * 2, 0, 3));
        var food = ThingMaker.MakeThing(ThingDefOf.MealSimple);
        food.stackCount = food.def.stackLimit;
        GenSpawn.Spawn(food, point + new IntVec3(0, 0, 1), map);
        AccessTools.Field(typeof(CompCourtyard), "pendingPartySize").SetValue(secondHome, partySize);
        secondHome.progress = secondHome.ArrivalTicks - 1;
        secondHome.Advance(int.MaxValue);
        Check(secondHome.Residents.Count() == partySize, "maximum tick advance completes an arrival without integer overflow");
        var residents = secondHome.Residents.ToList();
        Check(residents.Count == partySize && home.Residents.Count() == 1, "species populations stay independent");
        Check(residents.Where(p => p.DevelopmentalStage.Adult()).All(p => p.ageTracker.AgeBiologicalYearsFloat >= 20 && p.ageTracker.AgeBiologicalYearsFloat <= 24), "parent arrivals respect a maximum age below 26");
        if (ModsConfig.BiotechActive)
        {
            var children = residents.Where(p => p.DevelopmentalStage.Child()).ToList();
            Check(children.Count == 2 && children.All(p => p.ageTracker.AgeBiologicalYearsFloat >= 4 && p.ageTracker.AgeBiologicalYearsFloat <= 5), "family arrivals use configured child ages and native developmental stages");
            Check(residents.All(p => p.ownership.OwnedBed != null), "family arrival claims all required sleeping slots");
        }
        else Check(residents.All(p => p.DevelopmentalStage.Adult()), "adult arrivals work with family configuration and Biotech disabled");
        secondHome.welcome = false;
        foreach (var pawn in residents)
        {
            pawn.jobs.StopAll();
            map.GetComponent<MapComponent_ArrivalWalk>().Finish(pawn);
            var wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 60000;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
        }
    }

    public static void Update(Map __instance)
    {
        if (done || busy || __instance != Find.CurrentMap || LongEventHandler.AnyEventNowOrWaiting) return;
        busy = true;
        try
        {
            map = __instance;
            Find.TickManager.Pause();
            if (stage == 0)
            {
                Setup();
                stage = 1;
                frames = 0;
            }
            else if (stage == 1)
            {
                Tick(30);
                if (++frames < 45) return;
                Check(neighbor.Position.DistanceTo(home.parent.Position) < 30, "neighbor walks into the colony through the native arrival job");
                neighbor.jobs.StopAll();
                neighbor.Position = home.parent.Position + new IntVec3(0, 0, 2);
                neighbor.Notify_Teleported(false, true);
                map.terrainGrid.SetTerrain(neighbor.Position, TerrainDef.Named("WoodPlankFloor"));
                Check(FilthMaker.TryMakeFilth(neighbor.Position, map, ThingDefOf.Filth_Dirt), "cleanable filth fixture is created on a floor");
                sawCleaning = false;
                var filth = neighbor.Position.GetThingList(map).OfType<Filth>().First(f => f.def == ThingDefOf.Filth_Dirt);
                var waitForFilth = JobMaker.MakeJob(JobDefOf.Wait);
                waitForFilth.expiryInterval = 2000;
                neighbor.jobs.StartJob(waitForFilth, JobCondition.InterruptForced);
                // Native cleaning waits 600 ticks after dirt is created or thickened.
                Tick(601);
                var cleaner = (WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("CleanFilth").Worker;
                Check(cleaner.HasJobOnThing(neighbor, filth), "native cleaning scanner accepts the fixture; home=" +
                    map.areaManager.Home[filth.Position] + " fog=" + filth.Fogged() + " reserve=" + neighbor.CanReserve(filth) +
                    " age=" + filth.TicksSinceThickened);
                neighbor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                stage = 2;
                frames = 0;
            }
            else if (stage == 2)
            {
                Tick(20);
                if (!sawCleaning && ++frames < 100) return;
                Check(sawCleaning, "native work AI selects the configured cleaning job; current=" + neighbor.CurJobDef +
                    " priority=" + Roles.Priority(neighbor, WorkTypeDefOf.Cleaning));
                var state = neighbor.GetComp<CompAssistant>();
                state.personalName = "Hazel";
                AssistantNames.Ensure(neighbor);
                Check(AssistantNames.Occupied(null).Contains("Hazel"), "international names participate in duplicate avoidance");
                state.SetWorkEnabled(WorkTypeDefOf.Cleaning, false);
                Check(Roles.Priority(neighbor, WorkTypeDefOf.Cleaning) == 0, "public work toggle updates effective priorities");
                neighbor.needs.rest.CurLevelPercentage = .1f;
                neighbor.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced);
                stage = 3;
                frames = 0;
            }
            else if (stage == 3)
            {
                Tick(20);
                if (neighbor.CurrentBed() != bed && ++frames < 100) return;
                Check(neighbor.CurrentBed() == bed, "starter sleeps in its assigned native bed");
                Find.CameraDriver.JumpToCurrentMapLoc(bed.Position);
                Find.CameraDriver.SetRootSize(5);
                foreach (var window in Find.WindowStack.Windows.ToList()) window.Close(false);
                stage = 31;
                frames = 0;
            }
            else if (stage == 31)
            {
                if (++frames < 10) return;
                ScreenCapture.CaptureScreenshot(PathFor("starter-sleep.png"));
                GameDataSaveLoader.SaveGame("FrameworkRoundTrip");
                savedMap = map;
                stage = 4;
                LongEventHandler.QueueLongEvent(() => GameDataSaveLoader.LoadGame("FrameworkRoundTrip"), "LoadingLongEvent", false, null);
            }
            else if (stage == 4 && map != savedMap)
            {
                neighbor = map.mapPawns.AllPawns.Single(p => p.ThingID == neighborId);
                var state = neighbor.GetComp<CompAssistant>();
                Check(state.personalName == "Hazel" && neighbor.Name.ToStringShort == "Hazel", "name survives save and reload");
                Check(state.assignedRoleDef.defName == "NCS_Helper", "role definition survives save and reload");
                Check(state.disabledWork.Contains(WorkTypeDefOf.Cleaning), "manual work settings survive save and reload");
                Check(state.Home != null && state.Home.Species == species, "home reference survives save and reload");
                Check(neighbor.ownership.OwnedBed != null && neighbor.CurrentBed() == neighbor.ownership.OwnedBed, "bed ownership and sleep survive save and reload");
                Tick(120);
                Check(errors.Count == 0, "no unexpected runtime errors");
                Finish();
            }
        }
        catch (Exception error) { Finish(error); }
        finally { busy = false; }
    }

    static void Finish(Exception failure = null)
    {
        done = true;
        File.WriteAllText(PathFor("result.txt"), (failure == null && errors.Count == 0 ? "PASS" : "FAIL") +
            " checks=" + checks.Count + " errors=" + errors.Count + "\n" + string.Join("\n", checks) + "\n" + failure);
        File.WriteAllLines(PathFor("errors.txt"), errors);
        File.WriteAllLines(PathFor("expected-errors.txt"), expectedErrors);
        Application.Quit();
    }
}
