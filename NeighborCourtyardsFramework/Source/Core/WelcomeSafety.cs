using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace NeighborCourtyards
{
    public sealed class MapComponent_WelcomeSafety : MapComponent
    {
        public const int DeathCooldown = 5 * 60000, HarmCooldown = 3 * 60000;
        public int deathUntil, harmUntil;
        public MapComponent_WelcomeSafety(Map map) : base(map) { }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref deathUntil, "ncWelcomeDeathUntil");
            Scribe_Values.Look(ref harmUntil, "ncWelcomeHarmUntil");
        }
        public void RecordDeath() => deathUntil = Math.Max(deathUntil, Find.TickManager.TicksGame + DeathCooldown);
        public void RecordHarm() => harmUntil = Math.Max(harmUntil, Find.TickManager.TicksGame + HarmCooldown);
        public string BlockReason()
        {
            if (GenHostility.AnyHostileActiveThreatToPlayer(map)) return "NC_NearbyHostiles".Translate().ToString();
            if (map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout)) return "NC_ToxicFallout".Translate().ToString();
            int tick = Find.TickManager.TicksGame;
            if (deathUntil > tick) return "NC_Recovering".Translate(((deathUntil - tick) / 60000f).ToString("0.0")).ToString();
            if (harmUntil > tick) return "NC_InjuryRecovery".Translate(((harmUntil - tick) / 60000f).ToString("0.0")).ToString();
            foreach (var pawn in map.mapPawns.AllPawns.Where(p => !p.Dead && !p.Suspended && WelcomeSafety.Protected(p)))
            {
                if ((pawn.Downed && !pawn.DevelopmentalStage.Baby()) || pawn.health.hediffSet.BleedRateTotal > .1f) return "NC_NeighborNeedsRescue".Translate().ToString();
                if (pawn.needs?.food?.CurLevelPercentage < .1f) return "NC_NeighborsHungry".Translate().ToString();
            }
            return null;
        }
    }
    public static class WelcomeSafety
    {
        public static bool Protected(Pawn pawn) => pawn != null && pawn.Faction == Faction.OfPlayer &&
            (Roles.Species(pawn) || DefDatabase<AssistantSpeciesDef>.AllDefsListForReading.Any(s => s.companionPets.Contains(pawn.kindDef)) ||
             DefDatabase<CourtyardPetArrivalDef>.AllDefsListForReading.Any(p => p.pet == pawn.kindDef) ||
             DefDatabase<CourtyardContentDef>.AllDefsListForReading.Any(c=>c.animals.Contains(pawn.kindDef)));
        public static bool SafeCell(Map map, IntVec3 cell, ThingDef race)
        {
            return !cell.GetThingList(map).Any(t => t is Building_Trap) &&
                !GenRadial.RadialCellsAround(cell, 1.5f, true).Where(c => c.InBounds(map)).Any(c => c.GetThingList(map).Any(t => t.def == ThingDefOf.Fire)) &&
                GenTemperature.SafeTemperatureRange(race).Includes(GenTemperature.GetTemperatureForCell(cell, map));
        }
        public static bool SafeBed(Building_Bed bed, ThingDef race) =>
            SafeCell(bed.Map, bed.Position, race) && !bed.Map.listerThings.ThingsOfDef(ThingDefOf.Fire).Any(t => t.Position.DistanceToSquared(bed.Position) <= 64);
        public static string BlockReason(Thing destination, ThingDef race)
        {
            var map = destination.Map;
            string reason = map.GetComponent<MapComponent_WelcomeSafety>().BlockReason();
            if (reason != null) return reason;
            if (map.listerThings.ThingsOfDef(ThingDefOf.Fire).Any(t => t.Position.DistanceToSquared(destination.Position) <= 225)) return "NC_NearbyFire".Translate().ToString();
            if (!GenTemperature.SafeTemperatureRange(race).Includes(map.mapTemperature.OutdoorTemp) ||
                !GenTemperature.SafeTemperatureRange(race).Includes(GenTemperature.GetTemperatureForCell(destination.Position, map))) return "NC_UnsafeTemperature".Translate().ToString();
            return null;
        }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    static class WelcomeDeath
    {
        static void Prefix(Pawn __instance, out Map __state) => __state = !__instance.Dead && WelcomeSafety.Protected(__instance) ? __instance.MapHeld : null;
        static void Postfix(Pawn __instance, Map __state)
        { if (__state != null && __instance.Dead) __state.GetComponent<MapComponent_WelcomeSafety>().RecordDeath(); }
    }
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    static class WelcomeHarm
    {
        static void Prefix(Pawn __instance, out Map __state) => __state = WelcomeSafety.Protected(__instance) ? __instance.MapHeld : null;
        static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt, Map __state)
        {
            if (__state != null && totalDamageDealt > 0 && dinfo.Instigator?.Faction == Faction.OfPlayer && dinfo.Def.ExternalViolenceFor(__instance))
                __state.GetComponent<MapComponent_WelcomeSafety>().RecordHarm();
        }
    }
}
