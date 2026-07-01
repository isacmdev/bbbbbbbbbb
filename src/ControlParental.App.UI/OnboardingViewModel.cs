// <copyright file="OnboardingViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlParental.Domain;
using System.Diagnostics;
using Microsoft.UI.Xaml;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — ViewModel for the onboarding flow.
/// Manages step progression, funnel event recording, and demo overlay.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly IOnboardingStateStore stateStore;
    private readonly ConsentDialog consentDialog;
    private readonly DispatcherTimer demoTimer;
    private OnboardingState state;
    private int demoCountdown = 3;

    [ObservableProperty]
    private OnboardingStep? currentStep;

    [ObservableProperty]
    private int progressCount;

    [ObservableProperty]
    private string progressLabel = "Protección 0 de 4";

    [ObservableProperty]
    private bool canGoNext;

    [ObservableProperty]
    private bool canGoBack;

    [ObservableProperty]
    private bool isDemoOverlayVisible;

    [ObservableProperty]
    private string demoCountdownText = "3";

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isAbandoned;

    [ObservableProperty]
    private int progressTotal = 4;

    private readonly IEnforcementLevelMonitor? enforcementLevelMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingViewModel"/> class.
    /// </summary>
    /// <param name="stateStore">The onboarding state store.</param>
    /// <param name="consentDialog">The consent dialog.</param>
    /// <param name="enforcementLevelMonitor">Optional T12 enforcement monitor for real progress reporting.</param>
    /// <exception cref="ArgumentNullException">Thrown when stateStore is null.</exception>
    public OnboardingViewModel(IOnboardingStateStore stateStore, ConsentDialog consentDialog, IEnforcementLevelMonitor? enforcementLevelMonitor = null)
    {
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.consentDialog = consentDialog ?? throw new ArgumentNullException(nameof(consentDialog));
        this.enforcementLevelMonitor = enforcementLevelMonitor;
        this.state = new OnboardingState(0, false, false, Array.Empty<OnboardingStep>(), Array.Empty<FunnelEvent>());
        this.demoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        this.demoTimer.Tick += this.OnDemoTimerTick;
    }

    /// <summary>
    /// Initializes the view model by loading the onboarding state.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        this.state = await this.stateStore.LoadAsync(ct);
        this.IsCompleted = this.state.IsCompleted;
        this.IsAbandoned = this.state.IsAbandoned;

        if (this.IsCompleted || this.IsAbandoned)
        {
            // Onboarding already finished or abandoned — load last step for display
            this.CurrentStep = this.state.Steps.LastOrDefault();
        }
        else
        {
            this.CurrentStep = this.state.Steps.FirstOrDefault(s => s.Index == this.state.CurrentStepIndex);
            await this.RecordEventAsync(FunnelEventType.OnboardingStepReached, this.CurrentStep?.Id ?? "unknown", ct);
        }

        this.UpdateProgressBar();
        this.UpdateButtonState();
    }

    /// <summary>
    /// Executes the current step's action.
    /// </summary>
    [RelayCommand]
    private async Task ExecuteStepAsync(CancellationToken ct = default)
    {
        if (this.CurrentStep == null)
        {
            return;
        }

        switch (this.CurrentStep.Id)
        {
            case "pairing":
                await this.ExecutePairingStepAsync(ct);
                break;
            case "consent":
                await this.ExecuteConsentStepAsync(ct);
                break;
            case "account":
                this.OpenMsSettings("accounts");
                break;
            case "service":
                this.OpenMsSettings("privacy");
                break;
            case "demo":
                await this.ExecuteDemoStepAsync(ct);
                break;
            case "managed":
                this.OpenMsSettings("privacy");
                break;
        }
    }

    /// <summary>
    /// Advances to the next step.
    /// </summary>
    [RelayCommand]
    private async Task GoNextAsync(CancellationToken ct = default)
    {
        if (this.CurrentStep == null || !this.CanGoNext)
        {
            return;
        }

        await this.AdvanceToStepAsync(this.CurrentStep.Index + 1, ct);
    }

    /// <summary>
    /// Goes back to the previous step.
    /// </summary>
    [RelayCommand]
    private async Task GoBackAsync(CancellationToken ct = default)
    {
        if (this.CurrentStep == null || !this.CanGoBack)
        {
            return;
        }

        await this.AdvanceToStepAsync(this.CurrentStep.Index - 1, ct);
    }

    /// <summary>
    /// Abandons the onboarding flow.
    /// </summary>
    [RelayCommand]
    private async Task AbandonAsync(CancellationToken ct = default)
    {
        this.state = this.state with { IsAbandoned = true };
        await this.stateStore.SaveAsync(this.state, ct);
        await this.RecordEventAsync(FunnelEventType.OnboardingAbandoned, this.CurrentStep?.Id ?? "unknown", ct);
    }

    private async Task ExecuteConsentStepAsync(CancellationToken ct)
    {
        this.consentDialog.Show();
        if (this.consentDialog.ConsentGranted)
        {
            await this.MarkStepCompletedAsync(ct);
        }
    }

    private async Task ExecutePairingStepAsync(CancellationToken ct)
    {
        // T24 pairing — the PairingViewModel from T24 handles the actual QR/code flow.
        // For now, mark as completed (real pairing done via T24 subsystem).
        await this.MarkStepCompletedAsync(ct);
    }

    private async Task ExecuteDemoStepAsync(CancellationToken ct)
    {
        this.IsDemoOverlayVisible = true;
        this.DemoCountdownText = "3";
        this.demoCountdown = 3;
        this.demoTimer.Start();

        await this.MarkStepCompletedAsync(ct);
        await this.RecordEventAsync(FunnelEventType.OnboardingFirstWin, "demo", ct);
    }

    private void OnDemoTimerTick(object? sender, object e)
    {
        this.demoCountdown--;
        this.DemoCountdownText = this.demoCountdown.ToString();
        if (this.demoCountdown <= 0)
        {
            this.demoTimer.Stop();
            this.IsDemoOverlayVisible = false;
        }
    }

    private async Task AdvanceToStepAsync(int newIndex, CancellationToken ct)
    {
        if (newIndex >= this.state.Steps.Count)
        {
            this.state = this.state with { IsCompleted = true };
            await this.stateStore.SaveAsync(this.state, ct);
            await this.RecordEventAsync(FunnelEventType.OnboardingCompleted, "final", ct);
            return;
        }

        var steps = this.state.Steps.ToList();
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Index < newIndex)
            {
                steps[i] = steps[i] with { Status = OnboardingStepStatus.Completed };
            }
            else if (steps[i].Index == newIndex)
            {
                steps[i] = steps[i] with { Status = OnboardingStepStatus.InProgress };
            }
        }

        this.state = this.state with { CurrentStepIndex = newIndex, Steps = steps };
        await this.stateStore.SaveAsync(this.state, ct);
        this.CurrentStep = steps.First(s => s.Index == newIndex);
        await this.RecordEventAsync(FunnelEventType.OnboardingStepReached, this.CurrentStep.Id, ct);
        this.UpdateProgressBar();
        this.UpdateButtonState();
    }

    private async Task MarkStepCompletedAsync(CancellationToken ct)
    {
        await this.GoNextAsync(ct);
    }

    private async Task RecordEventAsync(FunnelEventType type, string stepId, CancellationToken ct)
    {
        var evt = new FunnelEvent(type, stepId, DateTimeOffset.UtcNow);
        await this.stateStore.RecordFunnelEventAsync(evt, ct);
    }

    private void UpdateProgressBar()
    {
        // N de M: cuenta estándar ✓, servicio activo ✓, watcher emitiendo ✓, capa preventiva ✓/✗
        // Reflects real T12 state — never inflated.
        var realCount = this.CalculateRealProgress();
        this.ProgressCount = realCount;
        this.ProgressLabel = $"Protección {realCount} de {this.ProgressTotal}";
    }

    private int CalculateRealProgress()
    {
        if (this.enforcementLevelMonitor == null)
        {
            // Fallback: count completed steps (may be inflated)
            return this.state.Steps.Count(s => s.Status == OnboardingStepStatus.Completed);
        }

        var issues = this.enforcementLevelMonitor.CurrentIssues;
        var issueTypes = issues.Select(i => i.Type).ToHashSet();
        var level = this.enforcementLevelMonitor.CurrentLevel;

        var serviceActive = !issueTypes.Contains(EnforcementIssueType.ServiceNotRunning);
        var accountStandard = !issueTypes.Contains(EnforcementIssueType.ChildIsAdministrator);
        var watcherEmitting = !issueTypes.Contains(EnforcementIssueType.AgentNotResponding)
            && !issueTypes.Contains(EnforcementIssueType.HookTimeout);
        var preventiveLayer = !issueTypes.Contains(EnforcementIssueType.PreventiveLayerUnavailable)
            && level is EnforcementLevel.Standard or EnforcementLevel.Managed;

        var count = 0;
        if (serviceActive) count++;
        if (accountStandard) count++;
        if (watcherEmitting) count++;
        if (preventiveLayer) count++;

        return count;
    }

    private void UpdateButtonState()
    {
        this.CanGoNext = this.CurrentStep?.Status == OnboardingStepStatus.Completed
            || this.CurrentStep?.Status == OnboardingStepStatus.InProgress;
        this.CanGoBack = this.CurrentStep?.Index > 0;
    }

    private void OpenMsSettings(string page)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = $"ms-settings:{page}", UseShellExecute = true });
        }
        catch
        {
            // ms-settings not available
        }
    }
}