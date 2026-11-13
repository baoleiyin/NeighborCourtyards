using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace NeighborCourtyards
{
    public enum EmotionBubbleTrigger { MoodHigh, AfterMeal, JoySatisfied, Signal, MoodLow, FoodLow, RestLow }

    public sealed class EmotionBubbleDef : Def
    {
        public EmotionBubbleTrigger trigger;
        public float threshold = .75f;
        public string signal, texture;
        public int priority = 10, durationTicks = 150, cooldownTicks = 1800;
        public Rect uvRect = new Rect(0f, 0f, 1f, 1f);
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (durationTicks < 30 || cooldownTicks < 0) yield return "Emotion bubble requires durationTicks >= 30 and cooldownTicks >= 0.";
            if (threshold < 0f || threshold > 1f) yield return "Emotion bubble threshold must be between 0 and 1.";
            if (trigger == EmotionBubbleTrigger.Signal && string.IsNullOrEmpty(signal)) yield return "Signal emotion bubble requires a signal name.";
            if (string.IsNullOrEmpty(texture)) yield return "Emotion bubble requires an icon texture.";
            if (uvRect.width <= 0f || uvRect.height <= 0f || uvRect.x < 0f || uvRect.y < 0f || uvRect.xMax > 1.0001f || uvRect.yMax > 1.0001f)
                yield return "Emotion bubble uvRect must be a positive normalized rectangle.";
        }
    }

    public sealed class EmotionBubbleStyleDef : Def
    {
        public Color fillColor = new Color(1f, .98f, .91f), outlineColor = new Color(.30f, .19f, .13f);
        public float size = .46f, outline = .025f, iconScale = .64f;
        public Vector3 offset = new Vector3(.30f, 0f, 1.03f);
        public Type anchorClass = typeof(EmotionBubbleAnchor);
        EmotionBubbleAnchor anchor;
        public EmotionBubbleAnchor Anchor => anchor ?? (anchor = (EmotionBubbleAnchor)Activator.CreateInstance(anchorClass));
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (size <= 0f || outline < 0f || outline * 2f >= size || iconScale <= 0f || iconScale > 1f) yield return "Invalid emotion bubble style dimensions.";
            if (anchorClass == null || !typeof(EmotionBubbleAnchor).IsAssignableFrom(anchorClass) || anchorClass.IsAbstract)
                yield return "Emotion bubble anchor must be a concrete EmotionBubbleAnchor.";
        }
    }

    public class EmotionBubbleAnchor
    {
        public virtual float Scale(Pawn pawn) => 1f;
        public virtual Vector3 Position(Pawn pawn, EmotionBubbleStyleDef style) => pawn.DrawPos + style.offset * Scale(pawn);
    }

    public sealed class CompProperties_EmotionBubble : CompProperties
    {
        public List<EmotionBubbleDef> emotions = new List<EmotionBubbleDef>();
        public EmotionBubbleStyleDef style;
        public int pollInterval = 30, minGapTicks = 480;
        public CompProperties_EmotionBubble() { compClass = typeof(CompEmotionBubble); }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (parentDef.race == null) yield return "Emotion bubbles can only be attached to pawns.";
            if (style == null || emotions == null || emotions.Count == 0 || emotions.Any(e => e == null)) yield return "Emotion bubbles require a style and resolved emotion defs.";
            if (emotions != null && emotions.Distinct().Count() != emotions.Count) yield return "Emotion bubble defs must not repeat within a component.";
            if (pollInterval < 1 || minGapTicks < 0) yield return "Invalid emotion bubble polling or gap interval.";
        }
    }

    // The command is explicitly preserved by the shared assistant gizmo filter.
    public sealed class Command_EmotionBubbleToggle : Command_Toggle { }

    public sealed class CompEmotionBubble : ThingComp
    {
        EmotionBubbleDef active;
        int startedAt, endsAt, nextAllowedAt, nextPoll, lastTick = -1;
        bool enabled = true, observed, wasEating, wasEnjoying, restoreTiming;
        float foodAtStart, joyAtStart;
        List<EmotionBubbleDef> latched = new List<EmotionBubbleDef>();
        Dictionary<EmotionBubbleDef, int> readyAt = new Dictionary<EmotionBubbleDef, int>();
        List<EmotionBubbleDef> savedCooldownDefs;
        List<int> savedCooldownTicks;
        int savedAge, savedRemaining, savedGap;

        public CompProperties_EmotionBubble Props => (CompProperties_EmotionBubble)props;
        public Pawn ParentPawn => parent as Pawn;
        public EmotionBubbleDef Active => active;
        public int StartedAt => startedAt;
        public int EndsAt => endsAt;
        public bool Enabled => enabled;
        static int Now => Find.TickManager?.TicksGame ?? 0;
        public bool CanDisplay => enabled && ParentPawn != null && ParentPawn.Spawned && !ParentPawn.Dead && !ParentPawn.Downed &&
            !ParentPawn.InMentalState && !ParentPawn.IsBurning() && ParentPawn.Awake() && ParentPawn.GetPosture() == PawnPosture.Standing;
        public float Opacity => active == null ? 0f : Mathf.Clamp01(Math.Min((Now - startedAt) / 12f, (endsAt - Now) / 24f));
        public float VisualScale => active == null ? 0f : .82f + .18f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Now - startedAt) / 12f));

        public override void CompTick() { TickState(); }
        public override void CompTickInterval(int delta) { TickState(); }
        public override void PostDraw() { base.PostDraw(); RestoreTiming(); EmotionBubbleRenderer.Draw(this); }
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        { base.PostDeSpawn(map, mode); Clear(); observed = false; EmotionBubbleRenderer.Release(this); }
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        { base.PostDestroy(mode, previousMap); Clear(); EmotionBubbleRenderer.Release(this); }

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!value) { Clear(); observed = false; EmotionBubbleRenderer.Release(this); }
        }
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer) yield break;
            yield return new Command_EmotionBubbleToggle {
                defaultLabel = "NC_EmotionBubbles".Translate().ToString(), defaultDesc = "NC_EmotionBubblesDesc".Translate().ToString(),
                icon = EmotionBubbleRenderer.ToggleIcon, isActive = () => enabled, toggleAction = () => SetEnabled(!enabled)
            };
        }

        // Returns false for suppressed events. No deferred queue is kept.
        public bool Signal(string signal)
        {
            RestoreTiming();
            EmotionBubbleDef candidate = Props.emotions.Where(e => e.trigger == EmotionBubbleTrigger.Signal && e.signal == signal)
                .OrderByDescending(e => e.priority).FirstOrDefault(e => Eligible(e, Now));
            return candidate != null && TryShow(candidate);
        }
        public bool TryShow(EmotionBubbleDef emotion)
        {
            RestoreTiming();
            int now = Now;
            if (!CanDisplay || emotion == null || !Props.emotions.Contains(emotion) || !Eligible(emotion, now)) return false;
            active = emotion; startedAt = now; endsAt = now + emotion.durationTicks;
            nextAllowedAt = endsAt + Props.minGapTicks;
            readyAt[emotion] = now + emotion.cooldownTicks;
            return true;
        }
        bool Eligible(EmotionBubbleDef emotion, int now)
        {
            if (readyAt.TryGetValue(emotion, out int ready) && now < ready) return false;
            if (active != null && now < endsAt) return emotion.priority > active.priority;
            return now >= nextAllowedAt;
        }
        void Clear() { active = null; startedAt = endsAt = 0; }

        void TickState()
        {
            RestoreTiming();
            int now = Now;
            if (lastTick == now) return;
            lastTick = now;
            if (!CanDisplay) { Clear(); observed = false; return; }
            if (active != null && (now >= endsAt || IsState(active) && !Condition(active, false))) Clear();
            Pawn pawn = ParentPawn;
            bool eating = pawn.CurJobDef == JobDefOf.Ingest;
            bool enjoying = pawn.CurJobDef?.joyKind != null;
            float food = pawn.needs?.food?.CurLevelPercentage ?? 0f;
            float joy = pawn.needs?.joy?.CurLevelPercentage ?? 0f;
            bool mealFinished = observed && wasEating && !eating && food > foodAtStart + .02f;
            bool joyFinished = observed && wasEnjoying && !enjoying && joy > joyAtStart + .02f;
            if (!observed || eating && !wasEating) foodAtStart = food;
            if (!observed || enjoying && !wasEnjoying) joyAtStart = joy;
            wasEating = eating; wasEnjoying = enjoying; observed = true;
            if (now < nextPoll && !mealFinished && !joyFinished) return;
            nextPoll = now + Math.Max(1, Props.pollInterval);
            latched.RemoveAll(e => e == null || !Props.emotions.Contains(e) || !Condition(e, true));
            EmotionBubbleDef candidate = null;
            foreach (EmotionBubbleDef emotion in Props.emotions)
            {
                bool triggered = IsState(emotion) ? !latched.Contains(emotion) && Condition(emotion, false) :
                    emotion.trigger == EmotionBubbleTrigger.AfterMeal ? mealFinished :
                    emotion.trigger == EmotionBubbleTrigger.JoySatisfied && joyFinished;
                if (triggered && Eligible(emotion, now) && (candidate == null || emotion.priority > candidate.priority)) candidate = emotion;
            }
            if (candidate != null && TryShow(candidate) && IsState(candidate)) latched.Add(candidate);
        }
        static bool IsState(EmotionBubbleDef emotion) => emotion.trigger == EmotionBubbleTrigger.MoodHigh || emotion.trigger == EmotionBubbleTrigger.MoodLow ||
            emotion.trigger == EmotionBubbleTrigger.FoodLow || emotion.trigger == EmotionBubbleTrigger.RestLow;
        bool Condition(EmotionBubbleDef emotion, bool hysteresis)
        {
            Need need = emotion.trigger == EmotionBubbleTrigger.FoodLow ? (Need)ParentPawn.needs?.food :
                emotion.trigger == EmotionBubbleTrigger.RestLow ? ParentPawn.needs?.rest : ParentPawn.needs?.mood;
            if (need == null) return false;
            float margin = hysteresis ? .05f : 0f;
            return emotion.trigger == EmotionBubbleTrigger.MoodHigh ? need.CurLevelPercentage >= emotion.threshold - margin :
                need.CurLevelPercentage <= emotion.threshold + margin;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                RestoreTiming();
                savedAge = active == null ? 0 : Math.Max(0, Now - startedAt);
                savedRemaining = active == null ? 0 : Math.Max(0, endsAt - Now);
                savedGap = Math.Max(0, nextAllowedAt - Now);
                savedCooldownDefs = new List<EmotionBubbleDef>(); savedCooldownTicks = new List<int>();
                foreach (var entry in readyAt.Where(e => e.Key != null && e.Value > Now))
                { savedCooldownDefs.Add(entry.Key); savedCooldownTicks.Add(entry.Value - Now); }
            }
            Scribe_Values.Look(ref enabled, "ncEmotionEnabled", true);
            Scribe_Defs.Look(ref active, "ncEmotionActive");
            Scribe_Values.Look(ref savedAge, "ncEmotionAge");
            Scribe_Values.Look(ref savedRemaining, "ncEmotionRemaining");
            Scribe_Values.Look(ref savedGap, "ncEmotionGap");
            Scribe_Collections.Look(ref savedCooldownDefs, "ncEmotionCooldownDefs", LookMode.Def);
            Scribe_Collections.Look(ref savedCooldownTicks, "ncEmotionCooldownTicks", LookMode.Value);
            Scribe_Collections.Look(ref latched, "ncEmotionLatched", LookMode.Def);
            // Preserve in-progress activities so saving during a meal does not invent a new completion event.
            Scribe_Values.Look(ref observed, "ncEmotionObserved");
            Scribe_Values.Look(ref wasEating, "ncEmotionWasEating");
            Scribe_Values.Look(ref wasEnjoying, "ncEmotionWasEnjoying");
            Scribe_Values.Look(ref foodAtStart, "ncEmotionFoodAtStart");
            Scribe_Values.Look(ref joyAtStart, "ncEmotionJoyAtStart");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            { restoreTiming = true; latched = latched ?? new List<EmotionBubbleDef>(); lastTick = -1; nextPoll = 0; }
        }
        void RestoreTiming()
        {
            if (!restoreTiming) return;
            restoreTiming = false;
            int now = Now;
            if (active != null && Props.emotions.Contains(active) && savedRemaining > 0)
            { startedAt = now - Math.Max(0, savedAge); endsAt = now + savedRemaining; }
            else Clear();
            nextAllowedAt = now + Math.Max(0, savedGap);
            readyAt.Clear();
            if (savedCooldownDefs != null && savedCooldownTicks != null)
                for (int i = 0; i < Math.Min(savedCooldownDefs.Count, savedCooldownTicks.Count); i++)
                    if (savedCooldownDefs[i] != null && Props.emotions.Contains(savedCooldownDefs[i]))
                        readyAt[savedCooldownDefs[i]] = now + Math.Max(0, savedCooldownTicks[i]);
        }
    }
}
