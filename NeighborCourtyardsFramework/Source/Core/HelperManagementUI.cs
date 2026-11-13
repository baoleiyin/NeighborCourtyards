using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public enum CourtyardIcon { Overview, Neighbors, Work, Camp, Guide, Clean, Haul, Food, Rest, Shelter, Locate, Attention }
    public static partial class CourtyardStyle
    {
        static Texture2D helperIcons;
        public static void Icon(Rect r, CourtyardIcon icon)
        {
            if (helperIcons == null) helperIcons = ContentFinder<Texture2D>.Get("CourtyardUI/HelperIcons", false);
            if (helperIcons == null) { Icon(r, icon == CourtyardIcon.Neighbors ? 1 : icon == CourtyardIcon.Food ? 2 : 0); return; }
            int index = (int)icon;
            var old = GUI.color; GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(r, helperIcons, new Rect((index % 4) / 4f, 1f - (index / 4 + 1) / 3f, .25f, 1f / 3));
            GUI.color = old;
        }
        public static bool Action(Rect r, string label, CourtyardIcon icon, bool active = false)
        {
            Round(r, active ? Sage : Mouse.IsOver(r) ? Peach : Card, 8);
            float size = Mathf.Min(30, r.height - 8);
            Icon(new Rect(r.x + 7, r.center.y - size / 2, size, size), icon);
            Label(new Rect(r.x + size + 13, r.y, r.width - size - 20, r.height), label);
            TooltipHandler.TipRegion(r, label);
            return Widgets.ButtonInvisible(r);
        }
    }

}
