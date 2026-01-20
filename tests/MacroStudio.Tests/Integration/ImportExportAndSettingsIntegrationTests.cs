using MacroStudio.Application.Services;
using MacroStudio.Domain.Entities;
using MacroStudio.Domain.Interfaces;
using MacroStudio.Domain.ValueObjects;
using MacroStudio.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MacroStudio.Tests.Integration;

public class ImportExportAndSettingsIntegrationTests
{
    [Fact]
    public async Task Settings_LoadSave_RoundTrip_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroStudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var settingsPath = Path.Combine(tempDir, "settings.json");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISettingsService>(sp =>
            new JsonSettingsService(sp.GetRequiredService<ILogger<JsonSettingsService>>(), settingsPath));

        var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<ISettingsService>();

        var s = new AppSettings { DefaultSpeedMultiplier = 1.5, ShowCountdown = false, CountdownSeconds = 0.0 };
        await settings.SaveAsync(s);

        var loaded = await settings.LoadAsync();
        Assert.Equal(1.5, loaded.DefaultSpeedMultiplier);
        Assert.False(loaded.ShowCountdown);
    }

    [Fact]
    public async Task ScriptManager_ExportThenImport_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MacroStudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var storageDir = Path.Combine(tempDir, "scripts");
        var exportPath = Path.Combine(tempDir, "export.json");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileStorageService>(sp =>
            new JsonFileStorageService(sp.GetRequiredService<ILogger<JsonFileStorageService>>(), storageDir));
        // ScriptManager depends on IGlobalHotkeyService even though this test doesn't exercise hotkeys.
        services.AddSingleton<IGlobalHotkeyService, NoopGlobalHotkeyService>();
        services.AddSingleton<IScriptManager, ScriptManager>();

        var provider = services.BuildServiceProvider();
        var mgr = provider.GetRequiredService<IScriptManager>();

        var script = await mgr.CreateScriptAsync("ExportMe");
        script.AddCommand(new SleepCommand(TimeSpan.FromMilliseconds(10)));
        await mgr.UpdateScriptAsync(script);

        await mgr.ExportScriptAsync(script.Id, exportPath);
        Assert.True(File.Exists(exportPath));

        var imported = await mgr.ImportScriptAsync(exportPath);
        Assert.NotNull(imported);
        Assert.True(imported.CommandCount >= 1);
    }

    private sealed class NoopGlobalHotkeyService : IGlobalHotkeyService
    {
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        public Task RegisterHotkeyAsync(HotkeyDefinition hotkey) => Task.CompletedTask;
        public Task UnregisterHotkeyAsync(HotkeyDefinition hotkey) => Task.CompletedTask;
        public Task UnregisterAllHotkeysAsync() => Task.CompletedTask;
        public Task<IEnumerable<HotkeyDefinition>> GetRegisteredHotkeysAsync() => Task.FromResult<IEnumerable<HotkeyDefinition>>(Array.Empty<HotkeyDefinition>());
        public Task<bool> IsHotkeyRegisteredAsync(HotkeyDefinition hotkey) => Task.FromResult(false);
        public Task<bool> IsReadyAsync() => Task.FromResult(true);

        // Helper for completeness if any future test wants to simulate presses.
        public void Raise(HotkeyDefinition hotkey) => HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkey, DateTime.Now));
    }
}

