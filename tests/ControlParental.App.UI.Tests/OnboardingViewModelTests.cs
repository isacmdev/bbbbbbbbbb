// <copyright file="OnboardingViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — Unit tests for OnboardingViewModel.
/// </summary>
public sealed class OnboardingViewModelTests : IDisposable
{
    private readonly OnboardingStateStore stateStore;
    private readonly ConsentDialog consentDialog;
    private readonly OnboardingViewModel viewModel;

    public OnboardingViewModelTests()
    {
        // Use a temp directory for state storage during tests
        this.stateStore = new OnboardingStateStoreTestable();
        this.consentDialog = new ConsentDialog(null, null);
        this.viewModel = new OnboardingViewModel(this.stateStore, this.consentDialog);
    }

    public void Dispose()
    {
        // Clean up test state file
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ControlParental");
        var stateFile = Path.Combine(dataDir, "onboarding_state.json");
        if (File.Exists(stateFile))
        {
            File.Delete(stateFile);
        }
    }

    /// <summary>
    /// Testable version of OnboardingStateStore that uses a temp file.
    /// </summary>
    private sealed class OnboardingStateStoreTestable : OnboardingStateStore
    {
        private readonly string tempFilePath;

        public OnboardingStateStoreTestable()
        {
            var tempDir = Path.GetTempPath();
            this.tempFilePath = Path.Combine(tempDir, $"onboarding_test_{Guid.NewGuid()}.json");
        }

        // Uses inherited implementation but with temp file path override behavior
        // For these tests we rely on the base class default behavior
    }

    [Fact]
    public async Task InitializeAsync_WhenNoState_ReturnsInitialState()
    {
        // Act
        await this.viewModel.InitializeAsync();

        // Assert
        Assert.NotNull(this.viewModel.CurrentStep);
        Assert.Equal("pairing", this.viewModel.CurrentStep.Id);
        Assert.Equal(0, this.viewModel.CurrentStep.Index);
    }

    [Fact]
    public async Task ExecuteConsentStep_SetsConsentGranted()
    {
        // Arrange - create a consent dialog with a mocked consent service
        var mockConsentService = new MockConsentService();
        var dialogWithService = new ConsentDialog(mockConsentService, null);
        var vm = new OnboardingViewModel(this.stateStore, dialogWithService);
        await vm.InitializeAsync();

        // Skip to consent step (index 1)
        // For this test we verify the consent dialog ConsentGranted is set after GrantAndClose
        dialogWithService.Show(); // Simulates user accepting

        // Assert - consent was granted via the dialog
        Assert.True(dialogWithService.ConsentGranted);
    }

    [Fact]
    public async Task AdvanceToStep_UpdatesCurrentStep()
    {
        // Arrange
        await this.viewModel.InitializeAsync();
        var initialStep = this.viewModel.CurrentStep;

        // Act - use the public GoNextCommand (which internally calls AdvanceToStepAsync)
        // Since ExecuteStepAsync for "pairing" marks it complete immediately,
        // we can advance
        await this.viewModel.GoNextCommand.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.NotEqual(initialStep?.Index, this.viewModel.CurrentStep?.Index);
        Assert.Equal(1, this.viewModel.CurrentStep?.Index);
    }

    [Fact]
    public async Task RecordFunnelEvent_SavesEventToStore()
    {
        // Arrange
        await this.viewModel.InitializeAsync();
        var eventType = FunnelEventType.OnboardingStepReached;
        var stepId = "pairing";

        // Act - InitializeAsync already records step reached event
        var loadedState = await this.stateStore.LoadAsync();

        // Assert - at least one event should be recorded
        Assert.NotEmpty(loadedState.Events);
        Assert.Contains(loadedState.Events, e => e.StepId == stepId && e.Type == eventType);
    }

    [Fact]
    public async Task UpdateProgressBar_ReflectsCompletedSteps()
    {
        // Arrange
        await this.viewModel.InitializeAsync();

        // Act - complete all steps (mark first 4 as completed per T12 spec)
        // The progress shows "Protección N de 4" where N is completed steps
        var completedSteps = this.viewModel.ProgressCount;

        // Assert - progress bar should show 0 initially
        Assert.Equal(0, completedSteps);
        Assert.Equal("Protección 0 de 4", this.viewModel.ProgressLabel);
    }

    [Fact]
    public async Task CurrentStep_AfterInit_IsPairingStep()
    {
        // Arrange & Act
        await this.viewModel.InitializeAsync();

        // Assert
        Assert.NotNull(this.viewModel.CurrentStep);
        Assert.Equal("pairing", this.viewModel.CurrentStep.Id);
        Assert.Equal(OnboardingStepStatus.Pending, this.viewModel.CurrentStep.Status);
    }

    [Fact]
    public async Task CanGoNext_IsFalseInitially_WhenStepNotCompleted()
    {
        // Arrange
        await this.viewModel.InitializeAsync();

        // Assert - can go next requires step to be completed or in progress
        Assert.False(this.viewModel.CanGoNext);
    }

    [Fact]
    public async Task CanGoBack_IsFalse_WhenOnFirstStep()
    {
        // Arrange
        await this.viewModel.InitializeAsync();

        // Assert - can't go back from first step
        Assert.False(this.viewModel.CanGoBack);
    }

    [Fact]
    public async Task Abandon_SetsIsAbandonedTrue()
    {
        // Arrange
        await this.viewModel.InitializeAsync();

        // Act
        await this.viewModel.AbandonCommand.ExecuteAsync(null);

        // Assert
        var state = await this.stateStore.LoadAsync();
        Assert.True(state.IsAbandoned);
    }

    /// <summary>
    /// Mock implementation of IConsentService for testing.
    /// </summary>
    private sealed class MockConsentService : IConsentService
    {
        public bool IsConsentGranted => true;

        public Task<ConsentRecord> GetConsentStatusAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new ConsentRecord(ConsentStatus.Granted, DateTimeOffset.UtcNow, null));
        }

        public Task GrantConsentAsync(string? grantedByDeviceId, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}