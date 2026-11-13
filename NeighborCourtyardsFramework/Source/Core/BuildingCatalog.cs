using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public enum CourtyardBuildingUse { Welcome, Home, Food, Workshop, Nursery, Recreation, Structure, Garden, Seating, Kitchen, ChildPlay, AnimalWelcome, AnimalRest, AnimalCare, Storage, Lighting, Floor, Medical, Animals }
    public sealed class CourtyardBuildingGroup
    {
        public CourtyardBuildingUse use;
        public List<string> buildings = new List<string>();
    }
    // An expansion opts its own buildable defs into the shared catalog. No naming convention or race assembly dependency.
    public sealed class CourtyardBuildingSetDef : Def
    {
        public AssistantSpeciesDef species;
        public CourtyardContentDef content;
        public List<CourtyardBuildingGroup> groups = new List<CourtyardBuildingGroup>();
        public override IEnumerable<string> ConfigErrors()
        {
            foreach(string e in base.ConfigErrors())yield return e;
            if(species==null&&content==null)yield return "Building set needs a species or content registration.";
            if (groups == null || groups.Any(g => g == null || g.buildings == null || g.buildings.Any(string.IsNullOrWhiteSpace)))
                yield return "Building groups must contain valid lists of definition names.";
            else if (groups.Any(g => !Enum.IsDefined(typeof(CourtyardBuildingUse), g.use)))
                yield return "Building group contains an unknown use category.";
        }
    }
    public sealed class CourtyardBuildingEntry
    {
        public BuildableDef def;
        public AssistantSpeciesDef species;
        public CourtyardContentDef content;
        public string ContentLabel => content?.label ?? species?.label ?? "";
        public CourtyardBuildingUse use;
        public Designator_Build designator;
        public string SearchText => def.label+" "+def.defName+" "+ContentLabel+" "+BuildingCatalog.Label(use)+" "+def.description;
    }
    public static class BuildingCatalog
    {
        static List<CourtyardBuildingEntry> entries;
        static HashSet<BuildableDef> owned;
        public static readonly CourtyardBuildingUse[] Uses={CourtyardBuildingUse.Welcome,CourtyardBuildingUse.Home,CourtyardBuildingUse.Workshop,CourtyardBuildingUse.Nursery,CourtyardBuildingUse.Recreation,CourtyardBuildingUse.Animals,CourtyardBuildingUse.Structure};
        public static readonly string[] Labels={"NC_Welcome".Translate().ToString(),"NC_BuildingFurniture".Translate().ToString(),"NC_BuildingTables".Translate().ToString(),"NC_BuildingProduction".Translate().ToString(),"NC_Nursery".Translate().ToString(),"NC_Recreation".Translate().ToString(),"NC_BuildingConstruction".Translate().ToString(),"NC_Garden".Translate().ToString(),"NC_BuildingSeats".Translate().ToString(),"NC_BuildingKitchen".Translate().ToString(),"NC_BuildingChildPlay".Translate().ToString(),"NC_BuildingAnimalWelcome".Translate().ToString(),"NC_BuildingAnimalRest".Translate().ToString(),"NC_BuildingAnimalCare".Translate().ToString(),"NC_BuildingStorage".Translate().ToString(),"NC_BuildingLighting".Translate().ToString(),"NC_BuildingFloor".Translate().ToString(),"NC_BuildingMedical".Translate().ToString(),"NC_BuildingAnimals".Translate().ToString()};
        public static readonly int[] Icons={3,6,5,4,7,8,9,10,6,5,7,10,6,10,4,10,9,7,10};
        public static string Label(CourtyardBuildingUse use)=>Labels[(int)use];
        public static List<CourtyardBuildingEntry> Entries
        {
            get
            {
                if(entries!=null)return entries;
                entries=new List<CourtyardBuildingEntry>();owned=new HashSet<BuildableDef>();
                var defs=DefDatabase<ThingDef>.AllDefs.Cast<BuildableDef>().Concat(DefDatabase<TerrainDef>.AllDefs);
                foreach(var sets in DefDatabase<CourtyardBuildingSetDef>.AllDefsListForReading.GroupBy(s=>s.modContentPack))
                foreach(var def in defs.Where(d=>d.modContentPack==sets.Key&&d.designationCategory!=null&&d.canGenerateDefaultDesignator))
                {
                    if(!owned.Add(def))continue;
                    // All registrations in a module participate, even when family furniture has its own set.
                    var set=sets.FirstOrDefault(s=>s.groups.Any(g=>g.buildings.Contains(def.defName)))??sets.First();
                    var group=set.groups.FirstOrDefault(g=>g.buildings.Contains(def.defName));
                    var use=Category(group?.use??InferUse(def));
                    entries.Add(new CourtyardBuildingEntry{def=def,species=set.species,content=set.content,use=use,designator=new Designator_Build(def)});
                }
                entries=entries.OrderBy(e=>Array.IndexOf(Uses,e.use)).ThenBy(e=>e.ContentLabel).ThenBy(e=>e.def.uiOrder).ThenBy(e=>e.def.label).ToList();
                return entries;
            }
        }
        // Keep old enum values readable by existing expansion XML; each entry receives one canonical use.
        public static CourtyardBuildingUse Category(CourtyardBuildingUse use)
        {
            switch(use)
            {
                case CourtyardBuildingUse.Food:case CourtyardBuildingUse.Seating:
                case CourtyardBuildingUse.Storage:case CourtyardBuildingUse.Lighting:
                case CourtyardBuildingUse.Medical:case CourtyardBuildingUse.Garden:return CourtyardBuildingUse.Home;
                case CourtyardBuildingUse.Kitchen:return CourtyardBuildingUse.Workshop;
                case CourtyardBuildingUse.ChildPlay:return CourtyardBuildingUse.Nursery;
                case CourtyardBuildingUse.AnimalWelcome:case CourtyardBuildingUse.AnimalRest:
                case CourtyardBuildingUse.AnimalCare:return CourtyardBuildingUse.Animals;
                case CourtyardBuildingUse.Floor:return CourtyardBuildingUse.Structure;
                default:return use;
            }
        }
        static CourtyardBuildingUse InferUse(BuildableDef def)
        {
            if(def is TerrainDef)return CourtyardBuildingUse.Floor;
            if(def is ThingDef t)
            {
                if(DefDatabase<CourtyardPetArrivalDef>.AllDefsListForReading.Any(p=>p.nest==t))return CourtyardBuildingUse.AnimalWelcome;
                if(t.GetCompProperties<CompProperties_Courtyard>()!=null)return CourtyardBuildingUse.Welcome;
                if(t.IsBed)return t.building.bed_humanlike?CourtyardBuildingUse.Home:CourtyardBuildingUse.AnimalRest;
                if(t.designationCategory.defName=="Joy")return CourtyardBuildingUse.Recreation;
                if(t.building?.isSittable==true)return CourtyardBuildingUse.Seating;
                if(t.GetCompProperties<CompProperties_Glower>()!=null)return CourtyardBuildingUse.Lighting;
            }
            switch(def.designationCategory.defName)
            {
                case "Production":return CourtyardBuildingUse.Workshop;
                case "Joy":return CourtyardBuildingUse.Recreation;
                case "Structure":return CourtyardBuildingUse.Structure;
                case "Floors":return CourtyardBuildingUse.Floor;
                default:return CourtyardBuildingUse.Home;
            }
        }
        public static bool Contains(BuildableDef def){var unused=Entries;return owned.Contains(def);}
        public static List<CourtyardBuildingEntry> Query(AssistantSpeciesDef species=null,int use=-1,string search="")=>Entries.Where(e=>(species==null||species==e.species)&&(use<0||e.use==Category((CourtyardBuildingUse)use))&&(string.IsNullOrWhiteSpace(search)||e.SearchText.IndexOf(search.Trim(),StringComparison.OrdinalIgnoreCase)>=0)).ToList();
        public static string Unavailable(CourtyardBuildingEntry entry)
        {
            var d=entry.designator;
            if(!entry.def.IsResearchFinished)return "NC_ResearchNeeded".Translate(string.Join("NC_ListSeparator".Translate().ToString(),entry.def.researchPrerequisites.Where(r=>!r.IsFinished).Select(r=>r.label))).ToString();
            if(Current.Game.Rules!=null&&!Current.Game.Rules.DesignatorAllowed(d))return "NC_RuleBlocksBuild".Translate().ToString();
            if(!d.Visible)return "NC_BuildRequirements".Translate().ToString();
            if(d.Disabled)return d.disabledReason??"NC_CannotBuild".Translate().ToString();
            return null;
        }
        public static bool BeginPlacement(CourtyardBuildingEntry entry,Map map)
        {
            if(map==null||!Find.Maps.Contains(map))return false;
            Current.Game.CurrentMap=map;
            string reason=Unavailable(entry);
            if(reason!=null){Messages.Message(reason,MessageTypeDefOf.RejectInput,false);return false;}
            // Native input keeps the material picker, resource checks, placement rules, rotation and blueprint behavior.
            var ev=Event.current==null?new Event{type=EventType.MouseDown,button=0}:new Event(Event.current);
            entry.designator.ProcessInput(ev);
            if(Find.DesignatorManager.SelectedDesignator==entry.designator)Find.MainTabsRoot.SetCurrentTab(null);
            return Find.DesignatorManager.SelectedDesignator==entry.designator;
        }
    }
    public sealed class Designator_CourtyardCatalog : Designator
    {
        public Designator_CourtyardCatalog(){defaultLabel="NC_Buildings".Translate().ToString();defaultDesc="";icon=ContentFinder<Texture2D>.Get("CourtyardUI/IconsBold");iconTexCoords=CourtyardStyle.IconUV(0);}
        public override bool Visible=>BuildingCatalog.Entries.Count>0;
        public override AcceptanceReport CanDesignateCell(IntVec3 c)=>false;
        public override void ProcessInput(Event ev)=>MainTabWindow_Assistants.Open(Find.CurrentMap,null,3);
    }
    // Filter the architect menu only. Keep the original defs and resolved designators intact for saves, rebuilding and eyedroppers.
    [HarmonyPatch(typeof(DesignationCategoryDef),"get_ResolvedAllowedDesignators")]
    static class CourtyardArchitectMenu
    {
        static void Postfix(ref IEnumerable<Designator> __result)=>__result=Filter(__result);
        static IEnumerable<Designator> Filter(IEnumerable<Designator> source)
        {
            foreach(var d in source)
            {
                if(d is Designator_Build build&&BuildingCatalog.Contains(build.PlacingDef))continue;
                if(d is Designator_Dropdown dropdown)
                {
                    var keep=dropdown.Elements.Where(e=>!(e is Designator_Build b&&BuildingCatalog.Contains(b.PlacingDef))).ToList();
                    if(keep.Count==0)continue;
                    if(keep.Count!=dropdown.Elements.Count){var replacement=new Designator_Dropdown();replacement.Order=dropdown.Order;foreach(var e in keep)replacement.Add(e);yield return replacement;continue;}
                }
                yield return d;
            }
        }
    }
}
