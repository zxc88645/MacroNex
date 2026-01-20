using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;

namespace MacroStudio.Presentation.ViewModels;

/// <summary>
/// Main ViewModel coordinating application state, script selection, and high-level commands.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IScriptManager _scriptManager;
    private readonly IRecordingService _recordingService;
    private readonly IExecutionService _executionService;
    private readonly ILoggingService _loggingService;
    private readonly ISafetyService _safetyService;
    private readonly IGlobalHotkeyService _hotkeyService;

    public ScriptListViewModel ScriptList { get; }
    public CommandGridViewModel CommandGrid { get; }
    public ExecutionControlViewModel ExecutionControls { get; }
    public LoggingViewModel Logging { get; }

    [ObservableProperty]
    private Script? selectedScript;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool isKillSwitchActive;

    private bool _isHotkeyCaptureActive = false;

    public MainViewModel(
        IScriptManager scriptManager,
        IRecordingService recordingService,
        IExecutionService executionService,
        ILoggingService loggingService,
        ISafetyService safetyService,
        IGlobalHotkeyService hotkeyService,
        ScriptListViewModel scriptListViewModel,
        CommandGridViewModel commandGridViewModel,
        ExecutionControlViewModel executionControlViewModel,
        LoggingViewModel loggingViewModel)
    {
        _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
        _safetyService = safetyService ?? throw new ArgumentNullException(nameof(safetyService));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));

        ScriptList = scriptListViewModel ?? throw new ArgumentNullException(nameof(scriptListViewModel));
        CommandGrid = commandGridViewModel ?? throw new ArgumentNullException(nameof(commandGridViewModel));
        ExecutionControls = executionControlViewModel ?? throw new ArgumentNullException(nameof(executionControlViewModel));
        Logging = loggingViewModel ?? throw new ArgumentNullException(nameof(loggingViewModel));

        ScriptList.SelectedScriptChanged += (_, script) =>
        {
            SelectedScript = script;
            CommandGrid.LoadScript(script);
            ExecutionControls.SetScript(script);
        };

        IsKillSwitchActive = _safetyService.IsKillSwitchActive;
        _safetyService.KillSwitchActivated += (_, args) =>
        {
            IsKillSwitchActive = true;
            StatusText = $"KILL SWITCH: {args.Reason}";
        };

        // Listen for hotkey presses to trigger scripts
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Auto-initialize scripts on first load so existing scripts are scanned.
        _ = InitializeAsync();
    }

    private async void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        // Don't trigger scripts when capturing hotkeys
        if (_isHotkeyCaptureActive)
            return;

        // Fire and forget - don't await anything to avoid delays
        _ = Task.Run(async () =>
        {
            try
            {
                // Find script with matching hotkey
                var scripts = await _scriptManager.GetAllScriptsAsync();
                var script = scripts.FirstOrDefault(s => s.TriggerHotkey != null && 
                    s.TriggerHotkey.Modifiers == e.Hotkey.Modifiers && 
                    s.TriggerHotkey.Key == e.Hotkey.Key);

                if (script != null && script.TriggerHotkey != null)
                {
                    // Prepare execution options
                    var options = ExecutionOptions.Default();
                    options.ShowCountdown = false;
                    options.CountdownDuration = TimeSpan.Zero;

                    // For "RepeatWhileHeld" mode, check if script is already executing
                    // If it is, ignore the trigger to avoid concurrent executions
                    if (script.TriggerHotkey.TriggerMode == HotkeyTriggerMode.RepeatWhileHeld)
                    {
                        try
                        {
                            // Try to start execution - if it's already running, this will throw
                            // We catch and ignore it for repeat mode to allow continuous triggering
                            await _executionService.StartExecutionAsync(script, options);
                        }
                        catch (InvalidOperationException)
                        {
                            // Script is already executing, ignore this trigger
                            // This allows Windows keyboard repeat to trigger multiple times
                            // but prevents concurrent executions of the same script
                            return;
                        }
                    }
                    else
                    {
                        // For "Once" mode, always try to execute
                        // If already executing, it will throw and be logged below
                        await _executionService.StartExecutionAsync(script, options);
                    }

                    // Log asynchronously (fire and forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _loggingService.LogInfoAsync("Script triggered by hotkey", new Dictionary<string, object>
                            {
                                { "ScriptId", script.Id },
                                { "ScriptName", script.Name },
                                { "Hotkey", e.Hotkey.GetDisplayString() },
                                { "TriggerMode", script.TriggerHotkey.TriggerMode.ToString() }
                            });
                        }
                        catch { /* Ignore logging errors */ }
                    });

                    // Update UI on UI thread
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Executing: {script.Name}";
                    });
                }
                else
                {
                    // No script found for this hotkey - this might be a leftover hotkey from a deleted script
                    // or a Windows message queue delay (WM_HOTKEY message processed after script deletion)
                    // Try to unregister it to prevent it from intercepting keyboard input
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // First check if the hotkey is actually registered
                            var isRegistered = await _hotkeyService.IsHotkeyRegisteredAsync(e.Hotkey);
                            
                            if (!isRegistered)
                            {
                                // Hotkey is not registered - this is normal if:
                                // 1. Script was deleted and hotkey was already unregistered
                                // 2. Windows message queue had a delayed WM_HOTKEY message
                                // Don't log this as it's expected behavior in these cases
                                return;
                            }

                            // Hotkey is still registered but no script found - this is an orphaned hotkey
                            await _loggingService.LogWarningAsync("No script found for hotkey, attempting to unregister orphaned hotkey", new Dictionary<string, object>
                            {
                                { "Hotkey", e.Hotkey.GetDisplayString() },
                                { "Modifiers", e.Hotkey.Modifiers.ToString() },
                                { "Key", e.Hotkey.Key.ToString() }
                            });

                            // Try to unregister the orphaned hotkey
                            try
                            {
                                await _hotkeyService.UnregisterHotkeyAsync(e.Hotkey);
                                await _loggingService.LogInfoAsync("Successfully unregistered orphaned hotkey", new Dictionary<string, object>
                                {
                                    { "Hotkey", e.Hotkey.GetDisplayString() }
                                });
                            }
                            catch (HotkeyRegistrationException hre) when (hre.Message.Contains("not registered"))
                            {
                                // Hotkey was not registered (might have been unregistered between check and unregister)
                                // This can happen due to race conditions - not an error
                                // Don't log this as it's expected in race condition scenarios
                            }
                            catch (Exception unregisterEx)
                            {
                                await _loggingService.LogErrorAsync("Failed to unregister orphaned hotkey", unregisterEx, new Dictionary<string, object>
                                {
                                    { "Hotkey", e.Hotkey.GetDisplayString() },
                                    { "ExceptionType", unregisterEx.GetType().Name },
                                    { "ExceptionMessage", unregisterEx.Message }
                                });
                            }
                        }
                        catch { /* Ignore logging errors */ }
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error asynchronously
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _loggingService.LogErrorAsync("Error handling hotkey trigger", ex, new Dictionary<string, object>
                        {
                            { "Hotkey", e.Hotkey.GetDisplayString() }
                        });
                    }
                    catch { /* Ignore logging errors */ }
                });
            }
        });
    }

    /// <summary>
    /// Sets whether hotkey capture is active (to prevent triggering scripts while setting hotkeys).
    /// </summary>
    public void SetHotkeyCaptureActive(bool isActive)
    {
        _isHotkeyCaptureActive = isActive;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        await ScriptList.RefreshAsync();
        
        // Clean up orphaned hotkeys (hotkeys without corresponding scripts)
        // This handles cases where scripts were deleted but hotkeys weren't properly unregistered
        try
        {
            var allRegisteredHotkeys = await _hotkeyService.GetRegisteredHotkeysAsync();
            var scripts = await _scriptManager.GetAllScriptsAsync();
            var scriptHotkeys = scripts
                .Where(s => s.TriggerHotkey != null)
                .Select(s => new { s.TriggerHotkey!.Modifiers, s.TriggerHotkey.Key, s.TriggerHotkey.TriggerMode })
                .ToList();

            foreach (var registeredHotkey in allRegisteredHotkeys)
            {
                var hasMatchingScript = scriptHotkeys.Any(sh =>
                    sh.Modifiers == registeredHotkey.Modifiers &&
                    sh.Key == registeredHotkey.Key &&
                    sh.TriggerMode == registeredHotkey.TriggerMode);

                if (!hasMatchingScript)
                {
                    // Orphaned hotkey - unregister it
                    try
                    {
                        await _hotkeyService.UnregisterHotkeyAsync(registeredHotkey);
                        await _loggingService.LogInfoAsync("Cleaned up orphaned hotkey", new Dictionary<string, object>
                        {
                            { "Hotkey", registeredHotkey.GetDisplayString() }
                        });
                    }
                    catch (Exception ex)
                    {
                        await _loggingService.LogErrorAsync("Failed to cleanup orphaned hotkey", ex, new Dictionary<string, object>
                        {
                            { "Hotkey", registeredHotkey.GetDisplayString() }
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await _loggingService.LogErrorAsync("Error during hotkey cleanup", ex);
        }
        
        // Register all script hotkeys through ScriptManager to ensure proper tracking
        // This ensures hotkeys are tracked and can be properly unregistered when scripts are deleted
        await _scriptManager.RegisterAllScriptHotkeysAsync();
        
        StatusText = "Loaded scripts";
    }

    [RelayCommand(CanExecute = nameof(CanStartExecution))]
    private async Task StartExecutionAsync()
    {
        if (SelectedScript == null) return;

        await _loggingService.LogInfoAsync("Execution started", new Dictionary<string, object>
        {
            { "ScriptId", SelectedScript.Id },
            { "ScriptName", SelectedScript.Name }
        });

        await _executionService.StartExecutionAsync(SelectedScript);
        StatusText = "Executing";
    }

    private bool CanStartExecution() => SelectedScript != null;

    partial void OnSelectedScriptChanged(Script? oldValue, Script? newValue)
    {
        StartExecutionCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanResetKillSwitch))]
    private async Task ResetKillSwitchAsync()
    {
        await _safetyService.DeactivateKillSwitchAsync();
        IsKillSwitchActive = _safetyService.IsKillSwitchActive;
        StatusText = "Kill switch reset";
    }

    private bool CanResetKillSwitch() => IsKillSwitchActive;

    partial void OnIsKillSwitchActiveChanged(bool oldValue, bool newValue)
    {
        ResetKillSwitchCommand.NotifyCanExecuteChanged();
    }
}

