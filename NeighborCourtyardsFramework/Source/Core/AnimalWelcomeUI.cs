using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public sealed partial class MainTabWindow_Assistants
    {
        public PawnKindDef WelcomeAnimalFilter { get; private set; }
        public static IEnumerable<ThingDef> AnimalWelcomeDefs => DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d=>d.GetCompProperties<CompProperties_AnimalWelcome>()?.animal!=null)
            .GroupBy(d=>d.GetCompProperties<CompProperties_AnimalWelcome>().animal).Select(g=>g.First()).OrderBy(d=>d.label);
        public void SetWelcomeAnimalFilter(PawnKindDef animal){SetFilter(null);WelcomeAnimalFilter=animal;}
        void DrawWelcomeFilter(Rect r)
        {
            if(!CourtyardStyle.Button(r,WelcomeAnimalFilter?.LabelCap.ToString()??Filter?.label??"NC_AllSpecies".Translate().ToString(),Filter!=null||WelcomeAnimalFilter!=null))return;
            var options=new List<FloatMenuOption>{new FloatMenuOption("NC_AllSpecies".Translate(),()=>SetFilter(null))};
            options.AddRange(AnimalWelcomeDefs.Select(d=>d.GetCompProperties<CompProperties_AnimalWelcome>().animal)
                .Select(a=>new FloatMenuOption(a.LabelCap,()=>SetWelcomeAnimalFilter(a))));
            options.AddRange(DefDatabase<AssistantSpeciesDef>.AllDefsListForReading.Select(s=>new FloatMenuOption(s.label,()=>SetFilter(s))));
            Find.WindowStack.Add(new CourtyardMenu(options));
        }
        void AnimalWelcomeCard(Rect r,Map map,ThingDef facility)
        {
            var props=facility.GetCompProperties<CompProperties_AnimalWelcome>();
            var home=CompAnimalWelcome.PrimaryFor(map,props.animal);
            CourtyardStyle.Round(r,CourtyardStyle.Card);
            CourtyardStyle.Icon(new Rect(r.x+12,r.y+8,56,56),3);
            CourtyardStyle.Label(new Rect(r.x+80,r.y+13,r.width-200,32),props.animal.LabelCap,font:GameFont.Medium);
            if(home==null)
            {
                CourtyardStyle.Label(new Rect(r.x+20,r.y+76,r.width-40,60),"NC_AnimalWelcomeMissing".Translate(facility.LabelCap),wrap:true);
                if(CourtyardStyle.Button(new Rect(r.x+18,r.y+444,r.width-36,30),"NC_Build".Translate()+" · "+facility.LabelCap))
                {SetFilter(null);SetPage(3);FilterBuildings((int)CourtyardBuildingUse.Animals,facility.label);}
                return;
            }
            string reason=home.BlockReason();
            CourtyardStyle.Label(new Rect(r.xMax-112,r.y+15,94,32),!home.enabled?"NC_AnimalWelcomePaused".Translate():reason!=null?"NC_WelcomeOnHold".Translate():"NC_Welcoming".Translate(),CourtyardStyle.Muted);
            CourtyardStyle.Label(new Rect(r.x+20,r.y+76,r.width-40,42),reason??"NC_AwaitingArrival".Translate().ToString(),reason==null?new Color(.30f,.43f,.23f):CourtyardStyle.Muted,wrap:true);
            Row(137,"NC_Residents".Translate(),home.Residents.Count()+" / "+home.PopulationLimit);
            CourtyardStyle.Label(new Rect(r.x+20,r.y+179,112,30),"NC_AnimalWelcomeCap".Translate(),CourtyardStyle.Muted);
            if(CourtyardStyle.Button(new Rect(r.xMax-154,r.y+179,38,30),"−"))home.PopulationLimit--;
            CourtyardStyle.Label(new Rect(r.xMax-112,r.y+179,50,30),home.PopulationLimit.ToString(),alignment:TextAnchor.MiddleCenter);
            if(CourtyardStyle.Button(new Rect(r.xMax-58,r.y+179,38,30),"+"))home.PopulationLimit++;
            CourtyardStyle.Label(new Rect(r.x+20,r.y+221,112,30),"NC_AnimalWelcomeSex".Translate(),CourtyardStyle.Muted);
            if(CourtyardStyle.Button(new Rect(r.x+150,r.y+221,r.width-170,30),home.GenderLabel))home.ChooseGender();
            Row(263,"NC_Preparation".Translate(),"NC_AnimalWelcomePreparation".Translate(props.food.LabelCap,props.foodCount));
            Row(305,"NC_Waited".Translate(),"NC_AnimalWelcomeWait".Translate((home.progress/60000f).ToString("0.##"),(props.waitTicks/60000f).ToString("0.##")));
            Row(347,"NC_AnimalWelcomeFacility".Translate(),home.parent.LabelCap);
            if(CourtyardStyle.Button(new Rect(r.x+18,r.y+389,r.width-36,30),"NC_Locate".Translate()))
            {Find.MainTabsRoot.SetCurrentTab(null);CameraJumper.TryJumpAndSelect(home.parent,CameraJumper.MovementMode.Cut);}
            if(CourtyardStyle.Button(new Rect(r.x+18,r.y+444,r.width-36,30),(home.enabled?"NC_AnimalWelcomePause":"NC_AnimalWelcomeStart").Translate(),home.enabled))home.enabled=!home.enabled;
            void Row(float y,string label,string value){CourtyardStyle.Label(new Rect(r.x+20,r.y+y,112,30),label,CourtyardStyle.Muted);CourtyardStyle.Label(new Rect(r.x+150,r.y+y,r.width-170,30),value);}
        }
    }
}
