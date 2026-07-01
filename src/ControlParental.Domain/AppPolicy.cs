// <copyright file="AppPolicy.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json.Serialization;

/// <summary>
/// Policy for a specific app (identified by package_name / AppId).
/// </summary>
public sealed record AppPolicy
{
    /// <summary>
    /// Gets the AppId canónico (AUMID para MSIX, exe+publisher para Win32).
    /// </summary>
    [JsonPropertyName("package_name")]
    public string PackageName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the policy state: allowed, blocked, limited, always_allowed.
    /// </summary>
    [JsonPropertyName("state")]
    public AppPolicyState State { get; init; }

    /// <summary>
    /// Gets the daily time limit in minutes.
    /// Required when state is Limited.
    /// </summary>
    [JsonPropertyName("daily_limit_minutes")]
    public int? DailyLimitMinutes { get; init; }

    /// <summary>
    /// Gets the category of this app.
    /// Used for category-based limits.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>
    /// Gets the allowed time windows for this app.
    /// Optional: if present and not empty, the app is only allowed within these windows.
    /// </summary>
    [JsonPropertyName("allowed_windows")]
    public Window[]? AllowedWindows { get; init; }

    /// <summary>
    /// Validates the app policy invariants.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the policy is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PackageName))
        {
            throw new ArgumentException(
                "AppPolicy must have a non-empty package_name.",
                nameof(PackageName));
        }

        if (State == AppPolicyState.Limited)
        {
            if (!DailyLimitMinutes.HasValue || DailyLimitMinutes.Value <= 0)
            {
                throw new ArgumentException(
                    "AppPolicy with state 'limited' must have a positive daily_limit_minutes.",
                    nameof(DailyLimitMinutes));
            }
        }

        if (AllowedWindows != null)
        {
            foreach (var window in AllowedWindows)
            {
                window.Validate();
            }
        }
    }
}