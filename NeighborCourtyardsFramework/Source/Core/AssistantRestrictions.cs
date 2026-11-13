using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace NeighborCourtyards
{
    public sealed partial class CompAssistant
    {
        public List<WorkTypeDef> disabledWork = new List<WorkTypeDef>();
        public WorkTypeDef preferredWork;
        internal int nextHelperWorkCheck;
        List<WorkTypeDef> enabledWork = new List<WorkTypeDef>();
        public const int OptionalWorkSkillThreshold = 6;
        public bool UsesOptionalWork => Settings.species.allowOptionalWork;
        int unskilledThreshold = -1;
        Area previousArea;
        bool shelterAreaApplied;
        public int UnskilledThreshold => unskilledThreshold < 0 ? Settings.defaultUnskilledThreshold : unskilledThreshold;
        void ExposeRestrictions()
        {
            Scribe_Defs.Look(ref preferredWork, "ncPreferredWork");
            Scribe_Collections.Look(ref disabledWork, "ncDisabledWork", LookMode.Def);
            Scribe_Collections.Look(ref enabledWork, "ncEnabledWork", LookMode.Def);
            Scribe_Values.Look(ref unskilledThreshold, "ncUnskilledThreshold", -1);
            Scribe_References.Look(ref previousArea, "ncPreviousArea");
            Scribe_Values.Look(ref shelterAreaApplied, "ncShelterAreaApplied");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                disabledWork = disabledWork?.Where(w => w != null).Distinct().ToList() ?? new List<WorkTypeDef>();
                enabledWork = enabledWork?.Where(w => w != null && !disabledWork.Contains(w)).Distinct().ToList() ?? new List<WorkTypeDef>();
                unskilledThreshold = Math.Max(-1, Math.Min(20, unskilledThreshold));
            }
        }
        public bool Unskilled(WorkTypeDef work)
        {
            // Optional work uses skill only for defaults; explicit choices always win.
            if (UsesOptionalWork) return false;
            int threshold = UnskilledThreshold;
            var skills = work?.relevantSkills;
            if (threshold <= 0 || skills == null || skills.Count == 0) return false;
            var tracker = Pawn.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = tracker?.GetSkill(skills[i]);
                if (skill != null && (skill.passion != Passion.None || skill.Level >= threshold)) return false;
            }
            return true;
        }
        public bool WorkEnabled(WorkTypeDef work)
        {
            if (work == null || disabledWork.Contains(work)) return false;
            if (!UsesOptionalWork) return !Unskilled(work);
            if (enabledWork.Contains(work)) return true;
            if (Roles.For(Pawn).work.Any(w => w.workType == work && w.priority > 0)) return true;
            return work.relevantSkills != null && work.relevantSkills.Any(s =>
                Pawn.skills?.GetSkill(s)?.Level > OptionalWorkSkillThreshold);
        }
        public List<WorkTypeDef> ConfigurableWorkTypes()
        {
            IEnumerable<WorkTypeDef> works = UsesOptionalWork
                ? DefDatabase<WorkTypeDef>.AllDefsListForReading
                : Roles.For(Pawn).work.Where(w => w.priority > 0).Select(w => w.workType);
            return works.Where(w => w != null && w.defName != "Patient" && w.defName != "PatientBedRest").Distinct().ToList();
        }
        public bool BlocksWork(WorkTypeDef work) => Home?.shelter == true || !WorkEnabled(work);
        public void SetWorkEnabled(WorkTypeDef work, bool enabled)
        {
            if (work == null || work.defName == "Patient" || work.defName == "PatientBedRest") return;
            disabledWork.Remove(work);
            enabledWork.Remove(work);
            if (enabled && UsesOptionalWork) enabledWork.Add(work);
            if (!enabled) disabledWork.Add(work);
            WorkPolicyChanged();
        }
        public void SetUnskilledThreshold(int value) { unskilledThreshold = Math.Max(0, Math.Min(20, value)); WorkPolicyChanged(); }
        public void SetPreferredWork(WorkTypeDef work)
        {
            if (work != null && !ConfigurableWorkTypes().Contains(work)) return;
            preferredWork = work; WorkPolicyChanged();
        }
        void WorkPolicyChanged()
        {
            nextHelperWorkCheck = 0;
            Refresh();
            var work = Roles.Helper(Pawn) ? HelperServices.WorkFor(Pawn, Pawn.CurJob) : Roles.Profile(Pawn).Adapter.WorkTypeForJob(Pawn, Pawn.CurJob);
            if (work != null && Roles.Priority(Pawn, work) == 0 && !Pawn.Downed && !Pawn.InMentalState)
                Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
        public void RefreshArea()
        {
            if (Pawn.playerSettings == null || !Pawn.Spawned) return;
            var homeComp = Home;
            homeComp?.ValidateAreas();
            if (previousArea != null && !Pawn.Map.areaManager.AllAreas.Contains(previousArea)) previousArea = null;
            Area shelterArea = homeComp?.shelter == true ? homeComp.shelterArea ?? homeComp.serviceArea : null;
            if (shelterArea != null)
            {
                if (!shelterAreaApplied) previousArea = Pawn.playerSettings.AreaRestrictionInPawnCurrentMap;
                shelterAreaApplied = true;
                Pawn.playerSettings.AreaRestrictionInPawnCurrentMap = shelterArea;
            }
            else
            {
                if (shelterAreaApplied) Pawn.playerSettings.AreaRestrictionInPawnCurrentMap = previousArea;
                previousArea = null; shelterAreaApplied = false;
                // An unset courtyard policy must not erase a player's native area selection.
                if (homeComp?.manageArea == true || homeComp?.serviceArea != null)
                    Pawn.playerSettings.AreaRestrictionInPawnCurrentMap = homeComp.serviceArea;
            }
        }
    }

    public sealed partial class CompCourtyard
    {
        public Area shelterArea;
        public bool manageArea;
        int nextShelterWarning;
        public void ValidateAreas()
        {
            if (!parent.Spawned) return;
            if (serviceArea != null && !parent.Map.areaManager.AllAreas.Contains(serviceArea)) serviceArea = null;
            if (shelterArea != null && !parent.Map.areaManager.AllAreas.Contains(shelterArea)) shelterArea = null;
        }
        void ExposeAreas()
        {
            Scribe_References.Look(ref shelterArea, "ncShelterArea");
            Scribe_Values.Look(ref manageArea, "ncManageArea");
        }
        public bool AcceptArea(Area area)
        {
            if (area == null || (parent.Map.areaManager.AllAreas.Contains(area) && area.ActiveCells.Any())) return true;
            Messages.Message("NC_EmptyArea".Translate(), MessageTypeDefOf.RejectInput, false); return false;
        }
        public void SetShelterArea(Area area) { if (!AcceptArea(area)) return; shelterArea = area; if (shelter) ApplyAreaPolicy(true); }
        public void UseNativeAreas() { serviceArea = null; manageArea = false; ApplyAreaPolicy(false); }
        void ApplyAreaPolicy(bool shelterChanged)
        {
            foreach (Pawn p in Residents.Where(p => p.Spawned && p.GetComp<CompAssistant>()?.Home == this).ToList())
            {
                p.GetComp<CompAssistant>().Refresh();
                if (p.Downed || p.InMentalState || !p.Awake() || p.jobs?.curDriver == null) continue;
                Job job = p.CurJob;
                Area area = p.playerSettings?.AreaRestrictionInPawnCurrentMap;
                bool Outside(LocalTargetInfo target) => area != null && target.IsValid && target.Cell.InBounds(p.Map) && !area[target.Cell];
                bool forbiddenTarget = Outside(job.targetA) || Outside(job.targetB) || Outside(job.targetC) ||
                    (job.targetQueueA?.Any(Outside) ?? false) || (job.targetQueueB?.Any(Outside) ?? false);
                bool work = job.workGiverDef != null || job.def.GetModExtension<AssistantSupplyJob>() != null || Roles.Helper(p) && HelperServices.WorkFor(p, job) != null;
                if ((shelterChanged && work) || forbiddenTarget || (shelterChanged && shelter && area != null && !area[p.Position]))
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
        public Job ShelterJob(Pawn p)
        {
            ValidateAreas();
            Area area = shelterArea ?? serviceArea ?? p.playerSettings?.AreaRestrictionInPawnCurrentMap;
            bool Safe(IntVec3 c) => c.InBounds(p.Map) && c.Standable(p.Map) && !c.IsForbidden(p) &&
                !c.ContainsStaticFire(p.Map) && p.CanReach(c, PathEndMode.OnCell, Danger.None);
            IntVec3 target = IntVec3.Invalid;
            if (area != null)
            {
                if (!area[p.Position] || !Safe(p.Position))
                    target = area.ActiveCells.Where(Safe).OrderBy(c => c.DistanceToSquared(p.Position)).DefaultIfEmpty(IntVec3.Invalid).First();
            }
            else if (p.Position.DistanceToSquared(parent.Position) > 16)
                target = GenRadial.RadialCellsAround(parent.Position, 3, true).Where(Safe).OrderBy(c => c.DistanceToSquared(p.Position)).DefaultIfEmpty(IntVec3.Invalid).First();
            if (target.IsValid) return JobMaker.MakeJob(JobDefOf.Goto, target);
            if ((area != null && (!area[p.Position] || !Safe(p.Position)) || area == null && p.Position.DistanceToSquared(parent.Position) > 16) &&
                Find.TickManager.TicksGame >= nextShelterWarning)
            {
                nextShelterWarning = Find.TickManager.TicksGame + 2500;
                Messages.Message("NC_ShelterUnreachable".Translate(p.LabelShortCap), p, MessageTypeDefOf.RejectInput, false);
            }
            Job wait = JobMaker.MakeJob(JobDefOf.Wait, p.Position); wait.expiryInterval = 250; return wait;
        }
    }
}
