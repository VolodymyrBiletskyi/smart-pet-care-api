using System.Collections.Concurrent;

namespace smart_pet_care_api.Modules.WellnessModule.Domain;

public sealed class WellnessCalculationLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

    public async ValueTask<IDisposable> AcquireAsync(Guid petId, CancellationToken cancellationToken)
    {
        var gate = locks.GetOrAdd(petId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
