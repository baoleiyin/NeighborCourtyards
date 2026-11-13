using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace NeighborCourtyards
{
    public sealed partial class MainTabWindow_Assistants
    {
        string bookSearch="";
        bool bookMaterials;
        Vector2 bookListScroll,bookDetailScroll;
        CourtyardBookEntry bookSelected;
        public void BrowseEncyclopedia(bool materials,string query="")
        {SetPage(4);bookMaterials=materials;bookSearch=query??"";bookSelected=null;bookListScroll=bookDetailScroll=Vector2.zero;}
        void DrawEncyclopedia(Rect r)
        {
            float tabWidth=Mathf.Min(198,(r.width-24)/3);
            for(int i=0;i<2;i++)if(CourtyardStyle.Button(new Rect(r.x+i*(tabWidth+8),r.y,tabWidth,34),(i==0?"NC_BookPeoples":"NC_BookMaterials").Translate(),bookMaterials==(i==1)))BrowseEncyclopedia(i==1,bookSearch);
            float sx=r.x+2*(tabWidth+8);
            CourtyardStyle.Label(new Rect(sx,r.y,54,34),"NC_Search".Translate());
            string query=CourtyardStyle.Input(new Rect(sx+54,r.y,r.xMax-sx-54,34),bookSearch);
            if(query!=bookSearch){bookSearch=query;bookSelected=null;bookListScroll=bookDetailScroll=Vector2.zero;}
            var rows=CourtyardEncyclopedia.Query(Filter,bookMaterials,bookSearch);
            if(rows.Count==0){Empty(new Rect(r.x,r.y+46,r.width,r.height-46),"NC_BookEmpty".Translate(),0);return;}
            if(bookSelected==null||!rows.Contains(bookSelected)){bookSelected=rows[0];bookDetailScroll=Vector2.zero;}
            float side=Mathf.Min(256,r.width*.29f);
            Rect list=new Rect(r.x,r.y+46,side,r.height-46),detail=new Rect(list.xMax+12,list.y,r.width-side-12,list.height);
            CourtyardStyle.Round(list,CourtyardStyle.Card);CourtyardStyle.Round(detail,CourtyardStyle.Card);
            Rect viewport=list.ContractedBy(8),view=new Rect(0,0,viewport.width-18,rows.Count*62);
            CourtyardStyle.BeginScroll(viewport,ref bookListScroll,view);
            try{for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];Rect card=new Rect(0,i*62,view.width,56);
                CourtyardStyle.Round(card,row==bookSelected?CourtyardStyle.Sage:CourtyardStyle.Paper,8);
                float x=12;
                if(row.material!=null){Widgets.DefIcon(new Rect(8,card.y+10,36,36),row.material);x=50;}
                CourtyardStyle.Label(new Rect(x,card.y+2,card.width-x-8,28),row.Label);
                CourtyardStyle.Label(new Rect(x,card.y+29,card.width-x-8,20),row.ContentLabel,CourtyardStyle.Muted,GameFont.Tiny);
                TooltipHandler.TipRegion(card,row.Label);
                if(Widgets.ButtonInvisible(card)){bookSelected=row;bookDetailScroll=Vector2.zero;}
            }}finally{Widgets.EndScrollView();}
            DrawBookDetail(detail,bookSelected);
        }
        void DrawBookDetail(Rect detail,CourtyardBookEntry selected)
        {
            Rect content=detail.ContractedBy(18);float width=content.width-18;
            var originalFont=Text.Font;Text.Font=GameFont.Small;
            try
            {
                float height=Content(false);
                bookDetailScroll.y=Mathf.Clamp(bookDetailScroll.y,0,Mathf.Max(0,height-content.height));
                CourtyardStyle.BeginScroll(content,ref bookDetailScroll,new Rect(0,0,width,height));
                try{Content(true);}finally{Widgets.EndScrollView();}
            }
            finally{Text.Font=originalFont;}
            float Content(bool draw)
            {
                bool material=selected.material!=null;
                float titleX=material?66:0;
                if(draw)
                {
                    if(material)Widgets.DefIcon(new Rect(0,0,52,52),selected.material);
                    CourtyardStyle.Label(new Rect(titleX,0,width-titleX,34),selected.Label,font:GameFont.Medium);
                    if(material||selected.article.kind!=CourtyardArticleKind.Species)
                        CourtyardStyle.Label(new Rect(titleX,34,width-titleX,22),selected.ContentLabel,CourtyardStyle.Muted);
                }
                float y=material||selected.article.kind!=CourtyardArticleKind.Species?72:48;
                Paragraph(selected.Description??"");
                if(material)
                {
                    y+=10;
                    if(draw)CourtyardStyle.Label(new Rect(0,y,width,28),"NC_BookSources".Translate());
                    y+=36;
                    foreach(string source in CourtyardEncyclopedia.Sources(selected.material))
                    {
                        float h=Mathf.Max(24,Text.CalcHeight(source,width-28))+24;
                        if(draw)
                        {
                            CourtyardStyle.Round(new Rect(0,y,width,h),CourtyardStyle.Paper,8);
                            CourtyardStyle.Label(new Rect(14,y+12,width-28,h-24),source,wrap:true,alignment:TextAnchor.UpperLeft);
                        }
                        y+=h+12;
                    }
                }
                else if(selected.article.role!=null){y+=12;Paragraph(CourtyardEncyclopedia.RoleText(selected.article.role));}
                return y+8;
                void Paragraph(string value)
                {
                    float h=Mathf.Max(24,Text.CalcHeight(value,width));
                    if(draw)CourtyardStyle.Label(new Rect(0,y,width,h),value,wrap:true,alignment:TextAnchor.UpperLeft);
                    y+=h;
                }
            }
        }
    }
}
