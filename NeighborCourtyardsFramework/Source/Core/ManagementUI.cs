using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NeighborCourtyards
{
    [StaticConstructorOnStartup]
    public static partial class CourtyardStyle
    {
        public static readonly Color Paper = new Color(.98f,.94f,.83f), Card = new Color(1f,.98f,.91f), Ink = new Color(.25f,.14f,.08f), Muted = new Color(.46f,.34f,.23f);
        public static readonly Color Sage = new Color(.91f,.77f,.43f), Peach = new Color(1f,.85f,.62f), Lilac = new Color(.85f,.87f,.66f), Line = new Color(.73f,.60f,.43f);
        static readonly Texture2D atlas = ContentFinder<Texture2D>.Get("CourtyardUI/IconsBold");
        // Individual artwork bounds, rather than equal cells which include neighbouring strokes.
        static readonly Rect[] iconPixels={new Rect(35,32,318,340),new Rect(383,69,346,311),new Rect(748,72,306,299),new Rect(1114,74,292,281),new Rect(35,412,313,285),new Rect(394,415,287,289),new Rect(724,410,338,299),new Rect(1114,393,293,320),new Rect(28,724,335,315),new Rect(403,756,324,267),new Rect(783,747,266,284),new Rect(1126,756,268,264)};
        static readonly Texture2D disk = MakeDisk();
        static readonly Texture2D portraitMask = MakePortraitMask(false), portraitRim = MakePortraitMask(true);
        static GUIStyle inputStyle;
        static float dragOffset;
        public static string Input(Rect r,string value)
        {
            if(inputStyle==null){inputStyle=new GUIStyle(Text.CurTextFieldStyle);inputStyle.normal.background=inputStyle.focused.background=inputStyle.hover.background=inputStyle.active.background=null;inputStyle.normal.textColor=inputStyle.focused.textColor=inputStyle.hover.textColor=inputStyle.active.textColor=Ink;}
            Round(r,Paper,7);return GUI.TextField(r.ContractedBy(5,1),value,inputStyle);
        }
        public static void Portrait(Rect r,Pawn pawn,Color? surround=null)
        {
            float size=Mathf.Min(r.width,r.height);
            r=new Rect(r.center.x-size/2,r.center.y-size/2,size,size);
            Roles.Profile(pawn).Adapter.PortraitFraming(pawn,out Vector3 offset,out float zoom);
            var old=GUI.color;
            try
            {
                GUI.color=Roles.Adult(pawn)?Peach:Lilac;GUI.DrawTexture(r,disk);
                GUI.color=Color.white;
                // Keep the species' head framing; crop the shoulder strip from its 7:8 portrait.
                var portrait=PortraitsCache.Get(pawn,new Vector2(size*2,size*16/7),Rot4.South,offset,zoom,healthStateOverride:PawnHealthState.Mobile);
                GUI.DrawTextureWithTexCoords(r,portrait,new Rect(0,1f/8,1,7f/8));
                // RimWorld 1.7's GUI backend ignores DrawTexture's borderRadius argument.
                GUI.color=surround??Paper;GUI.DrawTexture(r,portraitMask);
                GUI.color=Ink;GUI.DrawTexture(r,portraitRim);
            }
            finally{GUI.color=old;}
        }
        public static void BeginScroll(Rect r,ref Vector2 position,Rect view)
        {
            float max=Mathf.Max(0,view.height-r.height);
            position.y=Mathf.Clamp(position.y,0,max);
            int id=GUIUtility.GetControlID(FocusType.Passive);
            if(max>0)
            {
                Rect hit=new Rect(r.xMax-16,r.y,16,r.height);float height=Mathf.Max(28,r.height*r.height/view.height),travel=r.height-height;
                Rect thumb=new Rect(r.xMax-7,r.y+travel*Mathf.Clamp01(position.y/max),5,height);Event e=Event.current;
                if(e.type==EventType.MouseDown&&e.button==0&&hit.Contains(e.mousePosition)){GUIUtility.hotControl=id;dragOffset=thumb.Contains(e.mousePosition)?e.mousePosition.y-thumb.y:height/2;position.y=Mathf.Clamp01((e.mousePosition.y-r.y-dragOffset)/travel)*max;e.Use();}
                if(GUIUtility.hotControl==id&&e.type==EventType.MouseDrag){position.y=Mathf.Clamp01((e.mousePosition.y-r.y-dragOffset)/travel)*max;e.Use();}
                if(GUIUtility.hotControl==id&&e.type==EventType.MouseUp){GUIUtility.hotControl=0;e.Use();}
                Round(new Rect(r.xMax-7,r.y,5,r.height),Paper,2);Round(thumb,new Color(.70f,.74f,.62f),2);
            }
            Widgets.BeginScrollView(r,ref position,view,false);
        }
        static Texture2D MakeDisk()
        {
            var t = new Texture2D(32,32,TextureFormat.RGBA32,false); t.wrapMode = TextureWrapMode.Clamp;
            for (int y=0;y<32;y++) for (int x=0;x<32;x++) t.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(16-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(16,16)))));
            t.Apply(); return t;
        }
        static Texture2D MakePortraitMask(bool rim)
        {
            const int size=256;const float radius=127,inner=119;
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float distance=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(size/2,size/2));
                float alpha=rim?Mathf.Clamp01(radius-distance)*Mathf.Clamp01(distance-inner):Mathf.Clamp01(distance-radius+1);
                t.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            t.Apply();return t;
        }
        public static void Round(Rect r, Color color, float radius=10)
        {
            if(r.width<=0||r.height<=0)return;
            if(r.height>12&&r.width>12){FillRound(r,Ink,radius);FillRound(r.ContractedBy(2),color,Mathf.Max(1,radius-2));}
            else FillRound(r,color,radius);
        }
        static void FillRound(Rect r, Color color, float radius)
        {
            radius = Mathf.Min(radius, r.height/2, r.width/2);
            Widgets.DrawBoxSolid(new Rect(r.x+radius,r.y,r.width-2*radius,r.height),color);
            Widgets.DrawBoxSolid(new Rect(r.x,r.y+radius,r.width,r.height-2*radius),color);
            var old=GUI.color; GUI.color=color;
            foreach(float x in new[]{r.x,r.xMax-2*radius}) foreach(float y in new[]{r.y,r.yMax-2*radius}) GUI.DrawTexture(new Rect(x,y,2*radius,2*radius),disk);
            GUI.color=old;
        }
        public static void Icon(Rect r,int index)
        {
            Rect pixels=iconPixels[index];float scale=Mathf.Min(r.width/pixels.width,r.height/pixels.height);
            Rect fitted=new Rect(r.center.x-pixels.width*scale/2,r.center.y-pixels.height*scale/2,pixels.width*scale,pixels.height*scale);
            var old=GUI.color;GUI.color=Color.white;
            GUI.DrawTextureWithTexCoords(fitted,atlas,IconUV(index));GUI.color=old;
        }
        public static Rect IconUV(int index){Rect p=iconPixels[index];return new Rect(p.x/1448f,1-p.yMax/1086f,p.width/1448f,p.height/1086f);}
        public static void Label(Rect r,string text,Color? color=null,GameFont font=GameFont.Small,bool wrap=false,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var old=GUI.color; var oldFont=Text.Font;var oldAnchor=Text.Anchor;bool oldWrap=Text.WordWrap;
            GUI.color=color??Ink;Text.Font=font;Text.Anchor=alignment;Text.WordWrap=wrap;
            Widgets.Label(r,wrap?text:(text??"").Truncate(r.width));
            Text.WordWrap=oldWrap;Text.Anchor=oldAnchor;Text.Font=oldFont;GUI.color=old;
        }
        public static bool Button(Rect r,string label,bool active=false,int icon=-1)
        {
            Round(r,active?Sage:Mouse.IsOver(r)?Peach:Card,8);
            float start=r.x+12;
            if(icon>=0){Icon(new Rect(r.x+6,r.y+(r.height-32)/2,32,32),icon);start=r.x+42;}
            Label(new Rect(start,r.y,r.xMax-start-10,r.height),label);
            return Widgets.ButtonInvisible(r);
        }
    }

    public sealed partial class MainTabWindow_Assistants : MainTabWindow
    {
        public int Page { get; private set; }
        public AssistantSpeciesDef Filter { get; private set; }
        public Pawn Selected { get; private set; }
        Map viewedMap;
        string search="";
        Vector2 listScroll, detailScroll, welcomeScroll, outputScroll;
        int outputWindow=1;
        public override Vector2 RequestedTabSize => new Vector2(Mathf.Min(1180,UI.screenWidth),Mathf.Min(780,UI.screenHeight-60));
        public MainTabWindow_Assistants() { forcePause=false;closeOnClickedOutside=false; }
        public static void Open(Map map,AssistantSpeciesDef species=null,int page=0)
        {
            var def=DefDatabase<MainButtonDef>.GetNamed("NC_Assistants");
            Find.MainTabsRoot.SetCurrentTab(def);
            ((MainTabWindow_Assistants)def.TabWindow).Navigate(map,species,page);
        }
        public void Navigate(Map map,AssistantSpeciesDef species=null,int page=0)
        { viewedMap=map;Filter=species;WelcomeAnimalFilter=null;Page=Mathf.Clamp(page,0,5);Selected=null;ResetCreatureSelection();listScroll=detailScroll=welcomeScroll=outputScroll=buildingScroll=Vector2.zero; }
        public void SetPage(int page) { Page=Mathf.Clamp(page,0,5); }
        public void SetFilter(AssistantSpeciesDef species) { Filter=species;WelcomeAnimalFilter=null;Selected=null;listScroll=detailScroll=outputScroll=welcomeScroll=buildingScroll=Vector2.zero; }
        public List<Pawn> Residents(Map map) => NeighborRegistry.Residents(map).Where(p=>Filter==null||Roles.Profile(p)==Filter).OrderBy(p=>Roles.Profile(p).label).ThenBy(p=>Roles.Role(p)).ThenBy(p=>p.thingIDNumber).ToList();
        public static string WorkState(Pawn p)
        {
            if(p.Downed)return "NC_NeedsCare".Translate().ToString();
            if(p.InMentalState)return p.MentalStateDef.LabelCap;
            if(!p.Awake())return "NC_Sleeping".Translate().ToString();
            if(p.DevelopmentalStage.Baby())return "NC_Growing".Translate().ToString();
            if(p.jobs?.curDriver!=null)return p.jobs.curDriver.GetReport();
            return "NC_Resting".Translate().ToString();
        }
        public static string ToolState(Pawn p) => !Roles.Adult(p)?"NC_NoToolNeeded".Translate().ToString():Roles.For(p).tool==null?"NC_NoToolNeeded".Translate().ToString():Roles.HasTool(p)?"NC_ToolCarried".Translate(Roles.For(p).tool.label).ToString():"NC_ToolWaiting".Translate(Roles.For(p).tool.label).ToString();
        public static bool Locate(Pawn pawn)
        {
            if(pawn==null||pawn.Destroyed||!CameraJumper.CanJump(pawn))return false;
            Find.MainTabsRoot.SetCurrentTab(null);
            CameraJumper.TryJumpAndSelect(pawn,CameraJumper.MovementMode.Cut);
            return true;
        }
        public override void DoWindowContents(Rect rect)
        {
            Color old=GUI.color;GameFont font=Text.Font;TextAnchor anchor=Text.Anchor;bool wordWrap=Text.WordWrap;
            try { Draw(rect); } finally { GUI.color=old;Text.Font=font;Text.Anchor=anchor;Text.WordWrap=wordWrap; }
        }
        void Draw(Rect rect)
        {
            CourtyardStyle.Round(rect,CourtyardStyle.Paper,14);
            Map map=viewedMap;
            if(map==null||!Find.Maps.Contains(map))viewedMap=map=Find.CurrentMap;
            CourtyardStyle.Icon(new Rect(12,5,72,72),0);
            CourtyardStyle.Label(new Rect(92,22,300,36),"NC_Title".Translate().ToString(),font:GameFont.Medium);
            if(CourtyardStyle.Button(new Rect(rect.width-230,22,170,34),map?.Parent?.LabelCap??"NC_NoMap".Translate().ToString()))
                Find.WindowStack.Add(new CourtyardMenu(Find.Maps.Select(m=>new FloatMenuOption(m.Parent.LabelCap,()=>Navigate(m,Filter,Page))).ToList()));
            if(CourtyardStyle.Button(new Rect(rect.width-50,22,36,34),"×"))Close();
            string[] tabs={"NC_People".Translate().ToString(),"NC_Output".Translate().ToString(),"NC_Welcome".Translate().ToString(),"NC_BuildTab".Translate().ToString(),"NC_BookTab".Translate().ToString(),"NC_CreatureTab".Translate().ToString()};int[] icons={1,2,3,0,0,1};
            float tabWidth=Mathf.Min(114,(rect.width-36-5*8)/6);
            for(int i=0;i<tabs.Length;i++)if(CourtyardStyle.Button(new Rect(18+i*(tabWidth+8),85,tabWidth,40),tabs[i],Page==i,tabWidth>=100?icons[i]:-1))SetPage(i);
            if(Page==5){DrawCreatures(new Rect(18,181,rect.width-36,rect.height-199),map);return;}
            if(map==null && Page!=4){Empty(new Rect(18,150,rect.width-36,rect.height-180),"NC_NoColony".Translate().ToString(),0);return;}
            if(Page==2){DrawWelcomeFilter(new Rect(18,137,196,32));DrawWelcome(new Rect(18,181,rect.width-36,rect.height-199),map);return;}
            var people=map==null?new List<Pawn>():Residents(map);var ledger=map?.GetComponent<MapComponent_AssistantLedger>();
            CourtyardStyle.Label(new Rect(192,137,rect.width-210,32),"NC_ResidentsSummary".Translate(people.Count, people.Count(Roles.Adult), (ledger?.Total(Filter)??0)).ToString(),CourtyardStyle.Muted);
            if(CourtyardStyle.Button(new Rect(18,137,156,32),Filter?.label??"NC_AllSpecies".Translate().ToString(),Filter!=null))
            {
                var opts=new List<FloatMenuOption>{new FloatMenuOption("NC_AllSpecies".Translate().ToString(),()=>SetFilter(null))};
                opts.AddRange(DefDatabase<AssistantSpeciesDef>.AllDefsListForReading.Select(s=>new FloatMenuOption(s.label,()=>SetFilter(s))));Find.WindowStack.Add(new CourtyardMenu(opts));
            }
            Rect body=new Rect(18,181,rect.width-36,rect.height-199);
            if(Page==0)DrawPeople(body,map,people,ledger);
            else if(Page==1)DrawOutput(body,ledger);
            else if(Page==3)DrawBuildings(body,map);
            else DrawEncyclopedia(body);
        }
        static void Empty(Rect r,string title,int icon)
        {
            CourtyardStyle.Round(r,CourtyardStyle.Card);
            float top=r.center.y-68;
            CourtyardStyle.Icon(new Rect(r.center.x-48,top,96,96),icon);
            CourtyardStyle.Label(new Rect(r.x+28,top+108,r.width-56,28),title,font:GameFont.Medium,alignment:TextAnchor.MiddleCenter);
        }
        void DrawPeople(Rect r,Map map,List<Pawn> people,MapComponent_AssistantLedger ledger)
        {
            if(people.Count==0){Empty(r,"NC_NoPeople".Translate().ToString(),1);return;}
            float side=272, gap=12;Rect left=new Rect(r.x,r.y,r.width-side-gap,r.height),right=new Rect(r.xMax-side,r.y,side,r.height);
            CourtyardStyle.Round(left,CourtyardStyle.Card);CourtyardStyle.Round(right,CourtyardStyle.Card);
            CourtyardStyle.Label(new Rect(left.x+14,left.y+8,60,30),"NC_Search".Translate().ToString(),CourtyardStyle.Muted);
            search=CourtyardStyle.Input(new Rect(left.x+68,left.y+10,left.width-84,28),search);
            var filtered=people.Where(p=>string.IsNullOrWhiteSpace(search)||(AssistantNames.Personal(p)+Roles.Profile(p).label+Roles.For(p).label).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0).ToList();
            if(Selected==null||!people.Contains(Selected))Selected=filtered.FirstOrDefault();
            Rect list=new Rect(left.x+8,left.y+48,left.width-16,left.height-56),view=new Rect(0,0,list.width-18,filtered.Count*88);
            CourtyardStyle.BeginScroll(list,ref listScroll,view);
            try
            {
                for(int i=0;i<filtered.Count;i++)
                {
                    Pawn p=filtered[i];Rect row=new Rect(0,i*88,view.width,80);
                    Color fill=p==Selected?new Color(1f,.87f,.62f):CourtyardStyle.Paper;
                    CourtyardStyle.Round(row,fill);
                    CourtyardStyle.Portrait(new Rect(10,row.y+11,58,58),p,fill);
                    float column=(row.width-94)/3,roleX=80+column,stateX=80+column*2;
                    CourtyardStyle.Label(new Rect(80,row.y+10,column-12,27),AssistantNames.Personal(p));
                    CourtyardStyle.Label(new Rect(80,row.y+40,column-12,25),Identity(p),CourtyardStyle.Muted,GameFont.Tiny);
                    CourtyardStyle.Label(new Rect(roleX,row.y+10,column-12,27),AssistantNames.Title(p));
                    CourtyardStyle.Label(new Rect(roleX,row.y+40,column-12,25),Roles.Adult(p)?(Roles.For(p).tool==null?"NC_NoToolNeeded".Translate().ToString():Roles.HasTool(p)?"NC_ToolReady".Translate().ToString():"NC_ToolToCollect".Translate().ToString()):"NC_Growing".Translate().ToString(),CourtyardStyle.Muted,GameFont.Tiny);
                    CourtyardStyle.Label(new Rect(stateX,row.y+10,column,27),WorkState(p));
                    CourtyardStyle.Label(new Rect(stateX,row.y+40,column,25),"NC_MadeToday".Translate(ledger.Total(pawn:p)).ToString(),CourtyardStyle.Muted,GameFont.Tiny);
                    TooltipHandler.TipRegion(row,AssistantNames.Display(p)+"\n"+ToolState(p)+"\n"+WorkState(p));
                    if(Widgets.ButtonInvisible(row)){Selected=p;detailScroll=Vector2.zero;}
                }
                if(filtered.Count==0)CourtyardStyle.Label(new Rect(14,12,view.width-28,32),"NC_NoPeopleFound".Translate().ToString(),CourtyardStyle.Muted);
            }
            finally{Widgets.EndScrollView();}
            DrawPerson(right,Selected,ledger);
        }
        static string Identity(Pawn p)=>Roles.Profile(p).label+" · "+(Roles.Adult(p)?"NC_Adult".Translate().ToString():p.DevelopmentalStage.Baby()?"NC_Baby".Translate().ToString():"NC_Child".Translate().ToString());
        void DrawPerson(Rect r,Pawn p,MapComponent_AssistantLedger ledger)
        {
            if(p==null)return;
            Rect viewport=new Rect(r.x+14,r.y+14,r.width-28,r.height-28);
            float width=viewport.width-16;
            var rows=ledger.Query(pawn:p).Where(a=>a.Count(1,ledger.Day)>0).GroupBy(a=>a.product).ToList();
            var view=new Rect(0,0,width,Content(false));
            CourtyardStyle.BeginScroll(viewport,ref detailScroll,view);
            try{Content(true);}
            finally{Widgets.EndScrollView();}
            float Content(bool draw)
            {
                if(draw)
                {
                    float center=viewport.width/2;
                    CourtyardStyle.Portrait(new Rect(center-40,0,80,80),p,CourtyardStyle.Card);
                    CourtyardStyle.Label(new Rect(16,88,width-16,30),AssistantNames.Personal(p),font:GameFont.Medium,alignment:TextAnchor.MiddleCenter);
                    CourtyardStyle.Label(new Rect(16,120,width-16,22),Identity(p),CourtyardStyle.Muted,GameFont.Tiny,alignment:TextAnchor.MiddleCenter);
                    Rect locate=new Rect(center-39,148,78,28);
                    if(!p.Destroyed&&CameraJumper.CanJump(p))
                    {
                        CourtyardStyle.Round(locate,Mouse.IsOver(locate)?CourtyardStyle.Peach:CourtyardStyle.Card,8);
                        CourtyardStyle.Label(locate,"NC_Locate".Translate().ToString(),alignment:TextAnchor.MiddleCenter);
                        if(Widgets.ButtonInvisible(locate))Locate(p);
                    }
                    else CourtyardStyle.Label(locate,"NC_CannotLocate".Translate().ToString(),CourtyardStyle.Muted,GameFont.Tiny,alignment:TextAnchor.MiddleCenter);
                }
                float y=184;
                Divider();
                Detail("NC_Role".Translate().ToString(),AssistantNames.Title(p));
                Detail("NC_Tool".Translate().ToString(),ToolState(p));
                Detail("NC_Task".Translate().ToString(),WorkState(p));
                if(Roles.Adult(p))
                {
                    if(draw && CourtyardStyle.Button(new Rect(0,y,width,30),"NC_WorkSettings".Translate().ToString()))Find.WindowStack.Add(new Dialog_AssistantWork(p));
                    y+=40;
                }
                Divider();
                if(draw)
                {
                    CourtyardStyle.Label(new Rect(0,y,width-62,26),"NC_TodayOutput".Translate().ToString());
                    CourtyardStyle.Label(new Rect(width-62,y,62,26),"NC_ItemCount".Translate(ledger.Total(pawn:p)).ToString(),alignment:TextAnchor.MiddleRight);
                }
                y+=32;
                if(rows.Count==0)Paragraph("NC_NoOutput".Translate().ToString(),CourtyardStyle.Muted,GameFont.Tiny);
                foreach(var group in rows)Paragraph(group.Key.label+" × "+group.Sum(a=>a.Count(1,ledger.Day)),CourtyardStyle.Ink,GameFont.Small);
                y+=6;Paragraph("NC_MadeTotal".Translate(ledger.Total(pawn:p,window:0)).ToString(),CourtyardStyle.Muted,GameFont.Tiny);
                return y+8;
                void Detail(string title,string value,GameFont font=GameFont.Small)
                {
                    float height=Height(value,width-46,font);
                    if(draw)
                    {
                        CourtyardStyle.Label(new Rect(0,y,40,height),title,CourtyardStyle.Muted,GameFont.Tiny,alignment:TextAnchor.UpperLeft);
                        CourtyardStyle.Label(new Rect(46,y,width-46,height),value,font:font,wrap:true,alignment:TextAnchor.UpperLeft);
                    }
                    y+=height+12;
                }
                void Divider(){y+=2;if(draw)Widgets.DrawBoxSolid(new Rect(0,y,width,1),CourtyardStyle.Line);y+=16;}
                void Paragraph(string text,Color color,GameFont font)
                {
                    float height=Height(text,width,font);
                    if(draw)CourtyardStyle.Label(new Rect(0,y,width,height),text,color,font,true,TextAnchor.UpperLeft);
                    y+=height+6;
                }
            }
            float Height(string text,float available,GameFont font)
            {
                GameFont old=Text.Font;bool wrap=Text.WordWrap;Text.Font=font;Text.WordWrap=true;
                try{return Mathf.Max(24,Text.CalcHeight(text,available));}
                finally{Text.Font=old;Text.WordWrap=wrap;}
            }
        }
        void DrawOutput(Rect r,MapComponent_AssistantLedger ledger)
        {
            string[] titles={"NC_Today".Translate().ToString(),"NC_Week".Translate().ToString(),"NC_Total".Translate().ToString()};int[] windows={1,7,0};
            for(int i=0;i<3;i++)if(CourtyardStyle.Button(new Rect(r.x+i*110,r.y,100,32),titles[i],outputWindow==windows[i]))outputWindow=windows[i];
            var groups=ledger.Query(Filter).Where(a=>a.Count(outputWindow,ledger.Day)>0).GroupBy(a=>a.product).OrderByDescending(g=>g.Sum(a=>a.Count(outputWindow,ledger.Day))).ThenBy(g=>g.Key.label).ToList();
            Rect list=new Rect(r.x,r.y+44,r.width,r.height-44);
            if(groups.Count==0){Empty(list,"NC_NoOutput".Translate().ToString(),2);return;}
            var view=new Rect(0,0,list.width-18,groups.Count*86);
            CourtyardStyle.BeginScroll(list,ref outputScroll,view);
            try
            {
                for(int i=0;i<groups.Count;i++)
                {
                    var g=groups[i];Rect row=new Rect(0,i*86,view.width,76);CourtyardStyle.Round(row,CourtyardStyle.Card);
                    Widgets.DefIcon(new Rect(14,row.y+14,48,48),g.Key);
                    CourtyardStyle.Label(new Rect(78,row.y+8,row.width-240,28),g.Key.LabelCap);
                    string makers=string.Join("NC_ListSeparator".Translate().ToString(),g.Select(a=>a.content?.label??(a.species?.label+" / "+a.role?.label)).Distinct());
                    CourtyardStyle.Label(new Rect(78,row.y+38,row.width-240,25),makers,CourtyardStyle.Muted,GameFont.Tiny);
                    CourtyardStyle.Round(new Rect(row.width-126,row.y+18,108,40),CourtyardStyle.Sage);
                    CourtyardStyle.Label(new Rect(row.width-112,row.y+18,90,40),"× "+g.Sum(a=>a.Count(outputWindow,ledger.Day)),font:GameFont.Medium);
                    TooltipHandler.TipRegion(row,string.Join("\n",g.Select(a=>a.pawnName+"："+a.Count(outputWindow,ledger.Day))));
                }
            }
            finally{Widgets.EndScrollView();}
        }
        public static CompCourtyard PrimaryHome(Map map,AssistantSpeciesDef species) => map.listerThings.AllThings.Where(t=>t.Faction==Faction.OfPlayer).Select(t=>t.TryGetComp<CompCourtyard>()).Where(c=>c?.Species==species).OrderBy(c=>c.parent.thingIDNumber).FirstOrDefault();
        void DrawWelcome(Rect r,Map map)
        {
            var species=DefDatabase<AssistantSpeciesDef>.AllDefsListForReading.Where(s=>WelcomeAnimalFilter==null&&(Filter==null||s==Filter)).ToList();
            var animals=AnimalWelcomeDefs.Where(d=>Filter==null&&(WelcomeAnimalFilter==null||d.GetCompProperties<CompProperties_AnimalWelcome>().animal==WelcomeAnimalFilter)).ToList();
            int count=species.Count+animals.Count;
            if(count==0){Empty(r,"NC_NoSpecies".Translate().ToString(),3);return;}
            int cols=r.width>=840?2:1;float width=(r.width-18-(cols-1)*12)/cols;const float height=490;
            var view=new Rect(0,0,r.width-18,Mathf.CeilToInt(count/(float)cols)*(height+12));
            CourtyardStyle.BeginScroll(r,ref welcomeScroll,view);
            try{for(int i=0;i<count;i++){var card=new Rect((i%cols)*(width+12),(i/cols)*(height+12),width,height);if(i<animals.Count)AnimalWelcomeCard(card,map,animals[i]);else WelcomeCard(card,map,species[i-animals.Count]);}}
            finally{Widgets.EndScrollView();}
        }
        void WelcomeCard(Rect r,Map map,AssistantSpeciesDef species)
        {
            CourtyardStyle.Round(r,CourtyardStyle.Card);
            CourtyardStyle.Icon(new Rect(r.x+12,r.y+8,56,56),3);
            CourtyardStyle.Label(new Rect(r.x+80,r.y+13,r.width-200,32),species.label,font:GameFont.Medium);
            var home=PrimaryHome(map,species);
            if(home==null){CourtyardStyle.Label(new Rect(r.x+24,r.y+90,r.width-48,32),"NC_NoWelcomePoint".Translate().ToString());return;}
            string reason=home.BlockReason();
            CourtyardStyle.Label(new Rect(r.xMax-112,r.y+15,94,32),home.shelter?"NC_Sheltering".Translate().ToString():reason!=null?"NC_WelcomeOnHold".Translate().ToString():"NC_Welcoming".Translate().ToString(),CourtyardStyle.Muted);
            CourtyardStyle.Label(new Rect(r.x+20,r.y+76,r.width-40,42),reason??"NC_AwaitingArrival".Translate().ToString(),reason==null?new Color(.30f,.43f,.23f):CourtyardStyle.Muted,wrap:true);
            Row(137,"NC_Residents".Translate().ToString(),home.Residents.Count()+" / "+home.populationLimit);
            Row(179,"NC_Arrival".Translate().ToString(),home.ArrivalGroupLabel);
            Row(221,"NC_ArrivalWindow".Translate().ToString(),"NC_WithinDays".Translate((home.ArrivalTicks/60000f).ToString("0.##")).ToString());
            Row(263,"NC_Preparation".Translate().ToString(),"NC_BedAndMeal".Translate().ToString());
            Row(305,"NC_Role".Translate().ToString(),"NC_FillRoles".Translate().ToString());
            Row(347,"NC_Pets".Translate().ToString(),species.companionPets.Count>0?"NC_PetsPossible".Translate().ToString():"NC_None".Translate().ToString());
            Row(389,"NC_Waited".Translate().ToString(),"NC_Days".Translate((home.progress/60000f).ToString("0.##")).ToString());
            float buttonWidth=(r.width-52)/3;
            if(CourtyardStyle.Button(new Rect(r.x+18,r.y+444,buttonWidth,30),(home.welcome?"NC_PauseWelcome":"NC_ResumeWelcome").Translate().ToString(),!home.welcome))home.welcome=!home.welcome;
            if(CourtyardStyle.Button(new Rect(r.x+26+buttonWidth,r.y+444,buttonWidth,30),home.shelter?"NC_EndShelter".Translate().ToString():"NC_Shelter".Translate().ToString(),home.shelter))home.SetShelter(!home.shelter);
            if(CourtyardStyle.Button(new Rect(r.x+34+buttonWidth*2,r.y+444,buttonWidth,30),"NC_AreaSettings".Translate().ToString()))Find.WindowStack.Add(new Dialog_AssistantAreas(home));
            void Row(float y,string title,string value){CourtyardStyle.Label(new Rect(r.x+20,r.y+y,112,30),title,CourtyardStyle.Muted);CourtyardStyle.Label(new Rect(r.x+150,r.y+y,r.width-170,30),value);}
        }
    }
}
