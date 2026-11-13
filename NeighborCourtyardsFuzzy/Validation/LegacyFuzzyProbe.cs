using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using NeighborCourtyards;
using RimWorld;
using UnityEngine;
using Verse;

[StaticConstructorOnStartup]
public static class LegacyFuzzyProbe
{
    static bool started, done;
    static Map original;
    static string residentId;
    static readonly List<string> checks = new List<string>(), errors = new List<string>();
    static string At(string path) => Path.Combine(GenFilePaths.SaveDataFolderPath, path);
    static LegacyFuzzyProbe()
    {
        Application.logMessageReceived += (message, stack, type) => { if (!done && (type == LogType.Error || type == LogType.Exception)) errors.Add(message + "\n" + stack); };
        var harmony = new Harmony("local.neighborcourtyards.legacyfuzzyqa");
        harmony.Patch(AccessTools.Method(typeof(Root_Entry), "Update"), postfix: new HarmonyMethod(typeof(LegacyFuzzyProbe), nameof(Entry)));
        harmony.Patch(AccessTools.Method(typeof(Map), nameof(Map.MapUpdate)), postfix: new HarmonyMethod(typeof(LegacyFuzzyProbe), nameof(Update)));
    }
    public static void Entry()
    {
        if (started || done || LongEventHandler.AnyEventNowOrWaiting) return;
        started = true;
        LongEventHandler.QueueLongEvent(() => GameDataSaveLoader.LoadGame("LegacyBaseline"), "LoadingLongEvent", false, null);
    }
    static void Check(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); checks.Add(message); }
    public static void Update(Map __instance)
    {
        if (done || !started || __instance != Find.CurrentMap || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            Find.TickManager.Pause();
            var residents = NeighborRegistry.Residents(__instance).Where(p => p.def.defName == "NCFZ_NeighborRace").ToList();
            Check(residents.Count == 4, "four old furry neighbors are retained");
            Check(residents.All(p => p.RaceProps.Humanlike && p.IsColonist && !Roles.Helper(p)), "old humanlike identity is not converted");
            Check(residents.All(p => p.GetComp<CompAssistant>().Home != null), "old home references resolve");
            Check(residents.All(p => p.ownership.OwnedBed != null), "old bed ownership resolves");
            var resident = residents.First(p => p.GetComp<CompAssistant>().disabledWork.Contains(WorkTypeDefOf.Cleaning));
            Check(resident.GetComp<CompAssistant>().assignedRoleDef.defName == "NCFZ_Helper", "old assigned role and disabled cleaning survive");
            Check(resident.GetComp<CompAssistant>().preferredWork == null, "missing new preferred-work field defaults to null");
            if (original == null)
            {
                original = __instance; residentId = resident.ThingID;
                GameDataSaveLoader.SaveGame("LegacyUpgraded");
                LongEventHandler.QueueLongEvent(() => GameDataSaveLoader.LoadGame("LegacyUpgraded"), "LoadingLongEvent", false, null);
                return;
            }
            if (original == __instance) return;
            Check(resident.ThingID == residentId, "upgraded save reload preserves resident identity");
            Check(errors.Count == 0, "old save upgrade and reload have no runtime errors");
            Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }
    static void Finish(Exception failure)
    {
        done = true;
        File.WriteAllText(At("result.txt"), (failure == null && errors.Count == 0 ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count + "\n" + string.Join("\n", checks) + "\n" + failure);
        File.WriteAllLines(At("errors.txt"), errors);
        Application.Quit();
    }
}
