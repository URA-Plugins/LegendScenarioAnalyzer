namespace LegendScenarioAnalyzer;

internal static class LegendTrainingDisplayRegistry
{
    static readonly object Gate = new();
    static readonly List<Registration> Registrations = [];
    static long nextSequence;

    internal static IDisposable Register(
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> modifier,
        int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(modifier);

        var registration = new Registration(Interlocked.Increment(ref nextSequence), priority, modifier);
        lock (Gate)
            Registrations.Add(registration);

        return new RegistrySubscription(registration.Sequence);
    }

    internal static IReadOnlyList<Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor>> Snapshot()
    {
        lock (Gate)
        {
            return Registrations
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Sequence)
                .Select(x => x.Modifier)
                .ToArray();
        }
    }

    sealed record Registration(
        long Sequence,
        int Priority,
        Action<LegendTrainingDisplayContext, LegendTrainingDisplayEditor> Modifier);

    sealed class RegistrySubscription(long sequence) : IDisposable
    {
        bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            lock (Gate)
                Registrations.RemoveAll(x => x.Sequence == sequence);

            disposed = true;
        }
    }
}
