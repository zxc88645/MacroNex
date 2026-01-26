using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Application.Services;

/// <summary>
/// Factory for creating IInputHookService instances based on InputMode.
/// </summary>
public interface IInputHookServiceFactory
{
    /// <summary>
    /// Gets the appropriate IInputHookService for the specified input mode.
    /// </summary>
    /// <param name="mode">The input mode.</param>
    /// <returns>The IInputHookService instance.</returns>
    IInputHookService GetInputHookService(InputMode mode);
}

/// <summary>
/// Factory implementation for creating IInputHookService instances.
/// </summary>
public sealed class InputHookServiceFactory : IInputHookServiceFactory
{
    private readonly IInputHookService _softwareInputHookService;
    private readonly IInputHookService _hardwareInputHookService;

    public InputHookServiceFactory(
        IInputHookService softwareInputHookService,
        IInputHookService hardwareInputHookService)
    {
        _softwareInputHookService = softwareInputHookService ?? throw new ArgumentNullException(nameof(softwareInputHookService));
        _hardwareInputHookService = hardwareInputHookService ?? throw new ArgumentNullException(nameof(hardwareInputHookService));
    }

    public IInputHookService GetInputHookService(InputMode mode)
    {
        return mode switch
        {
            InputMode.Software => _softwareInputHookService,
            InputMode.Hardware => _hardwareInputHookService,
            _ => throw new ArgumentException($"Unknown input mode: {mode}", nameof(mode))
        };
    }
}
