using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MacroStudio.Application.Services;

/// <summary>
/// Application service for managing Arduino connection state and health monitoring.
/// </summary>
public sealed class ArduinoConnectionService : IDisposable
{
    private readonly IArduinoService _arduinoService;
    private readonly ILogger<ArduinoConnectionService> _logger;
    private bool _isDisposed;

    public ArduinoConnectionService(IArduinoService arduinoService, ILogger<ArduinoConnectionService> logger)
    {
        _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to connection state changes
        _arduinoService.ConnectionStateChanged += OnConnectionStateChanged;
        _arduinoService.ErrorOccurred += OnErrorOccurred;
    }

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    public event EventHandler<ArduinoConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ArduinoConnectionState ConnectionState => _arduinoService.ConnectionState;

    /// <summary>
    /// Gets whether the Arduino is currently connected.
    /// </summary>
    public bool IsConnected => _arduinoService.IsConnected;

    /// <summary>
    /// Gets the currently connected port name, or null if not connected.
    /// </summary>
    public string? ConnectedPortName => _arduinoService.ConnectedPortName;

    /// <summary>
    /// Gets a list of available serial ports.
    /// </summary>
    public Task<IReadOnlyList<string>> GetAvailablePortsAsync() => _arduinoService.GetAvailablePortsAsync();

    /// <summary>
    /// Connects to the Arduino on the specified port.
    /// </summary>
    public Task ConnectAsync(string portName) => _arduinoService.ConnectAsync(portName);

    /// <summary>
    /// Disconnects from the Arduino.
    /// </summary>
    public Task DisconnectAsync() => _arduinoService.DisconnectAsync();

    /// <summary>
    /// Validates that the Arduino is ready for use in hardware mode.
    /// </summary>
    /// <returns>True if ready, false otherwise.</returns>
    public bool ValidateConnection()
    {
        if (!_arduinoService.IsConnected)
        {
            _logger.LogWarning("Arduino is not connected");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates connection and throws if not ready.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when Arduino is not connected.</exception>
    public void EnsureConnected()
    {
        if (!_arduinoService.IsConnected)
        {
            throw new InvalidOperationException("Arduino is not connected. Please connect to an Arduino device before using hardware mode.");
        }
    }

    private void OnConnectionStateChanged(object? sender, ArduinoConnectionStateChangedEventArgs e)
    {
        _logger.LogInformation("Arduino connection state changed: {PreviousState} -> {NewState} (Port: {PortName})",
            e.PreviousState, e.NewState, e.PortName ?? "N/A");
        
        // Forward the event
        ConnectionStateChanged?.Invoke(this, e);
    }

    private void OnErrorOccurred(object? sender, ArduinoErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "Arduino error: {Message}", e.Message);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ArduinoConnectionService));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _arduinoService.ConnectionStateChanged -= OnConnectionStateChanged;
        _arduinoService.ErrorOccurred -= OnErrorOccurred;
    }
}
