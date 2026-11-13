using System;

namespace NeighborCourtyards
{
    internal static class DefinitionValidation
    {
        internal static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        internal static bool Positive(float value) => Nonnegative(value) && value > 0;
        internal static bool AgeRange(float minimum, float maximum) => Nonnegative(minimum) && Nonnegative(maximum) && minimum <= maximum;

        internal static bool ConcreteAdapter(Type candidate, Type baseType) =>
            candidate != null && baseType.IsAssignableFrom(candidate) && !candidate.IsAbstract &&
            !candidate.ContainsGenericParameters && candidate.GetConstructor(Type.EmptyTypes) != null;
    }
}
