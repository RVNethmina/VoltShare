// -----------------------------------------------------------------------------
// File        : DashboardService.cs
// Project     : VoltShare.Api - Smart Solar Microgrid Trading System
// Module      : Services
// Description : Produces the counts shown on the prosumer home screen and the
//               grid operator dashboard. Every figure is calculated here from
//               live data, so neither client aggregates anything itself and no
//               value is ever hard coded in a screen.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

using VoltShare.Api.Dtos;
using VoltShare.Api.Models;
using VoltShare.Api.Repositories;

namespace VoltShare.Api.Services;

/// <summary>
/// Contract for the role based dashboard figures.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Builds the counts shown on the prosumer home screen.
    /// </summary>
    Task<ProsumerDashboardResponse> GetProsumerDashboardAsync(
        string prosumerNic, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the counts and today's workload shown to a grid operator.
    /// </summary>
    Task<OperatorDashboardResponse> GetOperatorDashboardAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads live counts from the reservation and station collections.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IReservationRepository _reservations;
    private readonly IStationRepository _stations;

    /// <summary>
    /// Receives its collaborators from dependency injection.
    /// </summary>
    public DashboardService(IReservationRepository reservations, IStationRepository stations)
    {
        _reservations = reservations;
        _stations = stations;
    }

    /// <summary>
    /// Builds the prosumer dashboard: how many bookings are awaiting approval,
    /// how many approved bookings are still to come, and the next one due.
    /// </summary>
    public async Task<ProsumerDashboardResponse> GetProsumerDashboardAsync(
        string prosumerNic, CancellationToken cancellationToken = default)
    {
        // Every count is measured against the same instant so the figures on
        // one screen cannot contradict each other.
        var now = DateTime.UtcNow;

        var pending = await _reservations.CountAsync(new ReservationQuery
        {
            ProsumerNic = prosumerNic,
            Status = ReservationStatus.Pending
        }, cancellationToken);

        // "Approved future" counts only bookings that have not happened yet,
        // which is what the prosumer actually has coming up.
        var approvedFuture = await _reservations.CountAsync(new ReservationQuery
        {
            ProsumerNic = prosumerNic,
            Status = ReservationStatus.Approved,
            FromUtc = now
        }, cancellationToken);

        var completed = await _reservations.CountAsync(new ReservationQuery
        {
            ProsumerNic = prosumerNic,
            Status = ReservationStatus.Completed
        }, cancellationToken);

        var cancelled = await _reservations.CountAsync(new ReservationQuery
        {
            ProsumerNic = prosumerNic,
            Status = ReservationStatus.Cancelled
        }, cancellationToken);

        var next = await _reservations.GetNextUpcomingAsync(prosumerNic, now, cancellationToken);

        // The station name is only looked up when there actually is a next
        // booking to describe.
        string? stationName = null;
        if (next is not null)
        {
            var station = await _stations.GetByIdAsync(next.StationId, cancellationToken);
            stationName = station?.Name;
        }

        return new ProsumerDashboardResponse(
            prosumerNic,
            pending,
            approvedFuture,
            completed,
            cancelled,
            next?.ToResponse(stationName: stationName, nowUtc: now));
    }

    /// <summary>
    /// Builds the operator dashboard: outstanding approvals, approved bookings
    /// still to come, transfers completed today and today's schedule.
    /// </summary>
    public async Task<OperatorDashboardResponse> GetOperatorDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // The operating day is measured in UTC so the boundary is unambiguous
        // and matches how the timestamps are stored.
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var startOfNextDay = startOfDay.AddDays(1);

        var pending = await _reservations.CountAsync(new ReservationQuery
        {
            Status = ReservationStatus.Pending
        }, cancellationToken);

        var approvedFuture = await _reservations.CountAsync(new ReservationQuery
        {
            Status = ReservationStatus.Approved,
            FromUtc = now
        }, cancellationToken);

        var completedToday = await _reservations.CountAsync(new ReservationQuery
        {
            Status = ReservationStatus.Completed,
            FromUtc = startOfDay,
            ToUtc = startOfNextDay
        }, cancellationToken);

        // Today's schedule is every booking still open within the current day,
        // which is the operator's actual workload.
        var schedule = await _reservations.SearchAsync(new ReservationQuery
        {
            Statuses = ReservationStatus.Active,
            FromUtc = startOfDay,
            ToUtc = startOfNextDay,
            Limit = 100
        }, cancellationToken);

        var stations = await _stations.ListAsync(isActive: true, cancellationToken: cancellationToken);
        var stationNames = stations.ToDictionary(s => s.Id, s => s.Name);

        return new OperatorDashboardResponse(
            pending,
            approvedFuture,
            completedToday,
            stations.Count,
            schedule.ToResponseList(stationNames: stationNames));
    }
}
