using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace NeighborCourtyards
{
    public sealed class DailyOutput : IExposable
    {
        public int day, count;
        public void ExposeData() { Scribe_Values.Look(ref day, "day"); Scribe_Values.Look(ref count, "count"); }
    }
    public sealed class AssistantOutput : IExposable
    {
        public string pawnId, pawnName;
        public AssistantSpeciesDef species;
        public CourtyardContentDef content;
        public AssistantRoleDef role;
        public ThingDef product;
        public int total;
        public List<DailyOutput> days = new List<DailyOutput>();
        public int Count(int window, int day) => window == 0 ? total : days.Where(d => d.day <= day && d.day > day - window).Sum(d => d.count);
        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId"); Scribe_Values.Look(ref pawnName, "pawnName");
            Scribe_Defs.Look(ref species, "species"); Scribe_Defs.Look(ref role, "role"); Scribe_Defs.Look(ref product, "product");
            Scribe_Defs.Look(ref content,"content");
            Scribe_Values.Look(ref total, "total"); Scribe_Collections.Look(ref days, "days", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && days == null) days = new List<DailyOutput>();
        }
    }
    public sealed class MapComponent_AssistantLedger : MapComponent
    {
        public List<AssistantOutput> records = new List<AssistantOutput>();
        public int startedTick = -1;
        public int Day => GenDate.DayOfYear(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x) + GenDate.Year(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x) * 60;
        public MapComponent_AssistantLedger(Map map) : base(map) { }
        public override void FinalizeInit() { base.FinalizeInit(); if (startedTick < 0) startedTick = Find.TickManager.TicksGame; }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref startedTick, "ncLedgerStarted", -1);
            Scribe_Collections.Look(ref records, "ncOutputRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && records == null) records = new List<AssistantOutput>();
        }
        public void Record(Pawn worker, ThingDef product, int count)
        {
            if (!Roles.Adult(worker) || worker.Map != map || product == null || count <= 0) return;
            var role = Roles.For(worker); var species = Roles.Profile(worker);
            var row = records.FirstOrDefault(r => r.pawnId == worker.ThingID && r.product == product && r.role == role);
            if (row == null) { row = new AssistantOutput { pawnId = worker.ThingID, species = species, role = role, product = product }; records.Add(row); }
            row.pawnName = AssistantNames.Personal(worker); row.total += count;
            int day = Day; row.days.RemoveAll(d => d.day < day - 6);
            var daily = row.days.FirstOrDefault(d => d.day == day);
            if (daily == null) { daily = new DailyOutput { day = day }; row.days.Add(daily); }
            daily.count += count;
        }
        // Custom animal collectors call this once for actual placed products, regardless of the worker's species.
        public void RecordAnimal(Pawn animal,CourtyardContentDef content,ThingDef product,int count)
        {
            if(animal==null||animal.Map!=map||content==null||product==null||count<=0)return;
            var row=records.FirstOrDefault(r=>r.pawnId==animal.ThingID&&r.product==product&&r.content==content);
            if(row==null){row=new AssistantOutput{pawnId=animal.ThingID,content=content,product=product};records.Add(row);}
            row.pawnName=animal.LabelShort;row.total+=count;
            int day=Day;row.days.RemoveAll(d=>d.day<day-6);
            var daily=row.days.FirstOrDefault(d=>d.day==day);
            if(daily==null){daily=new DailyOutput{day=day};row.days.Add(daily);}daily.count+=count;
        }
        public IEnumerable<AssistantOutput> Query(AssistantSpeciesDef species = null, Pawn pawn = null) => records.Where(r => r.product != null && (species == null || r.species == species) && (pawn == null || r.pawnId == pawn.ThingID));
        public int Total(AssistantSpeciesDef species = null, Pawn pawn = null, int window = 1) => Query(species, pawn).Sum(r => r.Count(window, Day));
    }
    public static class AssistantProduction
    {
        public static void Record(Pawn pawn, ThingDef product, int count) => pawn?.Map?.GetComponent<MapComponent_AssistantLedger>()?.Record(pawn, product, count);
        [ThreadStatic] internal static Pawn gathering;
        internal static void During(Pawn pawn, Action action)
        {
            Pawn old = gathering; gathering = Roles.Adult(pawn) ? pawn : null;
            try { action(); } finally { gathering = old; }
        }
    }
    // Bill_Production.Notify_IterationCompleted receives INGREDIENTS, not products.
    // This native callback receives final products after ingredients are consumed.
    [HarmonyPatch(typeof(RecordsUtility), nameof(RecordsUtility.Notify_BillDone))]
    static class RecordCompletedRecipe
    {
        static void Postfix(Pawn billDoer, List<Thing> products)
        {
            if (!Roles.Adult(billDoer) || products == null) return;
            foreach (Thing thing in products) AssistantProduction.Record(billDoer, thing.def, thing.stackCount);
        }
    }
    [HarmonyPatch(typeof(JobDriver_PlantWork), "MakeNewToils")]
    static class ObserveHarvest
    {
        static void Postfix(JobDriver_PlantWork __instance, ref IEnumerable<Toil> __result)
        { if (Roles.Adult(__instance.pawn)) __result = Wrap(__result, __instance.pawn); }
        static IEnumerable<Toil> Wrap(IEnumerable<Toil> source, Pawn pawn)
        {
            foreach (Toil toil in source)
            {
                if (toil.tickIntervalAction != null) { Action<int> original = toil.tickIntervalAction; toil.tickIntervalAction = delta => AssistantProduction.During(pawn, () => original(delta)); }
                yield return toil;
            }
        }
    }
    [HarmonyPatch(typeof(CompHasGatherableBodyResource), nameof(CompHasGatherableBodyResource.Gathered))]
    static class ObserveAnimalYield
    {
        static void Prefix(Pawn doer, out Pawn __state) { __state = AssistantProduction.gathering; AssistantProduction.gathering = Roles.Adult(doer) ? doer : null; }
        static void Finalizer(Pawn __state) { AssistantProduction.gathering = __state; }
    }
    [HarmonyPatch]
    static class RecordGatherPlacement
    {
        static MethodBase TargetMethod() => typeof(GenPlace).GetMethods().Single(m => m.Name == "TryPlaceThing" && m.GetParameters().Any(p => p.ParameterType == typeof(Thing).MakeByRefType()));
        static void Prefix(ref Action<Thing, int> placedAction)
        {
            Pawn pawn = AssistantProduction.gathering;
            if (pawn == null) return;
            Action<Thing, int> original = placedAction;
            // Use actual placed quantities: failed harvests and partial stack merges must not inflate totals.
            placedAction = (thing, count) => { original?.Invoke(thing, count); AssistantProduction.Record(pawn, thing.def, count); };
        }
    }
}
