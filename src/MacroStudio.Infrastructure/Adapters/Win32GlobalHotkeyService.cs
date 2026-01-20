using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Infrastructure.Win32;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static MacroStudio.Infrastructure.Win32.Win32Api;
using static MacroStudio.Infrastructure.Win32.Win32Structures;

namespace MacroStudio.Infrastructure.Adapters;

/// <summary>
/// Win32-based implementation of global hotkey service using RegisterHotKey/UnregisterHotKey APIs.
/// Provides global hotkey registration, unregistration, and event handling with conflict detection.
/// </summary>
public class Win32GlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private readonly ILogger<Win32GlobalHotkeyService> _logger;
    private readonly IWin32HotkeyApi _api;
    private readonly object _lockObject = new();
    private readonly ConcurrentDictionary<int, HotkeyDefinition> _registeredHotkeys = new();
    private readonly ConcurrentDictionary<HotkeyDefinition, int> _hotkeyIds = new();
    private readonly ConcurrentDictionary<int, HotkeyRegistrationInfo> _pendingRegistrations = new();
    
    private Thread? _messageLoopThread;
    private volatile bool _isRunning = false;
    private volatile bool _isDisposed = false;
    private int _nextHotkeyId = 1;
    private uint _threadId;

    private class HotkeyRegistrationInfo
    {
        public int HotkeyId { get; set; }
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }
        public HotkeyDefinition Hotkey { get; set; } = null!;
    }

    /// <inheritdoc />
    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    /// <summary>
    /// Initializes a new instance of the Win32GlobalHotkeyService class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public Win32GlobalHotkeyService(ILogger<Win32GlobalHotkeyService> logger)
        : this(logger, new Win32HotkeyApi())
    {
    }

    internal Win32GlobalHotkeyService(ILogger<Win32GlobalHotkeyService> logger, IWin32HotkeyApi api)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger.LogDebug("Win32GlobalHotkeyService initialized");
        
        StartMessageLoop();
    }

    /// <inheritdoc />
    public async Task RegisterHotkeyAsync(HotkeyDefinition hotkey)
    {
        if (hotkey == null)
            throw new ArgumentNullException(nameof(hotkey));

        ThrowIfDisposed();

        if (!hotkey.IsValid())
        {
            throw new HotkeyRegistrationException($"Invalid hotkey definition: {hotkey}", hotkey);
        }

        _logger.LogDebug("Registering hotkey: {Hotkey}", hotkey);
        
        // Wait for message loop thread to be ready
        var maxWaitTime = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;
        while (_threadId == 0 && DateTime.UtcNow - startTime < maxWaitTime)
        {
            await Task.Delay(10);
        }

        if (_threadId == 0)
        {
            throw new HotkeyRegistrationException(
                "Message loop thread is not ready",
                hotkey);
        }

        int hotkeyId;
        HotkeyRegistrationInfo registrationInfo;

        lock (_lockObject)
        {
            // Check if hotkey is already registered
            if (_hotkeyIds.ContainsKey(hotkey))
            {
                _logger.LogWarning("Hotkey {Hotkey} is already registered", hotkey);
                return;
            }

            // Check for conflicts with existing hotkeys
            var conflictingHotkey = _registeredHotkeys.Values
                .FirstOrDefault(h => h.Modifiers == hotkey.Modifiers && h.Key == hotkey.Key);

            if (conflictingHotkey != null)
            {
                throw new HotkeyRegistrationException(
                    $"Hotkey conflict: {hotkey} conflicts with existing hotkey {conflictingHotkey}",
                    hotkey);
            }

            hotkeyId = GetNextHotkeyId();
            var modifiers = hotkey.Modifiers.ToWin32Modifiers() | MOD_NOREPEAT;
            var virtualKey = (uint)hotkey.Key;

            registrationInfo = new HotkeyRegistrationInfo
            {
                HotkeyId = hotkeyId,
                Modifiers = modifiers,
                VirtualKey = virtualKey,
                Hotkey = hotkey
            };

            // Store pending registration before posting message
            _pendingRegistrations[hotkeyId] = registrationInfo;

            // Post a custom message to the message loop thread to register the hotkey
            if (!_api.PostThreadMessage(_threadId, WM_HOTKEY_REGISTER, (IntPtr)hotkeyId, IntPtr.Zero))
            {
                _pendingRegistrations.TryRemove(hotkeyId, out _);
                var error = _api.GetLastError();
                _logger.LogError("Failed to post registration message for hotkey {Hotkey}. Win32 error: {Error}", hotkey, error);
                throw new HotkeyRegistrationException(
                    $"Failed to register hotkey {hotkey}: Could not post registration message",
                    hotkey,
                    (int)error);
            }
        }

        // Wait for registration to complete (with timeout)
        var registrationTimeout = TimeSpan.FromSeconds(2);
        var registrationStart = DateTime.UtcNow;
        while (!_registeredHotkeys.ContainsKey(hotkeyId) && 
               DateTime.UtcNow - registrationStart < registrationTimeout)
        {
            await Task.Delay(10);
        }

        if (!_registeredHotkeys.ContainsKey(hotkeyId))
        {
            lock (_lockObject)
            {
                _pendingRegistrations.TryRemove(hotkeyId, out _);
            }
            throw new HotkeyRegistrationException(
                "Hotkey registration timed out",
                hotkey);
        }

        _logger.LogDebug("Successfully registered hotkey {Hotkey} with ID {HotkeyId}", hotkey, hotkeyId);
    }

    /// <inheritdoc />
    public async Task UnregisterHotkeyAsync(HotkeyDefinition hotkey)
    {
        if (hotkey == null)
            throw new ArgumentNullException(nameof(hotkey));

        ThrowIfDisposed();

        _logger.LogDebug("Unregistering hotkey: {Hotkey}", hotkey);
        lock (_lockObject)
        {
            UnregisterHotkeyInternal_NoLock(hotkey);
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task UnregisterAllHotkeysAsync()
    {
        ThrowIfDisposed();

        _logger.LogDebug("Unregistering all hotkeys");
        List<HotkeyDefinition> hotkeysToUnregister;
        lock (_lockObject)
        {
            hotkeysToUnregister = _registeredHotkeys.Values.ToList();
        }

        var errors = new List<Exception>();
        foreach (var hotkey in hotkeysToUnregister)
        {
            try
            {
                await UnregisterHotkeyAsync(hotkey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unregister hotkey {Hotkey} during bulk unregistration", hotkey);
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("One or more hotkeys failed to unregister", errors);
        }

        _logger.LogDebug("Successfully unregistered all hotkeys");
    }

    /// <inheritdoc />
    public async Task<IEnumerable<HotkeyDefinition>> GetRegisteredHotkeysAsync()
    {
        ThrowIfDisposed();

        return await Task.Run(() =>
        {
            lock (_lockObject)
            {
                return _registeredHotkeys.Values.ToList();
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> IsHotkeyRegisteredAsync(HotkeyDefinition hotkey)
    {
        if (hotkey == null)
            throw new ArgumentNullException(nameof(hotkey));

        ThrowIfDisposed();

        return await Task.Run(() =>
        {
            lock (_lockObject)
            {
                return _hotkeyIds.ContainsKey(hotkey);
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync()
    {
        if (_isDisposed)
            return false;

        return await Task.Run(() =>
        {
            return _isRunning && _messageLoopThread != null && _messageLoopThread.IsAlive;
        });
    }

    /// <summary>
    /// Starts the message loop thread for handling hotkey messages.
    /// </summary>
    private void StartMessageLoop()
    {
        if (_messageLoopThread != null && _messageLoopThread.IsAlive)
        {
            _logger.LogWarning("Message loop thread is already running");
            return;
        }

        _isRunning = true;
        _messageLoopThread = new Thread(MessageLoopWorker)
        {
            Name = "HotkeyMessageLoop",
            IsBackground = true
        };

        _messageLoopThread.Start();
        _logger.LogDebug("Started hotkey message loop thread");
    }

    /// <summary>
    /// Stops the message loop thread.
    /// </summary>
    private void StopMessageLoop()
    {
        if (!_isRunning || _messageLoopThread == null)
            return;

        _logger.LogDebug("Stopping hotkey message loop thread");
        _isRunning = false;

        // Post a quit message to the message loop thread
        if (_threadId != 0)
        {
            _api.PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        // Wait for the thread to finish
        if (_messageLoopThread.IsAlive)
        {
            _messageLoopThread.Join(TimeSpan.FromSeconds(5));
            
            if (_messageLoopThread.IsAlive)
            {
                _logger.LogWarning("Message loop thread did not stop gracefully");
            }
        }

        _messageLoopThread = null;
        _logger.LogDebug("Stopped hotkey message loop thread");
    }

    /// <summary>
    /// Worker method for the message loop thread.
    /// </summary>
    private void MessageLoopWorker()
    {
        try
        {
            _threadId = _api.GetCurrentThreadId();
            _logger.LogDebug("Message loop thread started with ID {ThreadId}", _threadId);

            while (_isRunning)
            {
                // Check for messages without blocking
                if (_api.PeekMessage(out MSG msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                {
                    if (msg.message == WM_QUIT)
                    {
                        _logger.LogDebug("Received WM_QUIT message, stopping message loop");
                        break;
                    }

                    if (msg.message == WM_HOTKEY)
                    {
                        HandleHotkeyMessage(msg);
                    }
                    else if (msg.message == WM_HOTKEY_REGISTER)
                    {
                        HandleHotkeyRegistrationMessage(msg);
                    }
                    else
                    {
                        // Process other messages normally
                        _api.TranslateMessage(ref msg);
                        _api.DispatchMessage(ref msg);
                    }
                }
                else
                {
                    // No messages available, sleep briefly to avoid busy waiting
                    Thread.Sleep(10);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in hotkey message loop");
        }
        finally
        {
            _logger.LogDebug("Message loop thread exiting");
        }
    }

    /// <summary>
    /// Handles WM_HOTKEY_REGISTER messages to register hotkeys on the message loop thread.
    /// </summary>
    /// <param name="msg">The registration message.</param>
    private void HandleHotkeyRegistrationMessage(MSG msg)
    {
        try
        {
            var hotkeyId = (int)msg.wParam;
            
            if (_pendingRegistrations.TryRemove(hotkeyId, out var registrationInfo))
            {
                // Register the hotkey with Win32 on this thread
                if (!_api.RegisterHotKey(IntPtr.Zero, registrationInfo.HotkeyId, registrationInfo.Modifiers, registrationInfo.VirtualKey))
                {
                    var error = _api.GetLastError();
                    _logger.LogError("Failed to register hotkey {Hotkey} on message loop thread. Win32 error: {Error}", registrationInfo.Hotkey, error);

                    var errorMessage = error switch
                    {
                        ERROR_HOTKEY_ALREADY_REGISTERED => "Hotkey is already registered by another application",
                        _ => $"Win32 error {error}"
                    };

                    _logger.LogError("Hotkey registration failed: {ErrorMessage}", errorMessage);
                    return;
                }

                // Store the registration
                lock (_lockObject)
                {
                    _registeredHotkeys[registrationInfo.HotkeyId] = registrationInfo.Hotkey;
                    _hotkeyIds[registrationInfo.Hotkey] = registrationInfo.HotkeyId;
                }

                _logger.LogDebug("Successfully registered hotkey {Hotkey} with ID {HotkeyId} on message loop thread", registrationInfo.Hotkey, registrationInfo.HotkeyId);
            }
            else
            {
                _logger.LogWarning("Received registration message for unknown hotkey ID {HotkeyId}", hotkeyId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling hotkey registration message");
        }
    }

    /// <summary>
    /// Handles WM_HOTKEY messages.
    /// </summary>
    /// <param name="msg">The hotkey message.</param>
    private void HandleHotkeyMessage(MSG msg)
    {
        try
        {
            var hotkeyId = (int)msg.wParam;
            
            if (_registeredHotkeys.TryGetValue(hotkeyId, out var hotkey))
            {
                _logger.LogTrace("Hotkey pressed: {Hotkey}", hotkey);
                
                var eventArgs = new HotkeyPressedEventArgs(hotkey, DateTime.Now);
                
                // Raise the event on a background thread to avoid blocking the message loop
                Task.Run(() =>
                {
                    try
                    {
                        HotkeyPressed?.Invoke(this, eventArgs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in hotkey event handler for {Hotkey}", hotkey);
                    }
                });
            }
            else
            {
                _logger.LogWarning("Received hotkey message for unknown hotkey ID {HotkeyId}", hotkeyId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling hotkey message");
        }
    }

    /// <summary>
    /// Gets the next available hotkey ID.
    /// </summary>
    /// <returns>A unique hotkey ID.</returns>
    private int GetNextHotkeyId()
    {
        return Interlocked.Increment(ref _nextHotkeyId);
    }

    private void UnregisterHotkeyInternal_NoLock(HotkeyDefinition hotkey)
    {
        if (!_hotkeyIds.TryGetValue(hotkey, out var hotkeyId))
        {
            _logger.LogWarning("Hotkey {Hotkey} is not registered", hotkey);
            return;
        }

        // Unregister the hotkey with Win32
        if (!_api.UnregisterHotKey(IntPtr.Zero, hotkeyId))
        {
            var error = _api.GetLastError();
            _logger.LogError("Failed to unregister hotkey {Hotkey}. Win32 error: {Error}", hotkey, error);

            var errorMessage = error switch
            {
                ERROR_HOTKEY_NOT_REGISTERED => "Hotkey is not registered",
                _ => $"Win32 error {error}"
            };

            throw new HotkeyRegistrationException(
                $"Failed to unregister hotkey {hotkey}: {errorMessage}",
                hotkey,
                (int)error);
        }

        // Remove from tracking collections
        _registeredHotkeys.TryRemove(hotkeyId, out _);
        _hotkeyIds.TryRemove(hotkey, out _);

        _logger.LogDebug("Successfully unregistered hotkey {Hotkey} with ID {HotkeyId}", hotkey, hotkeyId);
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(Win32GlobalHotkeyService));
        }
    }

    /// <summary>
    /// Disposes the global hotkey service and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _logger.LogDebug("Disposing Win32GlobalHotkeyService");

        try
        {
            // Unregister all hotkeys
            UnregisterAllHotkeysAsync().Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering hotkeys during disposal");
        }

        // Stop the message loop
        StopMessageLoop();

        _isDisposed = true;
        _logger.LogDebug("Win32GlobalHotkeyService disposed");
    }
}