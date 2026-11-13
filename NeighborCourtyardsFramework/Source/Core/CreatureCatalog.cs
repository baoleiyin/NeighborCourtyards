using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace NeighborCourtyards
{
    // Animal expansions can register without a humanlike species or assistant roles.
    public sealed class CourtyardContentDef : Def
    {
        public List<PawnKindDef> animals = new List<PawnKindDef>();
        public override IEnumerable<string> ConfigErrors()
        {
            foreach(string error in base.ConfigErrors()) yield return error;
            if (animals == null)
            {
                yield return "animals must be a list of resolved animal PawnKindDefs.";
                yield break;
            }
            foreach(var kind in animals)
                if(kind?.race?.race?.Animal != true) yield return "Content animals must reference animal PawnKindDefs.";
        }
    }

    public sealed class CourtyardCreatureEntry
    {
        public PawnKindDef kind;
        public ThingDef Race => kind.race;
        public string Label => Race.LabelCap;
        public readonly List<Def> sources = new List<Def>();
        public string SourceLabel => string.Join(" / ", sources.Select(s=>s.label));
    }

    public enum CourtyardCreatureScope { All, Colony, Wild, Other }

    public static class CreatureCatalog
    {
        static List<CourtyardCreatureEntry> entries;
        public static IReadOnlyList<CourtyardCreatureEntry> Entries => entries ?? (entries=Build());
        static List<CourtyardCreatureEntry> Build()
        {
            var rows=new Dictionary<ThingDef,CourtyardCreatureEntry>();
            void Add(PawnKindDef kind,Def source)
            {
                if(kind?.race?.race?.Animal!=true || source==null || kind.race.comps.OfType<CompProperties_Assistant>().Any()) return;
                if(!rows.TryGetValue(kind.race,out var entry))
                    rows[kind.race]=entry=new CourtyardCreatureEntry{kind=kind};
                if(!entry.sources.Contains(source))entry.sources.Add(source);
            }
            foreach(var species in DefDatabase<AssistantSpeciesDef>.AllDefsListForReading)
            {
                foreach(var kind in species.companionPets)Add(kind,species);
                // Include the expansion's other animals even when they have no welcome nest.
                if(species.modContentPack!=null)
                    foreach(var kind in DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k=>k.modContentPack==species.modContentPack))Add(kind,species);
            }
            foreach(var arrival in DefDatabase<CourtyardPetArrivalDef>.AllDefsListForReading)Add(arrival.pet,arrival.species);
            foreach(var content in DefDatabase<CourtyardContentDef>.AllDefsListForReading)
                foreach(var kind in content.animals)Add(kind,content);
            return rows.Values.OrderBy(e=>e.SourceLabel).ThenBy(e=>e.Label).ThenBy(e=>e.Race.defName).ToList();
        }
        public static List<CourtyardCreatureEntry> Query(Def source=null,string search="") => Entries
            .Where(e=>(source==null||e.sources.Contains(source)) && Matches(e.Label+" "+e.Race.defName+" "+e.SourceLabel,search)).ToList();
        public static bool Matches(string value,string search) => string.IsNullOrWhiteSpace(search) || value.IndexOf(search.Trim(),StringComparison.OrdinalIgnoreCase)>=0;
        public static List<Pawn> Residents(Map map,CourtyardCreatureEntry entry,CourtyardCreatureScope scope=CourtyardCreatureScope.All,string search="")
        {
            if(map==null||entry==null)return new List<Pawn>();
            return map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead&&p.def==entry.Race&&
                (scope==CourtyardCreatureScope.All||Scope(p)==scope)&&Matches(p.LabelShortCap+" "+entry.Label,search))
                .OrderBy(p=>Scope(p)).ThenBy(p=>p.LabelShortCap.ToString()).ThenBy(p=>p.thingIDNumber).ToList();
        }
        public static List<Pawn> Individuals(Map map,Def source=null,ThingDef race=null,CourtyardCreatureScope scope=CourtyardCreatureScope.All,string search="")
        {
            if(map==null)return new List<Pawn>();
            var kinds=Query(source).Where(e=>race==null||e.Race==race).ToDictionary(e=>e.Race);
            return map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead&&kinds.TryGetValue(p.def,out var entry)&&
                (scope==CourtyardCreatureScope.All||Scope(p)==scope)&&
                Matches(p.LabelShortCap+" "+entry.Label+" "+entry.Race.defName+" "+entry.SourceLabel,search))
                .OrderBy(p=>Scope(p)).ThenBy(p=>kinds[p.def].SourceLabel).ThenBy(p=>kinds[p.def].Label)
                .ThenBy(p=>p.LabelShortCap.ToString()).ThenBy(p=>p.thingIDNumber).ToList();
        }
        public static CourtyardCreatureScope Scope(Pawn pawn) => pawn.Faction==Faction.OfPlayer?CourtyardCreatureScope.Colony:pawn.Faction==null?CourtyardCreatureScope.Wild:CourtyardCreatureScope.Other;
        public static string ScopeLabel(CourtyardCreatureScope scope) => ("NC_CreatureScope"+scope).Translate();
    }
}
