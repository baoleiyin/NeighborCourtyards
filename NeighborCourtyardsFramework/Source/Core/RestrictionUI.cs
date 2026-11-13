using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public sealed class Dialog_AssistantAreas : Window
    {
        readonly CompCourtyard home;
        public Dialog_AssistantAreas(CompCourtyard home) { this.home=home; doCloseX=true; forcePause=true; }
        public override Vector2 InitialSize => new Vector2(560,330);
        public override void DoWindowContents(Rect r)
        {
            if (!home.parent.Spawned) { Close(); return; }
            home.ValidateAreas();
            CourtyardStyle.Round(r,CourtyardStyle.Paper);
            CourtyardStyle.Label(new Rect(20,12,r.width-40,36),"NC_AreaSettings".Translate(),font:GameFont.Medium);
            Row(65,"NC_DailyArea",home.serviceArea?.Label ?? (home.manageArea?"NC_Unrestricted":"NC_KeepNativeArea").Translate().ToString(),false);
            Row(117,"NC_ShelterArea",home.shelterArea?.Label ?? "NC_UseDailyArea".Translate().ToString(),true);
            CourtyardStyle.Label(new Rect(20,174,r.width-40,64),"NC_AreaHint".Translate(),CourtyardStyle.Muted,wrap:true);
            if(CourtyardStyle.Button(new Rect(20,r.height-48,r.width-40,32),"NC_DrawAreas".Translate()))Find.WindowStack.Add(new Dialog_ManageAreas(home.parent.Map));
            void Row(float y,string key,string value,bool shelter)
            {
                CourtyardStyle.Label(new Rect(20,y,140,34),key.Translate());
                if(!CourtyardStyle.Button(new Rect(164,y,r.width-184,34),value))return;
                var choices=new List<FloatMenuOption>{new FloatMenuOption((shelter?"NC_UseDailyArea":"NC_Unrestricted").Translate(),()=>{if(shelter)home.SetShelterArea(null);else home.SetArea(null);})};
                if(!shelter)choices.Add(new FloatMenuOption("NC_KeepNativeArea".Translate(),home.UseNativeAreas));
                foreach(var area in home.parent.Map.areaManager.AllAreas.OfType<Area_Allowed>())
                {
                    Area picked=area;
                    choices.Add(new FloatMenuOption(picked.Label,()=>{if(shelter)home.SetShelterArea(picked);else home.SetArea(picked);}));
                }
                Find.WindowStack.Add(new CourtyardMenu(choices));
            }
        }
    }
    public sealed class Dialog_AssistantWork : Window
    {
        readonly Pawn pawn;
        Vector2 scroll;
        public Dialog_AssistantWork(Pawn pawn) { this.pawn=pawn; doCloseX=true; forcePause=true; }
        public override Vector2 InitialSize => new Vector2(650,600);
        public override void DoWindowContents(Rect r)
        {
            if(!Roles.Managed(pawn)||pawn.Destroyed){Close();return;}
            var state=pawn.GetComp<CompAssistant>();
            CourtyardStyle.Round(r,CourtyardStyle.Paper);
            CourtyardStyle.Label(new Rect(20,12,r.width-40,36),"NC_WorkSettings".Translate()+" · "+AssistantNames.Personal(pawn),font:GameFont.Medium);
            if(state.UsesOptionalWork)
                CourtyardStyle.Label(new Rect(20,60,r.width-40,48),"NC_OptionalWorkHint".Translate(CompAssistant.OptionalWorkSkillThreshold),CourtyardStyle.Muted,wrap:true);
            else if (pawn.skills != null && state.ConfigurableWorkTypes().Any(w => w.relevantSkills?.Count > 0))
            {
                string filter=state.UnskilledThreshold==0?"NC_FilterOff".Translate().ToString():"NC_FilterBelow".Translate(state.UnskilledThreshold).ToString();
                if(CourtyardStyle.Button(new Rect(20,62,r.width-40,36),filter))
                {
                    var choices=new List<FloatMenuOption>();
                    foreach(int n in new[]{0,4,6,8,10}) { int value=n; choices.Add(new FloatMenuOption(n==0?"NC_FilterOff".Translate().ToString():"NC_FilterBelow".Translate(n).ToString(),()=>state.SetUnskilledThreshold(value))); }
                    Find.WindowStack.Add(new CourtyardMenu(choices));
                }
            }
            var works=state.ConfigurableWorkTypes();
            Rect list=new Rect(20,114,r.width-40,r.height-134),view=new Rect(0,0,list.width-18,works.Count*74);
            CourtyardStyle.BeginScroll(list,ref scroll,view);
            try
            {
                for(int i=0;i<works.Count;i++)
                {
                    WorkTypeDef work=works[i];float y=i*74;
                    CourtyardStyle.Label(new Rect(0,y,view.width-150,30),(work.labelShort ?? work.defName).CapitalizeFirst());
                    string skills=string.Join(" · ",work.relevantSkills?.Where(s=>pawn.skills!=null).Select(s=>{var v=pawn.skills.GetSkill(s);return s.LabelCap+" "+v.Level+(v.passion==Passion.Major?" ★★":v.passion==Passion.Minor?" ★":"");})??Enumerable.Empty<string>());
                    CourtyardStyle.Label(new Rect(0,y+32,view.width-150,28),skills,CourtyardStyle.Muted,GameFont.Tiny);
                    bool off=state.UsesOptionalWork?!state.WorkEnabled(work):state.disabledWork.Contains(work);
                    string status=!Roles.Helper(pawn)&&pawn.WorkTypeIsDisabled(work)?"NC_WorkIncapable":off?"NC_WorkDisabled":state.Unskilled(work)?"NC_WorkFiltered":"NC_WorkEnabled";
                    if(CourtyardStyle.Button(new Rect(view.width-142,y+10,142,36),status.Translate(),!off))state.SetWorkEnabled(work,off);
                }
            }
            finally{Widgets.EndScrollView();}
        }
    }
}
