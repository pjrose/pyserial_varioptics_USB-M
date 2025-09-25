using System.Collections.Concurrent;

namespace VariopticLens;

/// <summary>
/// Provides a high-level registry for <see cref="VariopticLens"/> instances that can be consumed by
/// application layers (e.g. MVVM view models) without needing to manage the underlying serial resources.
/// </summary>
public class LensManager
{
    private readonly ConcurrentDictionary<string, VariopticLens> _lenses;
    private readonly int _capacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="LensManager"/> class.
    /// </summary>
    /// <param name="capacity">The maximum number of lenses that can be tracked concurrently. Defaults to four.</param>
    public LensManager(int capacity = 4)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }

        _capacity = capacity;
        _lenses = new ConcurrentDictionary<string, VariopticLens>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the number of lenses currently managed by this instance.
    /// </summary>
    public int Count => _lenses.Count;

    /// <summary>
    /// Adds a lens with the specified friendly name to the manager.
    /// </summary>
    /// <param name="name">A unique friendly name used to identify the lens.</param>
    /// <param name="lens">The lens instance to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="lens"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the capacity has been reached or a lens with the same name already exists.</exception>
    public void AddLens(string name, VariopticLens lens)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(lens);

        if (Count >= _capacity)
        {
            throw new InvalidOperationException($"The manager can only track {_capacity} lenses simultaneously.");
        }

        if (!_lenses.TryAdd(name, lens))
        {
            throw new InvalidOperationException($"A lens named '{name}' has already been registered.");
        }
    }

    /// <summary>
    /// Removes the lens with the specified friendly name from the manager.
    /// </summary>
    /// <param name="name">The friendly name of the lens to remove.</param>
    /// <param name="dispose">
    /// When set to <see langword="true"/>, the removed lens is disposed immediately.
    /// This is useful when the manager is considered the owner of the registered instance.
    /// </param>
    /// <returns><see langword="true"/> when the lens was found and removed; otherwise <see langword="false"/>.</returns>
    public bool RemoveLens(string name, bool dispose = false)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (_lenses.TryRemove(name, out var lens))
        {
            if (dispose)
            {
                lens.Dispose();
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to retrieve a lens by its friendly name.
    /// </summary>
    /// <param name="name">The friendly name of the lens.</param>
    /// <param name="lens">When the method returns, contains the registered lens instance if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a lens with the provided name exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetLens(string name, out VariopticLens? lens)
    {
        ArgumentNullException.ThrowIfNull(name);
        var found = _lenses.TryGetValue(name, out var existing);
        lens = existing;
        return found;
    }

    /// <summary>
    /// Retrieves a lens by its friendly name or throws an exception when the lens cannot be found.
    /// </summary>
    /// <param name="name">The friendly name of the lens.</param>
    /// <returns>The registered <see cref="VariopticLens"/> instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no lens matches the provided name.</exception>
    public VariopticLens GetLens(string name)
    {
        if (!TryGetLens(name, out var lens) || lens is null)
        {
            throw new KeyNotFoundException($"No lens has been registered with the name '{name}'.");
        }

        return lens;
    }
}
