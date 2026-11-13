using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace NeighborCourtyards
{
    public static class AssistantAbilities
    {
        public static void EnsureTraits(Pawn pawn)
        {
            var species=Roles.Profile(pawn);var traits=pawn?.story?.traits;
            if(species==null||traits==null||pawn.needs==null)return;
            foreach(var trait in traits.allTraits.ToList())
                if(!species.allowGrowthTraits && !species.innateTraits.Contains(trait.def))
                {
                    // Remove the unwanted trait without deleting unrelated gene effects.
                    trait.sourceGene=null;traits.RemoveTrait(trait);
                }
            foreach(var def in species.innateTraits)
                if(!traits.allTraits.Any(t=>t.def==def))traits.GainTrait(new Trait(def,0,true));
        }
        public static void EnsureSkills(Pawn pawn,bool newArrival)
        {
            var state=pawn.GetComp<CompAssistant>();
            if(state==null||state.skillProfileVersion>=1||!pawn.DevelopmentalStage.Adult()||pawn.skills==null)return;
            if(state.Settings.species.preserveChildSkills && state.grewUpInColony && !newArrival){state.skillProfileVersion=1;return;}
            var role=Roles.For(pawn);
            if(newArrival)
                foreach(var skill in pawn.skills.skills)
                {skill.Level=Rand.RangeInclusive(2,5);skill.passion=Passion.None;skill.xpSinceLastLevel=0;}
            foreach(var rule in role.skills)
            {
                var skill=pawn.skills.GetSkill(rule.skill);if(skill.TotallyDisabled)continue;
                int level=newArrival?Rand.RangeInclusive(rule.min,rule.max):rule.min;
                if(skill.Level<level)skill.Level=level;
                if(skill.passion<rule.passion)skill.passion=rule.passion;
            }
            if(role.primarySkill!=null&&!role.skills.Any(s=>s.skill==role.primarySkill))
            {var skill=pawn.skills.GetSkill(role.primarySkill);if(!skill.TotallyDisabled)skill.Level=Math.Max(skill.Level,role.minimumSkill);}
            state.skillProfileVersion=1;
        }
    }
    [HarmonyPatch(typeof(PawnGenerator),"GenerateTraits")]
    static class SpeciesOnlyGeneratedTraits
    {
        static bool Prefix(Pawn pawn)=>!Roles.Species(pawn);
        [HarmonyPriority(Priority.Last)]
        static void Postfix(Pawn pawn)=>AssistantAbilities.EnsureTraits(pawn);
    }
    [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
    static class SpeciesOnlyTraitGains
    {
        static bool Prefix(Pawn ___pawn,Trait trait)=>Roles.Profile(___pawn) is AssistantSpeciesDef species?species.allowGrowthTraits || species.innateTraits.Contains(trait.def):true;
    }
    public sealed class GameComponent_AssistantAbilities : GameComponent
    {
        public GameComponent_AssistantAbilities(Game game){}
        public override void LoadedGame()
        {
            foreach(var pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive.Where(Roles.Species))
            {AssistantAbilities.EnsureTraits(pawn);AssistantAbilities.EnsureSkills(pawn,false);}
        }
    }
}
