using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace NeighborCourtyards
{
    public sealed class WorkPriority
    {
        public WorkTypeDef workType;
        public int priority = 3;
    }
    public sealed class ToolStatBonus
    {
        public StatDef stat;
        public float factor = 1.15f;
    }
    public sealed class AssistantSkill
    {
        public SkillDef skill;
        public int min=8,max=10;
        public Passion passion=Passion.Minor;
    }
    public sealed class AssistantRoleDef : Def
    {
        public WorkTypeDef primaryWork;
        public SkillDef primarySkill;
        public int minimumSkill = 6;
        public ThingDef tool;
        [MustTranslate] public string services;
        public List<WorkPriority> work = new List<WorkPriority>();
        public List<ToolStatBonus> toolStats = new List<ToolStatBonus>();
        public List<WorkGiverDef> excludedWorkGivers = new List<WorkGiverDef>();
        public List<AssistantSkill> skills = new List<AssistantSkill>();
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors()) yield return e;
            if (primaryWork == null) yield return "Assistant role needs primaryWork.";
            if (minimumSkill < 0 || minimumSkill > 20) yield return "minimumSkill must be between 0 and 20.";
            if (work == null || work.Any(w => w == null || w.workType == null || w.priority < 0 || w.priority > 4))
                yield return "work must contain resolved work types and priorities between 0 and 4.";
            else if (work.Select(w => w.workType).Distinct().Count() != work.Count)
                yield return "work contains duplicate work types.";
            if (toolStats == null || toolStats.Any(b => b == null || b.stat == null || !DefinitionValidation.Positive(b.factor)))
                yield return "toolStats must contain resolved stats and finite positive factors.";
            if (skills == null || skills.Any(s => s == null || s.skill == null || s.min < 0 || s.max > 20 || s.min > s.max || !Enum.IsDefined(typeof(Passion), s.passion)))
                yield return "skills must contain resolved skills, ranges within 0..20 and valid passions.";
            else if (skills.Select(s => s.skill).Distinct().Count() != skills.Count)
                yield return "skills contains duplicate skills.";
            if (excludedWorkGivers == null || excludedWorkGivers.Any(w => w == null))
                yield return "excludedWorkGivers must contain resolved work givers.";
        }
    }
    public sealed class AssistantSpeciesDef : Def
    {
        public ThingDef race;
        public PawnKindDef pawnKind;
        public List<AssistantRoleDef> roles = new List<AssistantRoleDef>();
        public Type adapterClass = typeof(AssistantSpeciesAdapter);
        public int arrivalTicks = 120000, mealCost = 1, mealReserve = 0, defaultPopulationLimit = 6;
        public float mealRadius = 8, minimumMealNutrition = .5f, minAge = 20, maxAge = 35;
        public float childMinAge = 4, childMaxAge = 11;
        public Gender? fixedGender;
        public bool keepStartingApparel;
        // Opt-in only: existing humanlike species and saves retain their behavior.
        public bool helperMode;
        public bool allowOptionalWork;
        public bool familyChildren;
        public bool allowGrowthTraits, preserveChildSkills, preserveGrowingSchedules;
        public List<PawnKindDef> companionPets = new List<PawnKindDef>();
        public List<TraitDef> innateTraits = new List<TraitDef>();
        public AssistantNameSetDef naming;
        [MustTranslate] public string childTitle, babyTitle;
        AssistantSpeciesAdapter adapter;
        public AssistantSpeciesAdapter Adapter => adapter ?? (adapter = (AssistantSpeciesAdapter)Activator.CreateInstance(adapterClass));
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors()) yield return e;
            if (helperMode ? race?.race?.Animal != true : race?.race?.Humanlike != true)
                yield return helperMode ? "helperMode requires an animal race; use a new race Def instead of changing an existing humanlike race." : "race must be a humanlike ThingDef.";
            if (helperMode && (familyChildren || allowOptionalWork)) yield return "Helper mode currently supports adult configured services only; disable familyChildren and allowOptionalWork.";
            if (helperMode && adapterClass == typeof(AssistantSpeciesAdapter) && roles != null && roles.Where(r => r?.work != null).SelectMany(r => r.work).Any(w => w != null && w.priority > 0 && w.workType != WorkTypeDefOf.Cleaning && w.workType != WorkTypeDefOf.Hauling))
                yield return "Helper services other than Cleaning and Hauling require a custom adapter implementing SpecialJob and WorkTypeForJob.";
            if (pawnKind == null || pawnKind.race != race) yield return "pawnKind must reference this species' race.";
            var components = race?.comps?.OfType<CompProperties_Assistant>().ToList();
            if (components == null || components.Count != 1 || components[0].species != this)
                yield return "race needs exactly one CompProperties_Assistant referencing this species.";
            if (roles == null || roles.Count == 0 || roles.Any(r => r == null))
                yield return "roles must contain at least one resolved AssistantRoleDef.";
            else if (roles.Distinct().Count() != roles.Count)
                yield return "roles contains duplicate roles.";
            if (!DefinitionValidation.ConcreteAdapter(adapterClass, typeof(AssistantSpeciesAdapter)))
                yield return "adapterClass must be a concrete AssistantSpeciesAdapter with a public parameterless constructor.";
            if (arrivalTicks <= 0 || mealCost < 0 || mealReserve < 0 || (long)mealCost * 4 + mealReserve > int.MaxValue)
                yield return "Arrival ticks must be positive and meal counts must be nonnegative without overflow.";
            if (!DefinitionValidation.Positive(mealRadius) || !DefinitionValidation.Nonnegative(minimumMealNutrition))
                yield return "Meal radius must be finite and positive; nutrition must be finite and nonnegative.";
            if (defaultPopulationLimit < 0 || defaultPopulationLimit > 50)
                yield return "defaultPopulationLimit must be between 0 and 50.";
            if (!DefinitionValidation.AgeRange(minAge, maxAge)) yield return "Invalid adult arrival age range.";
            if (!DefinitionValidation.AgeRange(childMinAge, childMaxAge)) yield return "Invalid child arrival age range.";
            if (fixedGender.HasValue && !Enum.IsDefined(typeof(Gender), fixedGender.Value)) yield return "Invalid fixedGender.";
            if (companionPets == null || companionPets.Any(p => p?.race?.race?.Animal != true))
                yield return "companionPets must contain resolved animal PawnKindDefs.";
            if (innateTraits == null || innateTraits.Any(t => t == null)) yield return "innateTraits contains an unresolved trait.";
        }
    }
    // Extensions override only species-specific appearance or jobs. The framework
    // never references or loads a species assembly by name.
    public class AssistantSpeciesAdapter
    {
        public virtual int RoleIndex(Pawn pawn)
        {
            CompAssistant state = pawn.GetComp<CompAssistant>();
            if (state?.assignedRoleDef != null)
            {
                int index = state.Settings.species.roles.IndexOf(state.assignedRoleDef);
                if (index >= 0) return index;
            }
            return state?.assignedRole ?? 0;
        }
        public virtual void ConfigureAppearance(Pawn pawn, int role) { }
        /// <summary>Called once per member after an arrival commits. Throwing does not undo the arrival.</summary>
        public virtual void OnWelcomed(Pawn pawn, CompCourtyard courtyard) { }
        public virtual string NamePoolKey(Pawn pawn)=>null;
        public virtual void PortraitFraming(Pawn pawn,out Vector3 offset,out float zoom)
        { offset=new Vector3(0,0,.22f);zoom=2f; }
        public virtual Job SpecialJob(Pawn pawn) => null;
        public virtual WorkTypeDef WorkTypeForJob(Pawn pawn, Job job) => job?.workGiverDef?.workType;
        public virtual bool PreserveGizmo(Pawn pawn, Gizmo gizmo) => false;
    }
    public static class Roles
    {
        public static bool Helper(Pawn p) => Managed(p) && Profile(p).helperMode;
        public static AssistantSpeciesDef Profile(Pawn p) => p?.GetComp<CompAssistant>()?.Settings.species;
        public static bool Species(Pawn p) => Profile(p) != null;
        public static bool Managed(Pawn p) => Species(p) && p.Faction == Faction.OfPlayer && !p.IsPrisoner && !p.IsSlave;
        public static bool Adult(Pawn p) => Managed(p) && !p.Dead && p.DevelopmentalStage.Adult();
        public static int Role(Pawn p)
        {
            AssistantSpeciesDef profile = Profile(p);
            return profile == null ? 0 : Math.Max(0, Math.Min(profile.roles.Count - 1, profile.Adapter.RoleIndex(p)));
        }
        public static AssistantRoleDef For(Pawn p) => Profile(p)?.roles[Role(p)];
        public static int Priority(Pawn p, WorkTypeDef work)
        {
            if (work == null) return 0;
            if (work.defName == "Patient" || work.defName == "PatientBedRest") return 1;
            if (!Adult(p)) return 0;
            var state = p.GetComp<CompAssistant>();
            if (state.BlocksWork(work)) return 0;
            var priorities = For(p).work;
            for (int i = 0; i < priorities.Count; i++)
                if (priorities[i].workType == work && priorities[i].priority > 0)
                    return state.preferredWork == work ? 1 : state.preferredWork != null ? Math.Max(2, priorities[i].priority) : priorities[i].priority;
            return state.UsesOptionalWork ? 3 : 0;
        }
        public static bool HasTool(Pawn p)
        {
            if (!Adult(p)) return false;
            var tool = For(p).tool;
            var inventory = p.inventory?.innerContainer;
            if (tool == null || inventory == null) return false;
            for (int i = 0; i < inventory.Count; i++)
                if (inventory[i].def == tool) return true;
            return false;
        }
        public static bool Allowed(Pawn p, WorkGiver giver)
        {
            if (!Adult(p)) return giver.def.workType?.defName == "Patient" || giver.def.workType?.defName == "PatientBedRest";
            if (p.GetComp<CompAssistant>().Home?.shelter == true && giver.def.workType == null) return false;
            return !For(p).excludedWorkGivers.Contains(giver.def) && (giver.def.workType == null || Priority(p, giver.def.workType) > 0);
        }
    }
}
