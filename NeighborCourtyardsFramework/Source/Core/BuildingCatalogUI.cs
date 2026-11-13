using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public sealed partial class MainTabWindow_Assistants
    {
        Vector2 buildingScroll;
        Vector2 buildingCategoryScroll;
        bool revealBuildingCategory;
        int buildingUse=-1;
        string buildingSearch="";
        public void FilterBuildings(int use,string query=""){buildingUse=use<0?-1:(int)BuildingCatalog.Category((CourtyardBuildingUse)use);buildingSearch=query??"";buildingScroll=Vector2.zero;revealBuildingCategory=true;}
        void DrawBuildings(Rect r,Map map)
        {
            if(map!=Find.CurrentMap)
            {
                Empty(r,"NC_SwitchMapHint".Translate().ToString(),0);
                if(CourtyardStyle.Button(new Rect(r.center.x-80,r.yMax-60,160,36),"NC_SwitchMap".Translate().ToString()))Current.Game.CurrentMap=map;
                return;
            }
            var available=BuildingCatalog.Query(Filter);
            Rect nav=new Rect(r.x,r.y,192,r.height),content=new Rect(r.x+206,r.y,r.width-206,r.height);
            CourtyardStyle.Round(nav,CourtyardStyle.Card);
            var categories=BuildingCatalog.Uses.Where(u=>available.Any(e=>e.use==u)).Select(u=>(int)u).ToList();
            if(buildingUse>=0&&!categories.Contains(buildingUse))FilterBuildings(-1,buildingSearch);
            categories.Insert(0,-1);
            Rect navList=nav.ContractedBy(8);
            var navView=new Rect(0,0,navList.width-18,categories.Count*43);
            if(revealBuildingCategory)
            {
                float top=categories.IndexOf(buildingUse)*43;
                if(top<buildingCategoryScroll.y)buildingCategoryScroll.y=top;
                else if(top+35>buildingCategoryScroll.y+navList.height)buildingCategoryScroll.y=top+35-navList.height;
                revealBuildingCategory=false;
            }
            CourtyardStyle.BeginScroll(navList,ref buildingCategoryScroll,navView);
            try
            {
             for(int row=0;row<categories.Count;row++)
             {
                int i=categories[row];
                int count=i<0?available.Count:available.Count(e=>(int)e.use==i);
                Rect button=new Rect(0,row*43,navView.width,35);
                if(CourtyardStyle.Button(button,(i<0?"NC_All".Translate().ToString():BuildingCatalog.Label((CourtyardBuildingUse)i))+" "+count,buildingUse==i,i<0?0:BuildingCatalog.Icons[i]))FilterBuildings(i,buildingSearch);
             }
            }
            finally{Widgets.EndScrollView();}
            CourtyardStyle.Label(new Rect(content.x,content.y,48,34),"NC_Search".Translate().ToString());
            string query=CourtyardStyle.Input(new Rect(content.x+48,content.y,content.width-48,34),buildingSearch);
            if(query!=buildingSearch){buildingSearch=query;buildingScroll=Vector2.zero;}
            var rows=BuildingCatalog.Query(Filter,buildingUse,buildingSearch);
            Rect list=new Rect(content.x,content.y+46,content.width,content.height-46);
            if(rows.Count==0){Empty(list,"NC_NoBuildings".Translate().ToString(),0);return;}
            int columns=Math.Max(2,(int)((list.width-18)/218));float gap=12,width=(list.width-18-gap*(columns-1))/columns;
            const float height=187;
            var view=new Rect(0,0,list.width-18,Mathf.CeilToInt(rows.Count/(float)columns)*(height+gap));
            CourtyardStyle.BeginScroll(list,ref buildingScroll,view);
            try
            {
                for(int i=0;i<rows.Count;i++)
                {
                    var entry=rows[i];var def=entry.def;
                    Rect card=new Rect((i%columns)*(width+gap),(i/columns)*(height+gap),width,height);
                    CourtyardStyle.Round(card,CourtyardStyle.Card);
                    CourtyardStyle.Label(new Rect(card.x+12,card.y+6,width-24,28),def.LabelCap);
                    Rect picture=new Rect(card.x+12,card.y+40,72,72);
                    CourtyardStyle.Round(picture,CourtyardStyle.Paper,16);
                    Widgets.DefIcon(picture.ContractedBy(6),def,entry.designator.StuffDef);
                    CourtyardStyle.Label(new Rect(card.x+94,card.y+42,width-106,25),entry.ContentLabel,CourtyardStyle.Muted,GameFont.Tiny);
                    CourtyardStyle.Label(new Rect(card.x+94,card.y+69,width-106,44),BuildingCatalog.Label(entry.use)+(def is ThingDef t?"\n"+t.size.x+" × "+t.size.z:"NC_FloorLine".Translate().ToString()),CourtyardStyle.Muted,GameFont.Tiny,true);
                    string cost=string.Join("  ",def.CostListAdjusted(entry.designator.StuffDef).Select(c=>c.thingDef.label+"×"+c.count));
                    if(def.CostStuffCount>0)cost="NC_ChooseMaterial".Translate(cost).ToString();
                    CourtyardStyle.Label(new Rect(card.x+12,card.y+117,width-24,24),string.IsNullOrEmpty(cost)?"NC_ViewRequirements".Translate().ToString():cost,CourtyardStyle.Muted,GameFont.Tiny);
                    string blocked=BuildingCatalog.Unavailable(entry);
                    Rect build=new Rect(card.x+12,card.y+148,width-24,29);
                    if(blocked==null){if(CourtyardStyle.Button(build,"NC_Build".Translate().ToString()))BuildingCatalog.BeginPlacement(entry,map);}
                    else{CourtyardStyle.Round(build,CourtyardStyle.Paper,8);CourtyardStyle.Label(build.ContractedBy(8,0),"NC_Locked".Translate().ToString(),CourtyardStyle.Muted);}
                    TooltipHandler.TipRegion(card,def.LabelCap+"\n"+def.description+"\n\n"+cost+(blocked==null?"":"\n"+blocked));
                }
            }
            finally{Widgets.EndScrollView();}
        }
    }
}
