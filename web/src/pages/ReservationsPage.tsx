// -----------------------------------------------------------------------------
// File        : pages/ReservationsPage.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Energy reservation management. Staff filter and search the
//               bookings, approve or reject the pending ones, and cancel on a
//               prosumer's behalf. The twelve hour notice rule is decided by
//               the service: this screen only reads the canBeCancelled flag it
//               returns to decide whether the button is available.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { reservationsApi, stationsApi } from '../api/resources'
import type { ReservationFilters } from '../api/resources'
import { ApiError } from '../api/client'
import {
  Alert,
  EmptyState,
  Loading,
  PageHeader,
  StatusBadge,
  formatDateTime,
} from '../components/Ui'
import type { Reservation, Station } from '../types'

// Offered as filter buttons, in the order a member of staff usually wants them.
const STATUS_FILTERS = ['', 'Pending', 'Approved', 'Completed', 'Cancelled', 'Rejected'] as const

export default function ReservationsPage() {
  const [reservations, setReservations] = useState<Reservation[]>([])
  const [stations, setStations] = useState<Station[]>([])

  const [filters, setFilters] = useState<ReservationFilters>({ status: '' })
  const [searchTerm, setSearchTerm] = useState('')

  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busyId, setBusyId] = useState<string | null>(null)

  // The station list only feeds the filter dropdown, so it is fetched once.
  useEffect(() => {
    stationsApi
      .list()
      .then(setStations)
      .catch(() => {
        // A failure here only costs the filter dropdown, so the page carries on.
      })
  }, [])

  /**
   * Loads the bookings matching the current filters.
   */
  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      setReservations(
        await reservationsApi.search({
          status: filters.status || undefined,
          stationId: filters.stationId || undefined,
          q: searchTerm.trim() || undefined,
        }),
      )
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not load reservations.')
    } finally {
      setIsLoading(false)
    }
  }, [filters, searchTerm])

  useEffect(() => {
    void load()
  }, [load])

  /**
   * Runs an approve, reject or cancel action and reports the outcome.
   */
  async function runAction(
    reservation: Reservation,
    action: 'approve' | 'reject' | 'cancel',
  ) {
    setBusyId(reservation.id)
    setError(null)
    setNotice(null)

    try {
      if (action === 'approve') {
        await reservationsApi.approve(reservation.id)
        setNotice(`${reservation.reservationNo} approved. A transaction QR code has been issued.`)
      } else if (action === 'reject') {
        await reservationsApi.reject(reservation.id)
        setNotice(`${reservation.reservationNo} was rejected and its place released.`)
      } else {
        // The summary message is written by the service, so the confirmation
        // the user sees is the server's own wording.
        const summary = await reservationsApi.cancel(reservation.id)
        setNotice(summary.message)
      }

      await load()
    } catch (caught) {
      // A cancellation inside the twelve hour window is refused here with the
      // service's explanation.
      setError(caught instanceof ApiError ? caught.message : 'Could not complete the action.')
    } finally {
      setBusyId(null)
    }
  }

  return (
    <>
      <PageHeader
        title="Energy Reservations"
        description="Power trading bookings across every microgrid node."
        actions={
          <button type="button" onClick={() => void load()} className="btn-secondary">
            Refresh
          </button>
        }
      />

      {error && <Alert kind="error" message={error} onDismiss={() => setError(null)} />}
      {notice && <Alert kind="success" message={notice} onDismiss={() => setNotice(null)} />}

      <div className="card mb-4 p-4">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex flex-wrap gap-1">
            {STATUS_FILTERS.map((status) => (
              <button
                key={status || 'all'}
                type="button"
                onClick={() => setFilters({ ...filters, status })}
                className={`chip ${filters.status === status ? 'chip-active' : ''}`}
              >
                {status || 'All'}
              </button>
            ))}
          </div>

          <div className="ml-auto flex flex-wrap items-end gap-3">
            <div>
              <label className="field-label">Station</label>
              <select
                value={filters.stationId ?? ''}
                onChange={(e) => setFilters({ ...filters, stationId: e.target.value })}
                className="field-input min-w-48"
              >
                <option value="">All stations</option>
                {stations.map((station) => (
                  <option key={station.id} value={station.id}>
                    {station.code} — {station.name}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="field-label">Search</label>
              <input
                type="search"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder="Reference or NIC…"
                className="field-input min-w-56"
              />
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {isLoading ? (
          <Loading />
        ) : reservations.length === 0 ? (
          <EmptyState
            title="No reservations match these filters"
            hint="Try clearing the status filter or the search term."
          />
        ) : (
          <>
            {/* Below the medium breakpoint the table is replaced with a list of
                cards. Squeezing eight columns onto a phone forces the station
                name to wrap over several lines and makes the whole row hard to
                read, so each booking gets its own block instead. */}
            <ul className="divide-y divide-line md:hidden">
              {reservations.map((r) => (
                <li key={r.id} className="p-4">
                  <div className="flex items-start justify-between gap-3">
                    <Link
                      to={`/reservations/${r.id}`}
                      className="font-medium text-ink-900 hover:text-brand-600"
                    >
                      {r.reservationNo}
                    </Link>
                    <StatusBadge status={r.status} />
                  </div>

                  <p className="mt-1 text-sm text-ink-700">{r.stationName ?? '—'}</p>
                  <p className="text-xs text-ink-400">
                    {r.prosumerName ?? '—'} · {r.prosumerNic}
                  </p>

                  <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-ink-500">
                    <span>{formatDateTime(r.reservationStartUtc)}</span>
                    <span aria-hidden="true">·</span>
                    <span>{r.energyKwh} kWh</span>
                    <span
                      className={`badge ${
                        r.type === 'Injection'
                          ? 'bg-success-bg text-success-fg'
                          : 'bg-info-bg text-info-fg'
                      }`}
                    >
                      {r.type}
                    </span>
                  </div>

                  <div className="mt-3 flex flex-wrap gap-2">
                    {r.status === 'Pending' && (
                      <>
                        <button
                          type="button"
                          disabled={busyId === r.id}
                          onClick={() => void runAction(r, 'approve')}
                          className="btn-success btn-sm"
                        >
                          Approve
                        </button>
                        <button
                          type="button"
                          disabled={busyId === r.id}
                          onClick={() => void runAction(r, 'reject')}
                          className="btn-secondary btn-sm"
                        >
                          Reject
                        </button>
                      </>
                    )}

                    {r.canBeCancelled && (
                      <button
                        type="button"
                        disabled={busyId === r.id}
                        onClick={() => void runAction(r, 'cancel')}
                        className="btn-danger btn-sm"
                      >
                        Cancel
                      </button>
                    )}

                    <Link to={`/reservations/${r.id}`} className="btn-secondary btn-sm">
                      View
                    </Link>
                  </div>
                </li>
              ))}
            </ul>

            <div className="table-wrap hidden md:block">
            <table className="table">
              <thead>
                <tr>
                  <th>Reference</th>
                  <th>Prosumer</th>
                  <th>Station</th>
                  <th>Window</th>
                  <th>Type</th>
                  <th>Energy</th>
                  <th>Status</th>
                  <th className="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {reservations.map((r) => (
                  <tr key={r.id}>
                    <td className="font-medium text-ink-900">
                      <Link to={`/reservations/${r.id}`} className="hover:text-brand-600">
                        {r.reservationNo}
                      </Link>
                    </td>
                    <td>
                      <p>{r.prosumerName ?? '—'}</p>
                      <p className="text-xs text-ink-400">{r.prosumerNic}</p>
                    </td>
                    <td>{r.stationName ?? '—'}</td>
                    <td className="whitespace-nowrap">{formatDateTime(r.reservationStartUtc)}</td>
                    <td>
                      <span
                        className={`badge ${
                          r.type === 'Injection'
                            ? 'bg-success-bg text-success-fg'
                            : 'bg-info-bg text-info-fg'
                        }`}
                      >
                        {r.type}
                      </span>
                    </td>
                    <td className="whitespace-nowrap">{r.energyKwh} kWh</td>
                    <td>
                      <StatusBadge status={r.status} />
                    </td>
                    <td>
                      <div className="flex justify-end gap-2">
                        {r.status === 'Pending' && (
                          <>
                            <button
                              type="button"
                              disabled={busyId === r.id}
                              onClick={() => void runAction(r, 'approve')}
                              className="btn-success btn-sm"
                            >
                              Approve
                            </button>
                            <button
                              type="button"
                              disabled={busyId === r.id}
                              onClick={() => void runAction(r, 'reject')}
                              className="btn-secondary btn-sm"
                            >
                              Reject
                            </button>
                          </>
                        )}

                        {/* canBeCancelled is decided by the service from the
                            twelve hour rule; this screen never works it out. */}
                        {r.canBeCancelled && (
                          <button
                            type="button"
                            disabled={busyId === r.id}
                            onClick={() => void runAction(r, 'cancel')}
                            className="btn-danger btn-sm"
                          >
                            Cancel
                          </button>
                        )}

                        <Link to={`/reservations/${r.id}`} className="btn-secondary btn-sm">
                          View
                        </Link>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          </>
        )}
      </div>
    </>
  )
}
