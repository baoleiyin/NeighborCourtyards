using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public sealed partial class MainTabWindow_Assistants
    {
        Def creatureSource;
        ThingDef creatureRace;
        string creatureSearch="";
        CourtyardCreatureScope creatureScope;
        Pawn creaturePawn;
        Vector2 creatureListScroll,creatureDetailScroll;

        public void BrowseCreatures(Def source=null,string query="")
        {
            SetPage(5);creatureSource=source;creatureRace=null;creatureScope=CourtyardCreatureScope.All;
            creatureSearch=query??"";ResetCreatureSelection();
        }
        void ResetCreatureSelection()
        {
            creaturePawn=null;creatureListScroll=creatureDetailScroll=Vector2.zero;
        }
        void DrawCreatures(Rect r,Map map)
        {
            float filterWidth=Mathf.Min(156,r.width*.25f),scopeWidth=Mathf.Min(140,r.width*.22f);
            float x=r.x;
            if(CourtyardStyle.Button(new Rect(x,137,filterWidth,32),creatureSource?.label??"NC_CreatureAllSources".Translate(),creatureSource!=null))
            {
                var options=CreatureCatalog.Entries.SelectMany(e=>e.sources).Distinct().OrderBy(s=>s.label)
                    .Select(s=>new FloatMenuOption(s.label,()=>SelectSource(s))).ToList();
                options.Insert(0,new FloatMenuOption("NC_CreatureAllSources".Translate(),()=>SelectSource(null)));
                Find.WindowStack.Add(new CourtyardMenu(options));
            }
            x+=filterWidth+10;
            if(CourtyardStyle.Button(new Rect(x,137,filterWidth,32),creatureRace?.LabelCap.ToString()??"NC_CreatureAllKinds".Translate().ToString(),creatureRace!=null))
            {
                var options=CreatureCatalog.Query(creatureSource).Select(e=>new FloatMenuOption(e.Label,()=>{creatureRace=e.Race;ResetCreatureSelection();})).ToList();
                options.Insert(0,new FloatMenuOption("NC_CreatureAllKinds".Translate(),()=>{creatureRace=null;ResetCreatureSelection();}));
                Find.WindowStack.Add(new CourtyardMenu(options));
            }
            x+=filterWidth+10;
            if(CourtyardStyle.Button(new Rect(x,137,scopeWidth,32),CreatureCatalog.ScopeLabel(creatureScope),creatureScope!=CourtyardCreatureScope.All))
                Find.WindowStack.Add(new CourtyardMenu(Enum.GetValues(typeof(CourtyardCreatureScope)).Cast<CourtyardCreatureScope>()
                    .Select(s=>new FloatMenuOption(CreatureCatalog.ScopeLabel(s),()=>{creatureScope=s;ResetCreatureSelection();})).ToList()));
            var rows=CreatureCatalog.Individuals(map,creatureSource,creatureRace,creatureScope,creatureSearch);
            x+=scopeWidth+12;
            CourtyardStyle.Label(new Rect(x,137,r.xMax-x,32),"NC_CreatureMapCount".Translate(rows.Count),CourtyardStyle.Muted);
            if(map==null){Empty(r,"NC_NoMap".Translate(),10);return;}
            if(CreatureCatalog.Entries.Count==0){Empty(r,"NC_CreatureNone".Translate(),10);return;}

            const float side=272,gap=12;
            Rect left=new Rect(r.x,r.y,r.width-side-gap,r.height),right=new Rect(r.xMax-side,r.y,side,r.height);
            CourtyardStyle.Round(left,CourtyardStyle.Card);CourtyardStyle.Round(right,CourtyardStyle.Card);
            CourtyardStyle.Label(new Rect(left.x+14,left.y+8,60,30),"NC_Search".Translate(),CourtyardStyle.Muted);
            string query=CourtyardStyle.Input(new Rect(left.x+68,left.y+10,left.width-84,28),creatureSearch);
            if(query!=creatureSearch)
            {
                creatureSearch=query;ResetCreatureSelection();
                rows=CreatureCatalog.Individuals(map,creatureSource,creatureRace,creatureScope,creatureSearch);
            }
            if(creaturePawn==null||!rows.Contains(creaturePawn)){creaturePawn=rows.FirstOrDefault();creatureDetailScroll=Vector2.zero;}
            Rect viewport=new Rect(left.x+8,left.y+48,left.width-16,left.height-56),view=new Rect(0,0,viewport.width-18,rows.Count*88);
            creatureListScroll.y=Mathf.Clamp(creatureListScroll.y,0,Mathf.Max(0,view.height-viewport.height));
            CourtyardStyle.BeginScroll(viewport,ref creatureListScroll,view);
            try
            {
                for(int i=0;i<rows.Count;i++)
                {
                    Pawn p=rows[i];var entry=CreatureCatalog.Entries.First(e=>e.Race==p.def);
                    Rect row=new Rect(0,i*88,view.width,80);
                    CourtyardStyle.Round(row,p==creaturePawn?new Color(1f,.87f,.62f):CourtyardStyle.Paper);
                    Widgets.ThingIcon(new Rect(10,row.y+11,58,58),p);
                    float column=(row.width-94)/3,kindX=80+column,stateX=80+column*2;
                    CourtyardStyle.Label(new Rect(80,row.y+10,column-12,27),p.LabelShortCap);
                    CourtyardStyle.Label(new Rect(80,row.y+40,column-12,25),CreatureIdentity(p),CourtyardStyle.Muted,GameFont.Tiny);
                    CourtyardStyle.Label(new Rect(kindX,row.y+10,column-12,27),entry.Label);
                    CourtyardStyle.Label(new Rect(kindX,row.y+40,column-12,25),CreatureCatalog.ScopeLabel(CreatureCatalog.Scope(p)),CourtyardStyle.Muted,GameFont.Tiny);
                    CourtyardStyle.Label(new Rect(stateX,row.y+10,column,27),WorkState(p));
                    CourtyardStyle.Label(new Rect(stateX,row.y+40,column,25),entry.SourceLabel,CourtyardStyle.Muted,GameFont.Tiny);
                    TooltipHandler.TipRegion(row,p.LabelShortCap+"\n"+CreatureIdentity(p)+"\n"+entry.SourceLabel+"\n"+WorkState(p));
                    if(Widgets.ButtonInvisible(row)){creaturePawn=p;creatureDetailScroll=Vector2.zero;}
                }
                if(rows.Count==0)CourtyardStyle.Label(new Rect(14,12,view.width-28,32),"NC_CreatureNoResidents".Translate(),CourtyardStyle.Muted);
            }
            finally{Widgets.EndScrollView();}
            DrawCreatureDetail(right,creaturePawn);

            void SelectSource(Def source)
            {
                creatureSource=source;
                if(creatureRace!=null&&!CreatureCatalog.Query(source).Any(e=>e.Race==creatureRace))creatureRace=null;
                ResetCreatureSelection();
            }
        }
        void DrawCreatureDetail(Rect r,Pawn pawn)
        {
            if(pawn==null)return;
            var entry=CreatureCatalog.Entries.First(e=>e.Race==pawn.def);
            Rect viewport=new Rect(r.x+14,r.y+14,r.width-28,r.height-28);
            float width=viewport.width-16;
            var view=new Rect(0,0,width,Content(false));
            CourtyardStyle.BeginScroll(viewport,ref creatureDetailScroll,view);
            try{Content(true);}finally{Widgets.EndScrollView();}
            float Content(bool draw)
            {
                if(draw)
                {
                    Widgets.ThingIcon(new Rect((width-80)/2,0,80,80),pawn);
                    CourtyardStyle.Label(new Rect(0,88,width,30),pawn.LabelShortCap,font:GameFont.Medium,alignment:TextAnchor.MiddleCenter);
                    CourtyardStyle.Label(new Rect(0,120,width,22),CreatureIdentity(pawn),CourtyardStyle.Muted,GameFont.Tiny,alignment:TextAnchor.MiddleCenter);
                    if(CourtyardStyle.Button(new Rect(20,150,88,30),"NC_Locate".Translate()))Locate(pawn);
                    if(CourtyardStyle.Button(new Rect(120,150,88,30),"NC_CreatureInfo".Translate()))Find.WindowStack.Add(new Dialog_InfoCard(pawn));
                }
                float y=198;
                Line(entry.Label);
                Line(entry.SourceLabel,CourtyardStyle.Muted,GameFont.Tiny);
                Line(CreatureCatalog.ScopeLabel(CreatureCatalog.Scope(pawn)),CourtyardStyle.Muted);
                Line(WorkState(pawn));
                Line("NC_CreatureHealth".Translate(pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent()));
                if(pawn.needs?.food!=null)Line("NC_CreatureFood".Translate(pawn.needs.food.CurLevelPercentage.ToStringPercent()));
                if(pawn.needs?.rest!=null)Line("NC_CreatureRest".Translate(pawn.needs.rest.CurLevelPercentage.ToStringPercent()));
                Line("NC_CreatureBed".Translate(pawn.ownership?.OwnedBed?.LabelCap.ToString()??"NC_CreatureNoBed".Translate().ToString()));
                return y;
                void Line(string text,Color? color=null,GameFont font=GameFont.Small)
                {
                    GameFont old=Text.Font;bool wrap=Text.WordWrap;Text.Font=font;Text.WordWrap=true;
                    float height;
                    try{height=Mathf.Max(24,Text.CalcHeight(text,width));}finally{Text.Font=old;Text.WordWrap=wrap;}
                    if(draw)CourtyardStyle.Label(new Rect(0,y,width,height),text,color,font,true,TextAnchor.UpperLeft);
                    y+=height+10;
                }
            }
        }
        static string CreatureIdentity(Pawn pawn) => "NC_CreatureIdentity".Translate(
            (pawn.gender==Gender.Female?"NC_CreatureFemale":pawn.gender==Gender.Male?"NC_CreatureMale":"NC_CreatureNoGender").Translate(),
            pawn.ageTracker.AgeBiologicalYearsFloat.ToString("0.##"),pawn.ageTracker.CurLifeStage.label);
    }
}
