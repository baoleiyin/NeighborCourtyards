using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
namespace NeighborCourtyards
{
    public static class ArrivalWalk
    {
        public static bool TryEntry(Thing destination,out IntVec3 cell,params ThingDef[] races)
        {
            var map=destination.Map;
            cell=IntVec3.Invalid;
            if(races.Length==0||races.Any(r=>WelcomeSafety.BlockReason(destination,r)!=null))return false;
            var tried=new HashSet<IntVec3>();
            for(int attempt=0;attempt<24;attempt++)
            {
                if(!CellFinder.TryFindRandomEdgeCellWith(c=>!tried.Contains(c)&&c.Standable(map)&&!c.Fogged(map)&&
                    (map.TileInfo.AllowRoofedEdgeWalkIn||!map.roofGrid.Roofed(c))&&races.All(r=>WelcomeSafety.SafeCell(map,c,r))&&
                    map.reachability.CanReach(c,destination,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors,Danger.None)),map,.5f,out var entry))return false;
                tried.Add(entry);
                var path=map.pathFinder.FindPathNow(entry,destination,TraverseParms.For(TraverseMode.PassDoors,Danger.None),peMode:PathEndMode.Touch);
                try
                {
                    if(path.Found&&path.NodesReversed.All(c=>races.All(r=>WelcomeSafety.SafeCell(map,c,r)))){cell=entry;return true;}
                }
                finally{path.ReleaseToPool();}
            }
            return false;
        }
        public static void Start(Pawn pawn,Thing destination)
        {
            var arrivals=pawn.Map.GetComponent<MapComponent_ArrivalWalk>();
            arrivals.walkers.Add(new ArrivalWalker{pawn=pawn,destination=destination});
            arrivals.Resume(pawn,destination);
        }
    }
    public sealed class ArrivalWalker:IExposable
    {
        public Pawn pawn;public Thing destination;
        public void ExposeData(){Scribe_References.Look(ref pawn,"pawn");Scribe_References.Look(ref destination,"destination");}
    }
    public sealed class MapComponent_ArrivalWalk:MapComponent
    {
        public List<ArrivalWalker> walkers=new List<ArrivalWalker>();
        public MapComponent_ArrivalWalk(Map map):base(map){}
        public override void ExposeData()
        {
            if(Scribe.mode==LoadSaveMode.Saving)walkers.RemoveAll(w=>w.pawn==null||w.pawn.Destroyed||w.destination==null||w.destination.Destroyed);
            Scribe_Collections.Look(ref walkers,"ncArrivalWalkers",LookMode.Deep);if(walkers==null)walkers=new List<ArrivalWalker>();
        }
        public void Finish(Pawn pawn)=>walkers.RemoveAll(w=>w.pawn==pawn);
        public void Resume(Pawn pawn,Thing destination)
        {
            if(pawn.Downed||pawn.InMentalState||pawn.IsBurning()||pawn.Drafted||!pawn.Awake()||pawn.CurJob?.playerForced==true||
                pawn.needs?.food?.CurLevelPercentage<.15f||pawn.needs?.rest?.CurLevelPercentage<.15f||
                pawn.CurJob?.def==JobDefOf.Ingest||pawn.CurJob?.def==JobDefOf.LayDown||
                !pawn.CanReach(destination,PathEndMode.Touch,Danger.None))return;
            var def=DefDatabase<JobDef>.GetNamed("NC_Arrive");if(pawn.CurJob?.def==def)return;
            pawn.jobs.StartJob(JobMaker.MakeJob(def,destination),JobCondition.InterruptOptional);
        }
        public override void MapComponentTick()
        {
            if(Find.TickManager.TicksGame%250!=0)return;
            foreach(var w in walkers.ToList())
            {
                if(w.pawn==null||w.pawn.Dead||!w.pawn.Spawned||w.pawn.Map!=map||w.destination?.Spawned!=true||w.destination.Map!=map||
                    w.pawn.Position.DistanceToSquared(w.destination.Position)<=9){walkers.Remove(w);continue;}
                Resume(w.pawn,w.destination);
            }
        }
    }
    public sealed class JobDriver_Arrive:JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
            yield return Toils_General.Do(()=>pawn.Map.GetComponent<MapComponent_ArrivalWalk>().Finish(pawn));
        }
    }
}
