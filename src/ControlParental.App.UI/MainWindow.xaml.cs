// <copyright file="MainWindow.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — Main window hosting the onboarding flow.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly OnboardingViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The onboarding view model. Cannot be null.</param>
    public MainWindow(OnboardingViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.InitializeComponent();
        ((FrameworkElement)this.Content).DataContext = this.viewModel;

        // T26: Verify bindings work by logging current step data
        System.Diagnostics.Debug.WriteLine($"[T26] DataContext set: {this.viewModel.CurrentStep?.Title ?? "NULL"}");
        System.Diagnostics.Debug.WriteLine($"[T26] ProgressLabel: {this.viewModel.ProgressLabel}");
        System.Diagnostics.Debug.WriteLine($"[T26] ProgressCount: {this.viewModel.ProgressCount}");

        _ = this.InitializeOnboardingAsync();
    }

    private async Task InitializeOnboardingAsync()
    {
        try
        {
            await this.viewModel.InitializeAsync();
        }
        catch
        {
            // Onboarding init failed — window still shows with current state
        }
    }
}
