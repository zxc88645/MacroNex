using MacroNex.Domain.Interfaces;
using MacroNex.Domain.ValueObjects;
using MacroNex.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using static MacroNex.Infrastructure.Win32.Win32Api;
using static MacroNex.Infrastructure.Win32.Win32Structures;

namespace MacroNex.Infrastructure.Adapters;

/// <summary>
/// Arduino-based implementation of IInputSimulator that sends commands to Arduino Leonardo.
/// Uses calibration data (if available) to convert pixel deltas to HID deltas for accurate movement.
/// </summary>
public sealed class ArduinoInputSimulator : IInputSimulator, IDisposable
{
    private readonly IArduinoService _arduinoService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ArduinoInputSimulator> _logger;
    private bool _isDisposed;
    
    // Cached calibration data to avoid loading settings on every move
    private MouseCalibrationData? _calibrationData;
    private DateTime _calibrationLoadedAt;
    private static readonly TimeSpan CalibrationCacheExpiry = TimeSpan.FromMinutes(5);

    public ArduinoInputSimulator(
        IArduinoService arduinoService, 
        ISettingsService settingsService,
        ILogger<ArduinoInputSimulator> logger)
    {
        _arduinoService = arduinoService ?? throw new ArgumentNullException(nameof(arduinoService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SimulateMouseMoveAsync(Point position)
    {
        ThrowIfDisposed();
        ValidatePosition(position);

        if (!_arduinoService.IsConnected)
            throw new InvalidOperationException("Arduino is not connected.");

        _logger.LogDebug("Simulating mouse move to {Position} via Arduino", position);

        try
        {
            // Get current cursor position using Win32 API
            var currentPosition = await GetCursorPositionInternalAsync();
            
            // Calculate target pixel movement
            int targetDeltaX = position.X - currentPosition.X;
            int targetDeltaY = position.Y - currentPosition.Y;
            
            _logger.LogTrace("Current position: {Current}, Target: {Target}, PixelDelta: ({DeltaX}, {DeltaY})", 
                currentPosition, position, targetDeltaX, targetDeltaY);
            
            // Skip if no movement needed
            if (targetDeltaX == 0 && targetDeltaY == 0)
            {
                _logger.LogTrace("No movement needed, already at target position");
                return;
            }
            
            // Load calibration data and calculate HID deltas
            await RefreshCalibrationDataAsync();
            int hidDeltaX = CalculateHidDelta(targetDeltaX, useYAxis: false);
            int hidDeltaY = CalculateHidDelta(targetDeltaY, useYAxis: true);
            
            _logger.LogTrace("Calibrated HID Delta: ({HidDeltaX}, {HidDeltaY})", hidDeltaX, hidDeltaY);
            
            // Send relative move command to Arduino
            var command = new ArduinoMouseMoveRelativeCommand(hidDeltaX, hidDeltaY);
            await _arduinoService.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate mouse move to {Position} via Arduino", position);
            throw new InputSimulationException($"Failed to simulate mouse move to {position} via Arduino", ex);
        }
    }

    public async Task SimulateMouseMoveLowLevelAsync(Point position)
    {
        // For Arduino, low-level and regular move are the same (both use relative movement now)
        await SimulateMouseMoveAsync(position);
    }

    public async Task SimulateMouseMoveRelativeAsync(int deltaX, int deltaY)
    {
        ThrowIfDisposed();
        ValidateDelta(deltaX, deltaY);

        if (!_arduinoService.IsConnected)
            throw new InvalidOperationException("Arduino is not connected.");

        _logger.LogDebug("Simulating relative mouse move (Pixel: {DeltaX}, {DeltaY}) via Arduino", deltaX, deltaY);

        try
        {
            // Load calibration data and calculate HID deltas
            await RefreshCalibrationDataAsync();
            int hidDeltaX = CalculateHidDelta(deltaX, useYAxis: false);
            int hidDeltaY = CalculateHidDelta(deltaY, useYAxis: true);
            
            _logger.LogTrace("Calibrated HID Delta: ({HidDeltaX}, {HidDeltaY})", hidDeltaX, hidDeltaY);
            
            var command = new ArduinoMouseMoveRelativeCommand(hidDeltaX, hidDeltaY);
            await _arduinoService.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate relative mouse move ({DeltaX}, {DeltaY}) via Arduino", deltaX, deltaY);
            throw new InputSimulationException($"Failed to simulate relative mouse move via Arduino", ex);
        }
    }

    public async Task SimulateMouseMoveRelativeLowLevelAsync(int deltaX, int deltaY)
    {
        // For Arduino, low-level and regular relative move are the same
        await SimulateMouseMoveRelativeAsync(deltaX, deltaY);
    }

    public async Task SimulateMouseClickAsync(MouseButton button, ClickType type)
    {
        ThrowIfDisposed();
        ValidateButton(button);
        ValidateClickType(type);

        if (!_arduinoService.IsConnected)
            throw new InvalidOperationException("Arduino is not connected.");

        _logger.LogDebug("Simulating {ClickType} {Button} click via Arduino", type, button);

        try
        {
            var command = new ArduinoMouseClickCommand(button, type);
            await _arduinoService.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate {ClickType} {Button} click via Arduino", type, button);
            throw new InputSimulationException($"Failed to simulate mouse click via Arduino", ex);
        }
    }

    public async Task SimulateKeyboardInputAsync(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));

        ThrowIfDisposed();

        if (string.IsNullOrEmpty(text))
        {
            _logger.LogDebug("Skipping empty text input");
            return;
        }

        if (!_arduinoService.IsConnected)
            throw new InvalidOperationException("Arduino is not connected.");

        _logger.LogDebug("Simulating keyboard input: {Text} via Arduino", text);

        try
        {
            var command = new ArduinoKeyboardTextCommand(text);
            await _arduinoService.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate keyboard input via Arduino");
            throw new InputSimulationException($"Failed to simulate keyboard input via Arduino", ex);
        }
    }

    public async Task SimulateKeyPressAsync(VirtualKey key, bool isDown)
    {
        ThrowIfDisposed();
        ValidateKey(key);

        if (!_arduinoService.IsConnected)
            throw new InvalidOperationException("Arduino is not connected.");

        _logger.LogDebug("Simulating key {Key} {Action} via Arduino", key, isDown ? "down" : "up");

        try
        {
            var command = new ArduinoKeyPressCommand(key, isDown);
            await _arduinoService.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate key press via Arduino");
            throw new InputSimulationException($"Failed to simulate key press via Arduino", ex);
        }
    }

    public async Task SimulateKeyComboAsync(IEnumerable<VirtualKey> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        ThrowIfDisposed();

        var keyList = keys.ToList();
        if (keyList.Count == 0)
            throw new ArgumentException("Keys collection cannot be empty.", nameof(keys));

        if (!_arduinoService.IsConnected)
            throw new InvalidOperationException("Arduino is not connected.");

        _logger.LogDebug("Simulating key combo via Arduino");

        try
        {
            // Press all keys down
            foreach (var key in keyList)
            {
                await SimulateKeyPressAsync(key, true);
            }

            // Release all keys (in reverse order)
            for (int i = keyList.Count - 1; i >= 0; i--)
            {
                await SimulateKeyPressAsync(keyList[i], false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to simulate key combo via Arduino");
            throw new InputSimulationException($"Failed to simulate key combo via Arduino", ex);
        }
    }

    public async Task DelayAsync(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new ArgumentException("Duration cannot be negative.", nameof(duration));

        ThrowIfDisposed();

        if (duration == TimeSpan.Zero)
            return;

        if (!_arduinoService.IsConnected)
            throw new InvalidOperationException("Arduino is not connected.");

        var durationMs = (uint)Math.Min(duration.TotalMilliseconds, uint.MaxValue);

        _logger.LogTrace("Sending delay command {DurationMs}ms via Arduino", durationMs);

        try
        {
            var command = new ArduinoDelayCommand(durationMs);
            await _arduinoService.SendCommandAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send delay command via Arduino");
            throw new InputSimulationException($"Failed to send delay command via Arduino", ex);
        }
    }

    public async Task<Point> GetCursorPositionAsync()
    {
        ThrowIfDisposed();
        return await GetCursorPositionInternalAsync();
    }
    
    /// <summary>
    /// Gets the current cursor position using Win32 API.
    /// </summary>
    private Task<Point> GetCursorPositionInternalAsync()
    {
        return Task.Run(() =>
        {
            if (!GetCursorPos(out POINT point))
            {
                var error = GetLastError();
                _logger.LogError("Failed to get cursor position. Win32 error: {Error}", error);
                throw new InputSimulationException("Failed to get cursor position", (int)error);
            }

            var position = new Point(point.X, point.Y);
            _logger.LogTrace("Current cursor position: {Position}", position);
            return position;
        });
    }

    public Task<bool> IsReadyAsync()
    {
        ThrowIfDisposed();
        return Task.FromResult(_arduinoService.IsConnected);
    }

    private static void ValidatePosition(Point position)
    {
        if (position.X < 0 || position.Y < 0)
            throw new ArgumentException($"Position coordinates must be non-negative. Got: {position}");
    }

    private static void ValidateDelta(int deltaX, int deltaY)
    {
        // Allow any delta values (can be negative)
        // No validation needed
    }

    private static void ValidateButton(MouseButton button)
    {
        if (!Enum.IsDefined(typeof(MouseButton), button))
            throw new ArgumentException($"Invalid mouse button: {button}", nameof(button));
    }

    private static void ValidateClickType(ClickType type)
    {
        if (!Enum.IsDefined(typeof(ClickType), type))
            throw new ArgumentException($"Invalid click type: {type}", nameof(type));
    }

    private static void ValidateKey(VirtualKey key)
    {
        if (!Enum.IsDefined(typeof(VirtualKey), key))
            throw new ArgumentException($"Invalid virtual key: {key}", nameof(key));
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ArduinoInputSimulator));
    }

    #region Calibration Data Management

    /// <summary>
    /// Refreshes the calibration data from settings if it's stale or not loaded.
    /// </summary>
    private async Task RefreshCalibrationDataAsync()
    {
        // Check if we need to refresh the cached calibration data
        if (_calibrationData != null && DateTime.Now - _calibrationLoadedAt < CalibrationCacheExpiry)
        {
            return; // Use cached data
        }

        try
        {
            var settings = await _settingsService.LoadAsync();
            _calibrationData = settings.MouseCalibration;
            _calibrationLoadedAt = DateTime.Now;

            if (_calibrationData != null && _calibrationData.IsValid)
            {
                _logger.LogDebug("Loaded mouse calibration data: {Summary}", _calibrationData.GetSummary());
            }
            else
            {
                _logger.LogTrace("No valid mouse calibration data available, using 1:1 mapping");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load calibration data, using 1:1 mapping");
            _calibrationData = null;
        }
    }

    /// <summary>
    /// Calculates the HID delta needed to achieve the target pixel movement.
    /// Uses calibration data if available, otherwise assumes 1:1 mapping.
    /// </summary>
    /// <param name="targetPixelDelta">The target pixel movement</param>
    /// <param name="useYAxis">Whether to use Y-axis calibration data</param>
    /// <returns>The HID delta to send to Arduino</returns>
    private int CalculateHidDelta(int targetPixelDelta, bool useYAxis)
    {
        if (_calibrationData == null || !_calibrationData.IsValid)
        {
            // No calibration data, assume 1:1 mapping
            return targetPixelDelta;
        }

        return _calibrationData.CalculateHidDelta(targetPixelDelta, useYAxis);
    }

    /// <summary>
    /// Forces a refresh of the calibration data on next mouse move.
    /// Call this after calibration is completed.
    /// </summary>
    public void InvalidateCalibrationCache()
    {
        _calibrationData = null;
        _logger.LogDebug("Calibration cache invalidated");
    }

    #endregion

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        // ArduinoService is managed by DI, don't dispose it here
    }
}
