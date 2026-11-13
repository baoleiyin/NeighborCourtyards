using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace NeighborCourtyards
{
    public sealed class AssistantNamePool
    {
        public string key, label, requiredCharacters;
        public float weight=1;
        public bool requireTwoChineseCharacters = true;
        public List<string> names=new List<string>();
        public bool Accepts(string name) =>
            (requireTwoChineseCharacters ? AssistantNames.IsPersonalName(name) : AssistantNames.IsNameText(name)) &&
            (requiredCharacters.NullOrEmpty() || name.Any(c => requiredCharacters.Contains(c)));
    }
    public sealed class AssistantNameSetDef : Def
    {
        public List<AssistantNamePool> pools=new List<AssistantNamePool>();
        public List<string> sharedNames=new List<string>();
        public float sharedChance=.2f;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach(string e in base.ConfigErrors())yield return e;
            if (pools == null || pools.Count == 0 || pools.Any(p => p == null))
            {
                yield return "pools must contain at least one naming pool.";
                yield break;
            }
            if (pools.Select(p => p.key).Distinct().Count() != pools.Count) yield return "Naming pool keys must be unique.";
            if (!DefinitionValidation.Nonnegative(sharedChance) || sharedChance > 1) yield return "sharedChance must be finite and between 0 and 1.";
            if (sharedNames == null || sharedNames.Any(n => !AssistantNames.IsNameText(n))) yield return "sharedNames must contain valid single-line names.";
            foreach(var p in pools)
                if(p.key.NullOrEmpty() || !DefinitionValidation.Positive(p.weight) || p.names == null || p.names.Count==0 || p.names.Any(n=>!p.Accepts(n)) || p.names.Distinct().Count()!=p.names.Count)
                    yield return "Invalid whitelist or required character in naming pool "+p.key;
        }
        public List<string> Names(AssistantNamePool pool)=>pool.names.Concat(sharedNames.Where(pool.Accepts)).Distinct().ToList();
    }
    public static class AssistantNames
    {
        public static bool IsNameText(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 64 &&
            name == name.Trim() && !name.Any(c => char.IsControl(c) || c == '<' || c == '>');
        public static bool IsPersonalName(string name)=>name!=null&&name.Length==2&&name.All(c=>c>='\u4e00'&&c<='\u9fff');
        public static List<Pawn> Residents()=>PawnsFinder.AllMapsWorldAndTemporary_Alive.Where(p=>Roles.Managed(p)&&!p.Dead&&!p.Destroyed).Distinct().ToList();
        public static string Title(Pawn p)=>p.DevelopmentalStage.Baby()?(Roles.Profile(p)?.babyTitle ?? "NC_DefaultBabyTitle".Translate().ToString()):p.DevelopmentalStage.Child()?(Roles.Profile(p)?.childTitle ?? "NC_DefaultChildTitle".Translate().ToString()):Roles.For(p)?.label;
        public static string Personal(Pawn p)=>p.GetComp<CompAssistant>()?.personalName??p.Name?.ToStringShort??"";
        public static string Display(Pawn p)=>Personal(p)+" · "+Title(p);
        public static string HomeDescription(Pawn p)
        {
            var home=p.GetComp<CompAssistant>()?.home;
            string place=p.Map?.Parent?.Label??"NC_NoAssignedHome".Translate().ToString();
            return home?.Spawned==true?place+" / "+home.Label+"（"+home.Position.x+", "+home.Position.z+"）":place;
        }
        public static HashSet<string> Occupied(Pawn except)=>new HashSet<string>(Residents().Where(p=>p!=except).Select(p=>Personal(p)).Where(IsNameText));
        // Sample a whole approved name. Exhaustion permits homonyms; it never manufactures suffixes or new syllables.
        public static string Pick(AssistantNameSetDef set,AssistantNamePool pool,HashSet<string> occupied)
        {
            var all=set.Names(pool);
            var available=all.Where(n=>!occupied.Contains(n)).ToList();
            if(available.Count==0)available=all;
            var local=available.Where(pool.names.Contains).ToList();
            var shared=available.Where(n=>set.sharedNames.Contains(n)&&pool.Accepts(n)).ToList();
            if(shared.Count>0&&(local.Count==0||Rand.Value<set.sharedChance))return shared.RandomElement();
            return local.Count>0?local.RandomElement():available.RandomElement();
        }
        public static void Ensure(Pawn pawn)
        {
            var c=pawn.GetComp<CompAssistant>();var set=c?.Settings.species.naming;
            if(set==null||!Roles.Managed(pawn)||pawn.Dead||Scribe.mode!=LoadSaveMode.Inactive)return;
            if(c.personalName.NullOrEmpty())
            {
                var occupied=Occupied(pawn);
                string key=c.Settings.species.Adapter.NamePoolKey(pawn);
                var eligible=set.pools.Where(p=>key.NullOrEmpty()||p.key==key).ToList();
                if(eligible.Count==0){Log.ErrorOnce("No assistant name pool for "+key+" in "+set.defName,set.shortHash);return;}
                var unused=eligible.Where(p=>set.Names(p).Any(n=>!occupied.Contains(n))).ToList();
                // Use another eligible weighted pool before accepting a duplicate when no colour restriction exists.
                var pool=(unused.Count>0?unused:eligible).RandomElementByWeight(p=>p.weight);
                string previous=pawn.Name?.ToStringShort?.Split('·')[0].Trim();
                c.personalName=set.Names(pool).Contains(previous)&&!occupied.Contains(previous)?previous:Pick(set,pool,occupied);
                c.namePoolKey=pool.key;
                c.namingVersion=1;
            }
            if(!(pawn.Name is NameSingle)||pawn.Name.ToStringFull!=c.personalName)pawn.Name=new NameSingle(c.personalName,false);
        }
        public static void Migrate()
        {
            foreach(var pawn in Residents().OrderBy(p=>p.thingIDNumber))Ensure(pawn);
            var people=Residents().Where(p=>p.GetComp<CompAssistant>().personalName!=null).ToDictionary(p=>p.ThingID);
            foreach(var map in Find.Maps)
            foreach(var record in map.GetComponent<MapComponent_AssistantLedger>().records)
                if(people.TryGetValue(record.pawnId,out Pawn pawn))record.pawnName=Personal(pawn);
        }
    }
    // Run after the whole save has loaded so map order cannot lose cross-map/world-pawn name reservations.
    public sealed class GameComponent_AssistantNames : GameComponent
    {
        public GameComponent_AssistantNames(Game game) { }
        public override void LoadedGame()=>AssistantNames.Migrate();
        public override void StartedNewGame()=>AssistantNames.Migrate();
    }
}
