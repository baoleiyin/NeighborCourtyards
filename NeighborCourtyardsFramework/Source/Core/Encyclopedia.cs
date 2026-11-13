using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using Verse;

namespace NeighborCourtyards
{
    public enum CourtyardArticleKind { Species, Lineage, Source }
    // Each expansion owns its articles. Unloaded expansions never enter DefDatabase.
    public sealed class CourtyardArticleDef : Def
    {
        public AssistantSpeciesDef species;
        public CourtyardContentDef content;
        public CourtyardArticleKind kind;
        public AssistantRoleDef role;
        public ThingDef thing;
        public int order;
        public override IEnumerable<string> ConfigErrors()
        {
            foreach(var e in base.ConfigErrors()) yield return e;
            if(species==null&&content==null) yield return "Encyclopedia article requires a species or content registration.";
            if(kind==CourtyardArticleKind.Source && thing==null) yield return "Source article requires a material.";
        }
    }
    public sealed class CourtyardBookEntry
    {
        public AssistantSpeciesDef species;
        public CourtyardContentDef content;
        public string ContentLabel => content?.label ?? species?.label ?? "";
        public CourtyardArticleDef article;
        public ThingDef material;
        public string Label => material?.LabelCap.ToString() ?? article.LabelCap.ToString();
        public string Description => material?.description ?? article.description;
    }
    public static class CourtyardEncyclopedia
    {
        static List<CourtyardBookEntry> entries;
        static readonly Dictionary<ThingDef,List<string>> sources=new Dictionary<ThingDef,List<string>>();
        static readonly Dictionary<ThingDef,List<RecipeDef>> production=new Dictionary<ThingDef,List<RecipeDef>>();
        static readonly Dictionary<RecipeDef,List<ThingDef>> stations=new Dictionary<RecipeDef,List<ThingDef>>();
        static readonly Dictionary<ThingDef,List<string>> sourceText=new Dictionary<ThingDef,List<string>>();
        static string T(string key,params NamedArgument[] args)=>key.Translate(args).ToString();
        public static IReadOnlyList<CourtyardBookEntry> Entries { get { Ensure();return entries; } }
        static bool Material(ThingDef d)=>d!=null && d.category==ThingCategory.Item && !d.IsCorpse && !d.IsApparel && !d.IsWeapon && d.thingCategories!=null && d.thingCategories.Count>0 && d.graphicData!=null && !typeof(MinifiedThing).IsAssignableFrom(d.thingClass);
        static void AddSource(ThingDef d,string text)
        {
            if(d==null)return;
            if(!sources.TryGetValue(d,out var list))sources[d]=list=new List<string>();
            if(!list.Contains(text))list.Add(text);
        }
        static void Ensure()
        {
            if(entries!=null)return;
            var result=new List<CourtyardBookEntry>();
            var things=DefDatabase<ThingDef>.AllDefsListForReading;
            var articles=DefDatabase<CourtyardArticleDef>.AllDefsListForReading;
            foreach(var a in articles.Where(a=>(a.species!=null||a.content!=null) && a.kind!=CourtyardArticleKind.Source))
                result.Add(new CourtyardBookEntry{species=a.species,content=a.content,article=a});
            foreach(var r in DefDatabase<RecipeDef>.AllDefsListForReading)
                if(r.products!=null)foreach(var p in r.products)
                {
                    if(!production.TryGetValue(p.thingDef,out var recipes))production[p.thingDef]=recipes=new List<RecipeDef>();
                    recipes.Add(r);
                }
            foreach(var d in things)
            {
                if(d.building!=null && d.AllRecipes!=null)foreach(var recipe in d.AllRecipes)
                {
                    if(!stations.TryGetValue(recipe,out var users))stations[recipe]=users=new List<ThingDef>();
                    if(!users.Contains(d))users.Add(d);
                }
                if(d.plant?.harvestedThingDef!=null)AddSource(d.plant.harvestedThingDef,T("NC_BookHarvest",d.LabelCap,d.plant.harvestYield));
                if(d.building?.mineableThing!=null)AddSource(d.building.mineableThing,T("NC_BookMine",d.LabelCap,d.building.mineableYield));
                if(d.comps!=null)foreach(var c in d.comps)
                {
                    if(c is CompProperties_Shearable s)AddSource(s.woolDef,T("NC_BookShear",d.LabelCap,s.woolAmount,s.shearIntervalDays));
                    if(c is CompProperties_Milkable m)AddSource(m.milkDef,T("NC_BookMilk",d.LabelCap,m.milkAmount,m.milkIntervalDays));
                }
                if(d.race?.leatherDef!=null)AddSource(d.race.leatherDef,T("NC_BookButcher",d.LabelCap));
                if(d.race?.Animal==true && d.race.meatDef!=null)AddSource(d.race.meatDef,T("NC_BookButcher",d.LabelCap));
            }
            foreach(var species in DefDatabase<AssistantSpeciesDef>.AllDefsListForReading)
            {
                // Catalogue expansion-owned items only. Recipe inputs remain visible in their source details.
                var materials=new HashSet<ThingDef>(things.Where(d=>d.modContentPack==species.modContentPack && Material(d)));
                // Race meat is generated by the game, not an authored expansion resource.
                materials.ExceptWith(things.Where(d=>d.race!=null).Select(d=>d.race.meatDef));
                foreach(var material in materials)result.Add(new CourtyardBookEntry{species=species,material=material});
                if(!result.Any(e=>e.species==species && e.article?.kind==CourtyardArticleKind.Species))
                    result.Add(new CourtyardBookEntry{species=species,article=new CourtyardArticleDef{label=species.label,description=species.race.description,species=species}});
            }
            foreach(var content in DefDatabase<CourtyardContentDef>.AllDefsListForReading)
                foreach(var material in things.Where(d=>d.modContentPack==content.modContentPack && Material(d) && !things.Any(r=>r.race?.meatDef==d)))
                    if(!result.Any(e=>e.material==material)) result.Add(new CourtyardBookEntry{content=content,material=material});
            entries=result.OrderBy(e=>e.ContentLabel).ThenBy(e=>e.material==null?0:1).ThenBy(e=>e.article?.order??0).ThenBy(e=>e.Label).ToList();
        }
        public static List<CourtyardBookEntry> Query(AssistantSpeciesDef species=null,bool materials=false,string search="")=>Entries.Where(e=>(species==null||e.species==species)&&(e.material!=null)==materials&&(string.IsNullOrWhiteSpace(search)||(e.Label+" "+e.Description+" "+e.ContentLabel).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
        public static List<string> Sources(ThingDef material)
        {
            Ensure();if(sourceText.TryGetValue(material,out var cached))return cached;
            var result=sources.TryGetValue(material,out var found)?new List<string>(found):new List<string>();
            foreach(var a in DefDatabase<CourtyardArticleDef>.AllDefsListForReading.Where(a=>a.kind==CourtyardArticleKind.Source && a.thing==material))result.Add(a.description);
            if(production.TryGetValue(material,out var recipes))foreach(var recipe in recipes)
            {
                // Recipes with no currently loaded workbench are not obtainable recipes.
                if(!stations.TryGetValue(recipe,out var users)||users.Count==0)continue;
                string text=recipe.LabelCap+"\n"+T("NC_BookAt",string.Join(" / ",users.Select(u=>u.label)));
                if(recipe.ingredients!=null && recipe.ingredients.Count>0)text+="\n"+T("NC_BookInputs","\n"+string.Join("\n",recipe.ingredients.Select(i=>"  "+IngredientText(i,recipe))));
                text+="\n"+T("NC_BookOutput",string.Join("; ",recipe.products.Select(p=>p.thingDef.label+" × "+p.count)));
                var research=new List<ResearchProjectDef>();
                if(recipe.researchPrerequisite!=null)research.Add(recipe.researchPrerequisite);
                if(recipe.researchPrerequisites!=null)research.AddRange(recipe.researchPrerequisites);
                if(research.Count>0)text+="\n"+T("NC_BookResearch",string.Join(" / ",research.Distinct().Select(r=>r.label)));
                result.Add(text);
            }
            if(result.Count==0)result.Add(T("NC_BookNoSource"));
            sourceText[material]=result;return result;
        }
        public static string RoleText(AssistantRoleDef role)=>role==null?"":role.LabelCap+"\n"+role.services+(role.tool==null?"":"\n"+T("NC_BookTool",role.tool.LabelCap));
        static string IngredientText(IngredientCount ingredient,RecipeDef recipe)
        {
            // Generated make-item recipes can have an empty global filter while each
            // ingredient still specifies its fixed resource.
            var allowed=ingredient.filter.AllowedThingDefs.Where(d=>recipe.fixedIngredientFilter==null||recipe.fixedIngredientFilter.AllowedDefCount==0||recipe.fixedIngredientFilter.Allows(d)).OrderBy(d=>d.label).ToList();
            if(allowed.Count<=8)return string.Join(" / ",allowed.Select(d=>d.label+" × "+ingredient.CountRequiredOfFor(d,recipe)));
            // Broad food categories should not expand into a list of every animal's meat.
            var groups=allowed.GroupBy(d=>IngredientCategory(d)).ToList();
            return string.Join(" / ",groups.Select(group=>
            {
                var counts=group.Select(d=>ingredient.CountRequiredOfFor(d,recipe)).Distinct().ToList();
                if(counts.Count==1)return group.Key+" × "+counts[0];
                if(recipe.IngredientValueGetter is IngredientValueGetter_Nutrition)
                    // Numeric NamedArguments use the game's default integer-style formatting.
                    return T("NC_BookNutrition",ingredient.GetBaseCount().ToString("0.####",CultureInfo.InvariantCulture),group.Key);
                return string.Join(" / ",group.Select(d=>d.label+" × "+ingredient.CountRequiredOfFor(d,recipe)));
            }));
        }
        static string IngredientCategory(ThingDef thing)
        {
            // These are vanilla category identities, not expansion-specific references.
            foreach(var leaf in thing.thingCategories??new List<ThingCategoryDef>())
                for(var category=leaf;category!=null;category=category.parent)
                    if(category.defName=="MeatRaw"||category.defName=="PlantFoodRaw"||category.defName=="EggsFertilized"||category.defName=="EggsUnfertilized")return category.label;
            return thing.label;
        }
    }
}
