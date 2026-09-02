// -----------------------------------------------------------------------------
// File        : pages/DashboardPage.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Operational overview for back-office officers and grid
//               operators. Every figure shown here is read from the Web API,
//               which computes the counts; nothing on this page is calculated
//               in the browser or hard coded.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { dashboardApi } from '../api/resources'
import { ApiError } from '../api/client'
import { useAuth } from '../context/useAuth'
import {
  Alert,
  EmptyState,
  Loading,
  PageHeader,
  StatusBadge,
  formatDateTime,
} from '../components/Ui'
import type { OperatorDashboard } from '../types'

/** A single headline figure. */
function StatTile({
  label,
  value,
  hint,
  tone,
}: {
  label: string
  value: number
  hint: string
  tone: 'amber' | 'blue' | 'emerald' | 'slate'
}) {
  const tones = {
    amber: 'bg-amber-50 text-amber-700 border-amber-200',
    blue: 'bg-blue-50 text-blue-700 border-blue-200',
    emerald: 'bg-emerald-50 text-emerald-700 border-emerald-200',
    slate: 'bg-ink-50 text-ink-700 border-ink-200',
  }[tone]

  return (
    <div className={`rounded-xl border p-5 ${tones}`}>
      <p className="text-xs font-medium uppercase tracking-wide opacity-80">{label}</p>
      <p className="mt-2 text-3xl font-semibold tabular-nums">{value}</p>
      <p className="mt-1 text-xs opacity-70">{hint}</p>
    </div>
  )
}

export default function DashboardPage() {
  const { user } = useAuth()
  const [data, setData] = useState<OperatorDashboard | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  /**
   * Fetches the live figures from the API.
   */
  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      setData(await dashboardApi.operator())
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not load the dashboard.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <>
      <PageHeader
        title={`Welcome back, ${user?.fullName?.split(' ')[0] ?? 'there'}`}
        description="Live operational overview of the VoltShare microgrid network."
        actions={
          <button type="button" onClick={() => void load()} className="btn-secondary">
            Refresh
          </button>
        }
      />

      {error && <Alert kind="error" message={error} onDismiss={() => setError(null)} />}

      {isLoading && !data ? (
        <Loading label="Loading dashboard…" />
      ) : data ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <StatTile
              label="Awaiting approval"
              value={data.pendingCount}
              hint="Reservations needing a decision"
              tone="amber"
            />
            <StatTile
              label="Approved upcoming"
              value={data.approvedFutureCount}
              hint="Confirmed future transfers"
              tone="blue"
            />
            <StatTile
              label="Completed today"
              value={data.completedTodayCount}
              hint="Energy transfers finalised"
              tone="emerald"
            />
            <StatTile
              label="Active nodes"
              value={data.activeStationCount}
              hint="Microgrid stations in service"
              tone="slate"
            />
          </div>

          <div className="card mt-6">
            <div className="card-header">
              <div>
                <h2 className="card-title">Today&rsquo;s schedule</h2>
                <p className="mt-0.5 text-xs text-ink-500">
                  Open reservations due within the current day.
                </p>
              </div>
              <Link to="/reservations" className="btn-secondary btn-sm">
                View all
              </Link>
            </div>

            {data.todaySchedule.length === 0 ? (
              <EmptyState
                title="Nothing scheduled for today"
                hint="Approved and pending reservations due today will appear here."
              />
            ) : (
              <div className="table-wrap">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Reference</th>
                      <th>Prosumer NIC</th>
                      <th>Station</th>
                      <th>Window</th>
                      <th>Energy</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.todaySchedule.map((r) => (
                      <tr key={r.id}>
                        <td className="font-medium text-ink-900">
                          <Link to={`/reservations/${r.id}`} className="hover:text-brand-600">
                            {r.reservationNo}
                          </Link>
                        </td>
                        <td>{r.prosumerNic}</td>
                        <td>{r.stationName ?? '—'}</td>
                        <td className="whitespace-nowrap">{formatDateTime(r.reservationStartUtc)}</td>
                        <td className="whitespace-nowrap">{r.energyKwh} kWh</td>
                        <td>
                          <StatusBadge status={r.status} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      ) : null}
    </>
  )
}
