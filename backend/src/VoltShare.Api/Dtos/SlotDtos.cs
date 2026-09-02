// -----------------------------------------------------------------------------
// File        : SlotDtos.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Dtos
// Description : Request and response contracts for the energy booking slots
//               offered by a station.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace VoltShare.Api.Dtos;

/// <summary>
/// A bookable energy transfer window as returned to the clients.
/// RemainingCapacity is calculated by the service so neither client has to
/// work out whether a slot is still available.
/// </summary>
public record SlotResponse(
    string Id,
    string StationId,
    DateTime StartTimeUtc,
    DateTime EndTimeUtc,
    int Capacity,
    int BookedCount,
    int RemainingCapacity,
    double EnergyKwhPerSlot,
    bool IsActive);

/// <summary>
/// Fields required to create a booking window at a station.
/// </summary>
public class CreateSlotRequest
{
    [Required(ErrorMessage = "Start time is required.")]
    public DateTime StartTimeUtc { get; set; }

    [Required(ErrorMessage = "End time is required.")]
    public DateTime EndTimeUtc { get; set; }

    [Range(1, 1000, ErrorMessage = "Capacity must be at least one.")]
    public int Capacity { get; set; } = 1;

    [Range(0.1, 100000, ErrorMessage = "Energy per slot must be greater than zero.")]
    public double EnergyKwhPerSlot { get; set; }
}

/// <summary>
/// Fields that may be changed on an existing booking window.
/// </summary>
public class UpdateSlotRequest
{
    [Required(ErrorMessage = "Start time is required.")]
    public DateTime StartTimeUtc { get; set; }

    [Required(ErrorMessage = "End time is required.")]
    public DateTime EndTimeUtc { get; set; }

    [Range(1, 1000, ErrorMessage = "Capacity must be at least one.")]
    public int Capacity { get; set; } = 1;

    [Range(0.1, 100000, ErrorMessage = "Energy per slot must be greater than zero.")]
    public double EnergyKwhPerSlot { get; set; }

    // Lets staff withdraw a window from sale without deleting booking history.
    public bool IsActive { get; set; } = true;
}
