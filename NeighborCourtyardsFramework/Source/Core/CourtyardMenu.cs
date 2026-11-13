using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    // Only the framework's selectors use this skin; other mods keep their menus.
    public sealed class CourtyardMenu : FloatMenu
    {
        Vector2 scroll;
        public CourtyardMenu(List<FloatMenuOption> choices):base(choices){vanishIfMouseDistant=false;}
        public override Vector2 InitialSize
        {
            get
            {
                var font=Text.Font;Text.Font=GameFont.Small;
                try{return new Vector2(Mathf.Min(UI.screenWidth-16,Mathf.Clamp(options.Max(o=>Text.CalcSize(o.Label).x)+42,170,330)),Mathf.Min(options.Count*38+16,UI.screenHeight*.75f));}
                finally{Text.Font=font;}
            }
        }
        public override void DoWindowContents(Rect rect)
        {
            CourtyardStyle.Round(rect,CourtyardStyle.Paper,10);
            Rect area=rect.ContractedBy(8),view=new Rect(0,0,area.width-(options.Count*38>area.height?12:0),options.Count*38);
            CourtyardStyle.BeginScroll(area,ref scroll,view);
            try
            {
                for(int i=0;i<options.Count;i++)
                {
                    var option=options[i];Rect row=new Rect(0,i*38,view.width,34);
                    if(option.Disabled)CourtyardStyle.Label(row.ContractedBy(12,0),option.Label,CourtyardStyle.Muted);
                    else if(CourtyardStyle.Button(row,option.Label)){option.Chosen(false,this);Close();break;}
                    TooltipHandler.TipRegion(row,option.Label);
                }
            }
            finally{Widgets.EndScrollView();}
        }
    }
}
