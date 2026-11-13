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
public static class LegacyProbe
{
    static readonly List<string> checks = new List<string>();
    static readonly List<string> errors = new List<string>();
    static bool started, done, busy;
    static int stage, frames;
    static Pawn neighbor;
    static Building_Bed bed;
    static string PathFor(string file) => Path.Combine(GenFilePaths.SaveDataFolderPath, file);

    static LegacyProbe()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (!done && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack);
        };
        var harmony = new Harmony("local.neighborcourtyards.legacyqa");
        harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(LegacyProbe), nameof(Entry)));
        harmony.Patch(AccessTools.Method(typeof(Root_Play), nameof(Root_Play.SetupForQuickTestPlay)), postfix: new HarmonyMethod(typeof(LegacyProbe), nameof(MapSize)));
        harmony.Patch(AccessTools.Method(typeof(Map), nameof(Map.MapUpdate)), postfix: new HarmonyMethod(typeof(LegacyProbe), nameof(Update)));
    }

    public static void MapSize() => Find.GameInitData.mapSize = 100;
    public static void Entry()
    {
        if (started || done || LongEventHandler.AnyEventNowOrWaiting || ++frames < 20 || File.Exists(PathFor("verify-legacy.txt"))) return;
        started = true;
        LongEventHandler.QueueLongEvent(() => { }, "Play", "GeneratingMap", false, null);
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks.Add(message);
        File.AppendAllText(PathFor("progress.txt"), "PASS " + message + "\n");
    }

    static Thing Build(Map map, string definition, IntVec3 cell)
    {
        var def = ThingDef.Named(definition);
        var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.WoodLog : null);
        thing.SetFaction(Faction.OfPlayer);
        return GenSpawn.Spawn(thing, cell, map);
    }

    static void Generate(Map map)
    {
        map.fogGrid.ClearAllFog();
        map.Biome.constantOutdoorTemperature = 21;
        foreach (var cell in CellRect.CenteredOn(map.Center, 8))
        {
            foreach (var thing in cell.GetThingList(map).ToList())
            {
                if (thing is Pawn) continue;
                if (thing.def.destroyable) thing.Destroy();
                else thing.DeSpawn();
            }
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            map.roofGrid.SetRoof(cell, null);
        }
        foreach (var pawn in map.mapPawns.FreeColonists) pawn.needs.food.CurLevelPercentage = 1;
        var home = Build(map, "MM_WelcomeLamp", map.Center).TryGetComp<CompCourtyard>();
        bed = (Building_Bed)Build(map, "Bed", map.Center + new IntVec3(3, 0, 0));
        foreach (var room in map.regionGrid.AllRooms) room.Temperature = 21;
        var meal = ThingMaker.MakeThing(ThingDefOf.MealSimple);
        meal.stackCount = meal.def.stackLimit;
        GenSpawn.Spawn(meal, map.Center + new IntVec3(1, 0, 0), map);
        home.populationLimit = 1;
        Check(home.BlockReason() == null, "baseline Miaomiao welcome prerequisites pass: " + home.BlockReason());
        Check(home.TryWelcome(out neighbor), "unmodified baseline core welcomes an existing Miaomiao");
        var state = neighbor.GetComp<CompAssistant>();
        state.personalName = "雪绒";
        AssistantNames.Ensure(neighbor);
        state.SetWorkEnabled(WorkTypeDefOf.Cleaning, false);
        neighbor.Position = bed.Position + new IntVec3(0, 0, -1);
        neighbor.Notify_Teleported(false, true);
        map.GetComponent<MapComponent_ArrivalWalk>().Finish(neighbor);
        neighbor.needs.rest.CurLevelPercentage = .1f;
        neighbor.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced);
        File.WriteAllLines(PathFor("legacy-state.txt"), new[] { neighbor.ThingID, state.assignedRoleDef.defName, state.home.ThingID, bed.ThingID });
    }

    static void Verify(Map map)
    {
        var saved = File.ReadAllLines(PathFor("legacy-state.txt"));
        neighbor = map.mapPawns.AllPawns.Single(p => p.ThingID == saved[0]);
        var state = neighbor.GetComp<CompAssistant>();
        Check(state.personalName == "雪绒" && neighbor.Name.ToStringShort == "雪绒", "0.6.9 save keeps the existing Miaomiao name");
        Check(state.assignedRoleDef.defName == saved[1], "0.6.9 save keeps the existing role reference");
        Check(state.Home != null && state.home.ThingID == saved[2], "0.6.9 save keeps its welcome point reference");
        Check(state.disabledWork.Contains(WorkTypeDefOf.Cleaning) && Roles.Priority(neighbor, WorkTypeDefOf.Cleaning) == 0, "0.6.9 save keeps explicit work settings");
        Check(neighbor.ownership.OwnedBed?.ThingID == saved[3], "0.6.9 save keeps bed ownership");
        Check(neighbor.CurrentBed() == neighbor.ownership.OwnedBed, "0.6.9 sleeping job resumes with the candidate core");
        for (int tick = 0; tick < 120; tick++) Find.TickManager.DoSingleTick();
        Check(Roles.Managed(neighbor) && !neighbor.CanTakeOrder && !neighbor.Drafted, "existing Miaomiao management policy survives upgrade");
        GameDataSaveLoader.SaveGame("LegacyUpgraded");
        Check(File.Exists(PathFor("Saves/LegacyUpgraded.rws")), "upgraded game saves successfully");
        Finish();
    }

    public static void Update(Map __instance)
    {
        if (done || busy || __instance != Find.CurrentMap || LongEventHandler.AnyEventNowOrWaiting) return;
        busy = true;
        try
        {
            Find.TickManager.Pause();
            if (File.Exists(PathFor("verify-legacy.txt"))) { Verify(__instance); return; }
            if (stage == 0) { Generate(__instance); stage = 1; frames = 0; return; }
            for (int tick = 0; tick < 20; tick++) Find.TickManager.DoSingleTick();
            if (neighbor.CurrentBed() != bed && ++frames < 100) return;
            Check(neighbor.CurrentBed() == bed, "baseline neighbor is sleeping before save");
            GameDataSaveLoader.SaveGame("LegacyBaseline");
            Check(File.Exists(PathFor("Saves/LegacyBaseline.rws")), "baseline save is created by the original core");
            Finish();
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
        Application.Quit();
    }
}
