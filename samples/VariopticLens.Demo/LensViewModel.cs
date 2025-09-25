using System.ComponentModel;
using System.Runtime.CompilerServices;
using VariopticLens;

namespace VariopticLens.Demo;

/// <summary>
/// A lightweight MVVM view model that demonstrates how to interact with the <see cref="VariopticLens.VariopticLens"/>
/// library while exposing bindable properties and user actions.
/// </summary>
public class LensViewModel : INotifyPropertyChanged
{
    private readonly VariopticLens.VariopticLens _lens;
    private ushort _currentFocus;

    /// <summary>
    /// Initializes a new instance of the <see cref="LensViewModel"/> class.
    /// </summary>
    /// <param name="manager">The shared <see cref="LensManager"/> that holds the lens instances.</param>
    /// <param name="lensName">The friendly name of the lens to attach to.</param>
    public LensViewModel(LensManager manager, string lensName)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(lensName);

        LensName = lensName;
        _lens = manager.GetLens(lensName);
        _lens.PropertyChanged += LensOnPropertyChanged;
        _currentFocus = _lens.CurrentFocus;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the friendly name of the lens represented by this view model.
    /// </summary>
    public string LensName { get; }

    /// <summary>
    /// Gets the current focus value exposed to the view.
    /// </summary>
    public ushort CurrentFocus
    {
        get => _currentFocus;
        private set
        {
            if (_currentFocus != value)
            {
                _currentFocus = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Initializes the underlying lens using the provided configuration flags.
    /// </summary>
    /// <param name="analog">When set to <see langword="true"/>, the driver is configured for analog input.</param>
    /// <param name="standby">When set to <see langword="true"/>, the driver is placed into standby mode.</param>
    public void InitializeLens(bool analog, bool standby)
    {
        _lens.Initialize(analog, standby);
        CurrentFocus = _lens.CurrentFocus;
    }

    /// <summary>
    /// Sends a new focus value to the lens.
    /// </summary>
    /// <param name="value">The focus value to apply.</param>
    public void SetFocus(ushort value)
    {
        _lens.SetFocusValue(value);
        CurrentFocus = _lens.CurrentFocus;
    }

    /// <summary>
    /// Requests the current focus value from the lens and updates the view model.
    /// </summary>
    public void RefreshFocus()
    {
        CurrentFocus = _lens.GetFocusValue();
    }

    private void LensOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VariopticLens.VariopticLens.CurrentFocus))
        {
            CurrentFocus = _lens.CurrentFocus;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
