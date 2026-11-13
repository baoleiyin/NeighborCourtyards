using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using NeighborCourtyards;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

[StaticConstructorOnStartup]
public static class FuzzyProbe
{
    static readonly List<string> checks = new List<string>();
    static readonly List<string> errors = new List<string>();
    static readonly List<Pawn> views = new List<Pawn>();
    static readonly List<Building_Bed> beds = new List<Building_Bed>();
    static bool started, done, busy, sawCleaning;
    static int stage, frames;
    static Pawn neighbor;
    static CompCourtyard courtyard;
    static Map map, savedMap;
    static string pawnId;
    static Thing meal;
    static float foodBefore;
    static string At(string file) => Path.Combine(GenFilePaths.SaveDataFolderPath, file);

    static FuzzyProbe()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (!done && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack);
        };
        var harmony = new Harmony("local.neighborcourtyards.fuzzyqa");
        harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(FuzzyProbe), nameof(Entry)));
        harmony.Patch(AccessTools.Method(typeof(Root_Play), nameof(Root_Play.SetupForQuickTestPlay)), postfix: new HarmonyMethod(typeof(FuzzyProbe), nameof(MapSize)));
        harmony.Patch(AccessTools.Method(typeof(Map), nameof(Map.MapUpdate)), postfix: new HarmonyMethod(typeof(FuzzyProbe), nameof(Update)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)), prefix: new HarmonyMethod(typeof(FuzzyProbe), nameof(JobStarted)));
    }

    public static void MapSize() => Find.GameInitData.mapSize = 100;
    public static void JobStarted(Pawn ___pawn, Job newJob)
    {
        if (___pawn == neighbor && newJob?.def == JobDefOf.Clean) sawCleaning = true;
    }
    static void Check(bool result, string text)
    {
        if (!result) throw new InvalidOperationException(text);
        checks.Add(text);
        File.AppendAllText(At("progress.txt"), "PASS " + text + "\n");
    }
    static void Tick(int count)
    {
        for (int index = 0; index < count; index++) Find.TickManager.DoSingleTick();
    }
    static void Hold(Pawn pawn)
    {
        pawn.jobs.StopAll();
        pawn.pather.StopDead();
        var job = JobMaker.MakeJob(JobDefOf.Wait);
        job.expiryInterval = 60000;
        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
    }
    static Thing Build(string name, IntVec3 cell, Rot4 rotation)
    {
        var def = ThingDef.Named(name);
        var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.WoodLog : null);
        thing.SetFaction(Faction.OfPlayer);
        return GenSpawn.Spawn(thing, cell, map, rotation);
    }
    static void Camera(IntVec3 center, float size)
    {
        foreach (var window in Find.WindowStack.Windows.ToList()) window.Close(false);
        Prefs.DevMode = false;
        ((CameraMapConfig)AccessTools.Field(typeof(CameraDriver), "config").GetValue(Find.CameraDriver)).sizeRange = new FloatRange(2, 60);
        Find.CameraDriver.JumpToCurrentMapLoc(center);
        Find.CameraDriver.SetRootSize(size);
    }
    public static void Entry()
    {
        if (started || done || LongEventHandler.AnyEventNowOrWaiting || ++frames < 20) return;
        started = true;
        try
        {
            var species = DefDatabase<AssistantSpeciesDef>.GetNamed("NCFZ_Species");
            Check(!species.ConfigErrors().Any(), "species configuration validates");
            Check(species.race.race.renderTree.defName == "NCFZ_RenderTree", "custom furry render tree is registered");
            Check(DefDatabase<PawnKindDef>.GetNamed("NCFZ_Neighbor").ConfigErrors().Count() == 0, "pawn kind configuration validates");
            Check(BuildingCatalog.Entries.Any(e => e.def.defName == "NCFZ_WelcomeLamp"), "welcome lamp appears in the common catalog");
            Check(CourtyardEncyclopedia.Entries.Any(e => e.article?.defName == "NCFZ_Introduction"), "species appears in the common encyclopedia");
            LongEventHandler.QueueLongEvent(() => { }, "Play", "GeneratingMap", false, null);
        }
        catch (Exception error) { Finish(error); }
    }
    static void Setup()
    {
        map.fogGrid.ClearAllFog();
        map.Biome.constantOutdoorTemperature = 21;
        map.weatherManager.TransitionTo(WeatherDefOf.Clear);
        foreach (var cell in CellRect.CenteredOn(map.Center, 14))
        {
            foreach (var thing in cell.GetThingList(map).ToList())
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy();
                else thing.DeSpawn();
            }
            map.terrainGrid.SetTerrain(cell, TerrainDef.Named("WoodPlankFloor"));
            map.roofGrid.SetRoof(cell, null);
            map.areaManager.Home[cell] = true;
        }
        foreach (var pawn in map.mapPawns.FreeColonists)
        {
            pawn.needs.food.CurLevelPercentage = 1;
            pawn.needs.rest.CurLevelPercentage = 1;
            Hold(pawn);
            pawn.Position = map.Center + new IntVec3(-18, 0, -18);
            pawn.Notify_Teleported(false, true);
        }
        courtyard = Build("NCFZ_WelcomeLamp", map.Center, Rot4.South).TryGetComp<CompCourtyard>();
        courtyard.populationLimit = 1;
        beds.Add((Building_Bed)Build("Bed", map.Center + new IntVec3(3, 0, 0), Rot4.South));
        foreach (var room in map.regionGrid.AllRooms) room.Temperature = 21;
        meal = ThingMaker.MakeThing(ThingDefOf.MealSimple);
        meal.stackCount = meal.def.stackLimit;
        GenSpawn.Spawn(meal, map.Center + IntVec3.North, map);
        Check(courtyard.BlockReason() == null, "welcome conditions are satisfied: " + courtyard.BlockReason());
        Check(courtyard.TryWelcome(out neighbor), "framework welcomes the furry neighbor");
        Check(meal.stackCount == meal.def.stackLimit - 1, "arrival charges one meal");
        Check(neighbor.ownership.OwnedBed == beds[0], "arrival claims a native bed");
        Check(neighbor.apparel.WornApparel.Any(a => a.def.defName == "Apparel_TribalA"), "arrival has its starting clothing");
        Check(neighbor.Name.ToStringShort.Length == 2, "framework assigns a name from the species pool");
        courtyard.welcome = false;
        pawnId = neighbor.ThingID;
        neighbor.needs.food.CurLevelPercentage = 1;
        neighbor.needs.rest.CurLevelPercentage = 1;
        neighbor.needs.joy.CurLevelPercentage = 1;
    }
    static void StandingViews()
    {
        views.Add(neighbor);
        for (int index = 1; index < 4; index++)
        {
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDef.Named("NCFZ_Neighbor"),
                Faction.OfPlayer, forceGenerateNewPawn: true, fixedBiologicalAge: 25, canGeneratePawnRelations: false));
            GenSpawn.Spawn(pawn, map.Center, map);
            views.Add(pawn);
        }
        for (int index = 0; index < views.Count; index++)
        {
            var pawn = views[index];
            Hold(pawn);
            map.GetComponent<MapComponent_ArrivalWalk>().Finish(pawn);
            pawn.Position = map.Center + new IntVec3(-4 + index * 2, 0, 5);
            pawn.Rotation = new Rot4(index);
            pawn.Notify_Teleported(false, true);
            pawn.Drawer.renderer.SetAllGraphicsDirty();
        }
        Camera(map.Center + new IntVec3(-1, 0, 5), 3);
    }
    static void SleepViews()
    {
        neighbor.ownership.UnclaimBed();
        beds[0].DeSpawn();
        beds.Clear();
        for (int index = 0; index < views.Count; index++)
        {
            var bed = (Building_Bed)Build("Bed", map.Center + new IntVec3(-5 + index * 3, 0, 0), new Rot4(index));
            beds.Add(bed);
            var pawn = views[index];
            Hold(pawn);
            pawn.Position = bed.Position + new IntVec3(0, 0, -2);
            pawn.Notify_Teleported(false, true);
            Check(pawn.ownership.ClaimBedIfNonMedical(bed), "native bed can be assigned at rotation " + index);
            pawn.needs.rest.CurLevelPercentage = .05f;
            pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced);
        }
        Camera(map.Center, 4);
    }
    public static void Update(Map __instance)
    {
        if (done || busy || __instance != Find.CurrentMap || LongEventHandler.AnyEventNowOrWaiting) return;
        busy = true;
        try
        {
            map = __instance;
            Find.TickManager.Pause();
            if (stage == 0) { Setup(); stage = 1; frames = 0; }
            else if (stage == 1)
            {
                Tick(30);
                if (++frames < 45) return;
                Check(neighbor.Position.DistanceTo(courtyard.parent.Position) < 30, "neighbor follows the native arrival walk");
                StandingViews(); stage = 2; frames = 0;
            }
            else if (stage == 2)
            {
                if (++frames < 15) return;
                Check(views.All(p => p.Drawer.renderer.renderTree.rootNode != null), "all four facing render trees initialize");
                ScreenCapture.CaptureScreenshot(At("standing-four-directions.png"));
                stage = 20; frames = 0;
            }
            else if (stage == 20)
            {
                if (++frames < 3) return;
                neighbor.Position = map.Center + new IntVec3(0, 0, 2);
                neighbor.Notify_Teleported(false, true);
                Check(FilthMaker.TryMakeFilth(neighbor.Position, map, ThingDefOf.Filth_Dirt), "cleaning fixture is created");
                Hold(neighbor);
                Tick(601);
                sawCleaning = false;
                neighbor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                stage = 3; frames = 0;
            }
            else if (stage == 3)
            {
                Tick(10);
                if (!sawCleaning && ++frames < 200) return;
                Check(sawCleaning, "normal work AI selects cleaning");
                Hold(neighbor);
                neighbor.needs.food.CurLevelPercentage = .15f;
                foodBefore = neighbor.needs.food.CurLevel;
                var eat = JobMaker.MakeJob(JobDefOf.Ingest, meal);
                eat.count = 1;
                neighbor.jobs.StartJob(eat, JobCondition.InterruptForced);
                Camera(map.Center + IntVec3.North, 2.5f);
                stage = 4; frames = 0;
            }
            else if (stage == 4)
            {
                Tick(10);
                if (frames == 5) ScreenCapture.CaptureScreenshot(At("eating.png"));
                if (neighbor.needs.food.CurLevel <= foodBefore && ++frames < 200) return;
                Check(neighbor.needs.food.CurLevel > foodBefore, "native meal ingestion restores nutrition");
                SleepViews(); stage = 5; frames = 0;
            }
            else if (stage == 5)
            {
                Tick(10);
                if (!views.Select((p, index) => p.CurrentBed() == beds[index]).All(v => v) && ++frames < 200) return;
                for (int index = 0; index < views.Count; index++) Check(views[index].CurrentBed() == beds[index], "actual sleeping pawn occupies bed rotation " + index);
                stage = 6; frames = 0;
            }
            else if (stage == 6)
            {
                if (++frames < 15) return;
                ScreenCapture.CaptureScreenshot(At("sleeping-four-directions.png"));
                stage = 60; frames = 0;
            }
            else if (stage == 60)
            {
                if (++frames < 3) return;
                neighbor.GetComp<CompAssistant>().SetWorkEnabled(WorkTypeDefOf.Cleaning, false);
                GameDataSaveLoader.SaveGame("FuzzyNeighborDemo");
                savedMap = map; stage = 7;
                LongEventHandler.QueueLongEvent(() => GameDataSaveLoader.LoadGame("FuzzyNeighborDemo"), "LoadingLongEvent", false, null);
            }
            else if (stage == 7 && map != savedMap)
            {
                neighbor = map.mapPawns.AllPawns.Single(p => p.ThingID == pawnId);
                Check(neighbor.GetComp<CompAssistant>().Home != null, "home reference survives reload");
                Check(neighbor.GetComp<CompAssistant>().assignedRoleDef.defName == "NCFZ_Helper", "role survives reload");
                Check(neighbor.GetComp<CompAssistant>().disabledWork.Contains(WorkTypeDefOf.Cleaning), "manual work choice survives reload");
                Check(neighbor.CurrentBed() == neighbor.ownership.OwnedBed, "sleep and bed ownership survive reload");
                Tick(90);
                Check(errors.Count == 0, "no runtime errors");
                Finish();
            }
        }
        catch (Exception error) { Finish(error); }
        finally { busy = false; }
    }
    static void Finish(Exception failure = null)
    {
        done = true;
        File.WriteAllText(At("result.txt"), (failure == null && errors.Count == 0 ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count + "\n" + string.Join("\n", checks) + "\n" + failure);
        File.WriteAllLines(At("errors.txt"), errors);
        Application.Quit();
    }
}
