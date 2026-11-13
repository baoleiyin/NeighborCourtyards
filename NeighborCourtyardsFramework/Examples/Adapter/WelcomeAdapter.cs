using NeighborCourtyards;
using Verse;

namespace StarterNeighbor
{
    // Compile into your extension's Assemblies directory, then set its species'
    // adapterClass to StarterNeighbor.WelcomeAdapter.
    public sealed class WelcomeAdapter : AssistantSpeciesAdapter
    {
        public override void OnWelcomed(Pawn pawn, CompCourtyard courtyard)
        {
            Log.Message("[StarterNeighbor] Welcomed " + pawn.ThingID + " at " + courtyard.parent.ThingID);
        }
    }
}
