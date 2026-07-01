// <copyright file="Schedule.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json.Serialization;

/// <summary>
/// A time schedule that applies a rule during specific time windows.
/// </summary>
public sealed record Schedule
{
    /// <summary>
    /// Gets the unique identifier for this schedule.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the days when this schedule is active.
    /// </summary>
    [JsonPropertyName("days")]
    public DayOfWeek[] Days { get; init; } = [];

    /// <summary>
    /// Gets the start time in HH:mm format.
    /// </summary>
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    /// <summary>
    /// Gets the end time in HH:mm format.
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    /// <summary>
    /// Gets the action to take during this schedule.
    /// lock: block all non-always_allowed apps.
    /// allow_only: allow only apps in allow_list.
    /// </summary>
    [JsonPropertyName("action")]
    public ScheduleAction Action { get; init; }

    /// <summary>
    /// Gets the list of allowed apps when action is AllowOnly.
    /// Required when action is AllowOnly; ignored for Lock.
    /// </summary>
    [JsonPropertyName("allow_list")]
    public string[]? AllowList { get; init; }

    /// <summary>
    /// Validates the schedule invariants.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the schedule is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Schedule must have a non-empty Id.", nameof(Id));
        }

        if (Days == null || Days.Length == 0)
        {
            throw new ArgumentException("Schedule must have at least one day.", nameof(Days));
        }

        if (!IsValidTimeFormat(From))
        {
            throw new ArgumentException(
                $"Invalid start time format '{From}'. Expected HH:mm.",
                nameof(From));
        }

        if (!IsValidTimeFormat(To))
        {
            throw new ArgumentException(
                $"Invalid end time format '{To}'. Expected HH:mm.",
                nameof(To));
        }

        if (Action == ScheduleAction.AllowOnly)
        {
            if (AllowList == null || AllowList.Length == 0)
            {
                throw new ArgumentException(
                    "Schedule with action 'allow_only' must have a non-empty allow_list.",
                    nameof(AllowList));
            }

            // Check no empty entries in allow_list
            if (AllowList.Any(app => string.IsNullOrWhiteSpace(app)))
            {
                throw new ArgumentException(
                    "AllowList must not contain empty entries.",
                    nameof(AllowList));
            }
        }
    }

    private static bool IsValidTimeFormat(string time)
    {
        if (string.IsNullOrEmpty(time) || time.Length != 5)
        {
            return false;
        }

        if (time[2] != ':')
        {
            return false;
        }

        return int.TryParse(time.AsSpan(0, 2), out var hours) &&
               int.TryParse(time.AsSpan(3, 2), out var minutes) &&
               hours >= 0 && hours <= 23 &&
               minutes >= 0 && minutes <= 59;
    }

    /// <summary>
    /// Gets the window as a Window object for reuse in engine logic.
    /// </summary>
    public Window ToWindow() => new() { Days = Days, From = From, To = To };

    /// <summary>
    /// Gets whether this schedule crosses midnight (e.g. 22:00 → 07:00).
    /// </summary>
    public bool CrossesMidnight => !string.IsNullOrEmpty(From) &&
        !string.IsNullOrEmpty(To) &&
        string.Compare(From, To, StringComparison.Ordinal) > 0;
}