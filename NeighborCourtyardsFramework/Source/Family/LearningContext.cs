using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace NeighborCourtyards.Family
{
    // A species activity can join the native desire picker without appearing for unrelated children.
    public static class LearningSelectionContext { [ThreadStatic] public static Pawn Pawn; }
    [HarmonyPatch(typeof(Pawn_LearningTracker), "AddNewLearningDesire")]
    static class LearningSelectionScope
    {
        static void Prefix(Pawn ___pawn, out Pawn __state) { __state = LearningSelectionContext.Pawn; LearningSelectionContext.Pawn = ___pawn; }
        static Exception Finalizer(Exception __exception, Pawn __state) { LearningSelectionContext.Pawn = __state; return __exception; }
    }
}
