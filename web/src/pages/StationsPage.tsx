// -----------------------------------------------------------------------------
// File        : pages/StationsPage.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Management of the solar microgrid nodes. Back-office officers
//               register and edit nodes and take them out of service; grid
//               operators update battery availability. When the API refuses an
//               action, such as deactivating a node that still has bookings,
//               its own explanation is shown unchanged.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { stationsApi } from '../api/resources'
import type { StationPayload } from '../api/resources'
import { ApiError } from '../api/client'
import { useAuth } from '../context/useAuth'
import { ActiveBadge, Alert, EmptyState, Loading, Modal, PageHeader } from '../components/Ui'
import type { Station } from '../types'

// A new station starts centred on Colombo, which saves typing coordinates for
// the common case while still allowing any location to be entered.
const BLANK_FORM: StationPayload & { code: string } = {
  code: '',
  name: '',
  addressLine: '',
  city: '',
  latitude: 6.9271,
  longitude: 79.8612,
  capacityKwh: 100,
  totalBatterySlots: 10,
  openTime: '06:00',
  closeTime: '20:00',
}

export default function StationsPage() {
  const { user } = useAuth()
  const isBackoffice = user?.role === 'Backoffice'

  const [stations, setStations] = useState<Station[]>([])
  const [search, setSearch] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [isFormOpen, setIsFormOpen] = useState(false)
  const [editing, setEditing] = useState<Station | null>(null)
  const [form, setForm] = useState(BLANK_FORM)
  const [isSaving, setIsSaving] = useState(false)

  /**
   * Loads the station list, applying the current search term.
   */
  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      setStations(await stationsApi.list({ search: search.trim() || undefined }))
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not load stations.')
    } finally {
      setIsLoading(false)
    }
  }, [search])

  useEffect(() => {
    void load()
  }, [load])

  /** Opens the form ready to register a new node. */
  function openCreate() {
    setEditing(null)
    setForm(BLANK_FORM)
    setError(null)
    setIsFormOpen(true)
  }

  /** Opens the form populated with an existing node. */
  function openEdit(station: Station) {
    setEditing(station)
    setForm({
      code: station.code,
      name: station.name,
      addressLine: station.addressLine,
      city: station.city,
      latitude: station.latitude,
      longitude: station.longitude,
      capacityKwh: station.capacityKwh,
      totalBatterySlots: station.totalBatterySlots,
      openTime: station.operatingHours.openTime,
      closeTime: station.operatingHours.closeTime,
    })
    setError(null)
    setIsFormOpen(true)
  }

  /**
   * Creates or updates a node, then refreshes the list.
   */
  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setIsSaving(true)
    setError(null)

    try {
      if (editing) {
        await stationsApi.update(editing.id, form)
        setNotice(`Station ${editing.code} was updated.`)
      } else {
        await stationsApi.create(form)
        setNotice(`Station ${form.code} was registered.`)
      }

      setIsFormOpen(false)
      await load()
    } catch (caught) {
      // Duplicate codes and invalid coordinates are rejected by the API; its
      // wording is shown so the reason is always the server's.
      setError(caught instanceof ApiError ? caught.message : 'Could not save the station.')
    } finally {
      setIsSaving(false)
    }
  }

  /**
   * Toggles a node in or out of service. Deactivation is refused by the API
   * while the node still holds active reservations.
   */
  async function handleToggleActive(station: Station) {
    setError(null)
    setNotice(null)

    try {
      if (station.isActive) {
        await stationsApi.deactivate(station.id)
        setNotice(`Station ${station.code} was taken out of service.`)
      } else {
        await stationsApi.activate(station.id)
        setNotice(`Station ${station.code} is back in service.`)
      }

      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not change the station status.')
    }
  }

  /**
   * Updates how many battery storage slots are currently free.
   */
  async function handleBatteryChange(station: Station, value: string) {
    const parsed = Number(value)

    // The API validates this too; checking here just avoids a pointless call.
    if (!Number.isFinite(parsed) || parsed < 0) return

    setError(null)

    try {
      await stationsApi.updateBatterySlots(station.id, parsed)
      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not update battery slots.')
    }
  }

  return (
    <>
      <PageHeader
        title="Microgrid Nodes"
        description="Solar grid hubs, their capacity, battery storage and operating schedule."
        actions={
          isBackoffice ? (
            <button type="button" onClick={openCreate} className="btn-primary">
              + Register node
            </button>
          ) : undefined
        }
      />

      {error && <Alert kind="error" message={error} onDismiss={() => setError(null)} />}
      {notice && <Alert kind="success" message={notice} onDismiss={() => setNotice(null)} />}

      <div className="card">
        <div className="card-header">
          <h2 className="card-title">All nodes</h2>
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search code, name or address…"
            className="field-input max-w-xs"
          />
        </div>

        {isLoading ? (
          <Loading />
        ) : stations.length === 0 ? (
          <EmptyState
            title="No microgrid nodes found"
            hint={search ? 'Try a different search term.' : 'Register the first node to begin.'}
          />
        ) : (
          <>
            {/* On a phone the eight column table forces the node name and
                address to wrap badly, so each node becomes its own card. */}
            <ul className="divide-y divide-line md:hidden">
              {stations.map((station) => (
                <li key={station.id} className="p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link
                        to={`/stations/${station.id}`}
                        className="font-medium text-ink-900 hover:text-brand-600"
                      >
                        {station.name}
                      </Link>
                      <p className="text-xs text-ink-400">
                        {station.code} · {station.addressLine}, {station.city}
                      </p>
                    </div>
                    <ActiveBadge isActive={station.isActive} />
                  </div>

                  <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-500">
                    <span>{station.capacityKwh} kWh</span>
                    <span>
                      Battery {station.availableBatterySlots} / {station.totalBatterySlots}
                    </span>
                    <span>
                      {station.operatingHours.openTime}–{station.operatingHours.closeTime}
                    </span>
                  </div>

                  <div className="mt-3 flex flex-wrap gap-2">
                    <Link to={`/stations/${station.id}`} className="btn-secondary btn-sm">
                      Slots
                    </Link>
                    {isBackoffice && (
                      <>
                        <button
                          type="button"
                          onClick={() => openEdit(station)}
                          className="btn-secondary btn-sm"
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          onClick={() => void handleToggleActive(station)}
                          className={station.isActive ? 'btn-danger btn-sm' : 'btn-success btn-sm'}
                        >
                          {station.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </>
                    )}
                  </div>
                </li>
              ))}
            </ul>

          <div className="table-wrap hidden md:block">
            <table className="table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>City</th>
                  <th>Capacity</th>
                  <th>Battery free</th>
                  <th>Hours</th>
                  <th>Status</th>
                  <th className="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {stations.map((station) => (
                  <tr key={station.id}>
                    <td className="font-medium text-ink-900">{station.code}</td>
                    <td>
                      <Link to={`/stations/${station.id}`} className="font-medium hover:text-brand-600">
                        {station.name}
                      </Link>
                      <p className="text-xs text-ink-400">{station.addressLine}</p>
                    </td>
                    <td>{station.city}</td>
                    <td className="whitespace-nowrap">{station.capacityKwh} kWh</td>
                    <td>
                      <div className="flex items-center gap-1">
                        <input
                          type="number"
                          min={0}
                          max={station.totalBatterySlots}
                          defaultValue={station.availableBatterySlots}
                          onBlur={(e) => void handleBatteryChange(station, e.target.value)}
                          className="w-16 rounded border border-line-strong px-2 py-1 text-sm"
                          title="Battery slots currently free"
                        />
                        <span className="text-xs text-ink-400">/ {station.totalBatterySlots}</span>
                      </div>
                    </td>
                    <td className="whitespace-nowrap text-xs">
                      {station.operatingHours.openTime}–{station.operatingHours.closeTime}
                    </td>
                    <td>
                      <ActiveBadge isActive={station.isActive} />
                    </td>
                    <td>
                      <div className="flex justify-end gap-2">
                        <Link to={`/stations/${station.id}`} className="btn-secondary btn-sm">
                          Slots
                        </Link>

                        {isBackoffice && (
                          <>
                            <button
                              type="button"
                              onClick={() => openEdit(station)}
                              className="btn-secondary btn-sm"
                            >
                              Edit
                            </button>
                            <button
                              type="button"
                              onClick={() => void handleToggleActive(station)}
                              className={station.isActive ? 'btn-danger btn-sm' : 'btn-success btn-sm'}
                            >
                              {station.isActive ? 'Deactivate' : 'Activate'}
                            </button>
                          </>
                        )}
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

      <Modal
        title={editing ? `Edit ${editing.code}` : 'Register a microgrid node'}
        isOpen={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        footer={
          <>
            <button type="button" onClick={() => setIsFormOpen(false)} className="btn-secondary">
              Cancel
            </button>
            <button type="submit" form="station-form" disabled={isSaving} className="btn-primary">
              {isSaving ? 'Saving…' : editing ? 'Save changes' : 'Register node'}
            </button>
          </>
        }
      >
        <form id="station-form" onSubmit={handleSave} className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="field-label">Station code</label>
            <input
              required
              value={form.code}
              // The code identifies the node in other records, so it is fixed
              // once the node exists.
              disabled={editing !== null}
              onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
              className="field-input"
              placeholder="SS-COL-001"
            />
          </div>

          <div>
            <label className="field-label">Name</label>
            <input
              required
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
              className="field-input"
              placeholder="Colombo Fort Solar Hub"
            />
          </div>

          <div className="sm:col-span-2">
            <label className="field-label">Address</label>
            <input
              required
              value={form.addressLine}
              onChange={(e) => setForm({ ...form, addressLine: e.target.value })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">City</label>
            <input
              required
              value={form.city}
              onChange={(e) => setForm({ ...form, city: e.target.value })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Capacity (kWh)</label>
            <input
              required
              type="number"
              min={0.1}
              step="0.1"
              value={form.capacityKwh}
              onChange={(e) => setForm({ ...form, capacityKwh: Number(e.target.value) })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Latitude</label>
            <input
              required
              type="number"
              step="any"
              min={-90}
              max={90}
              value={form.latitude}
              onChange={(e) => setForm({ ...form, latitude: Number(e.target.value) })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Longitude</label>
            <input
              required
              type="number"
              step="any"
              min={-180}
              max={180}
              value={form.longitude}
              onChange={(e) => setForm({ ...form, longitude: Number(e.target.value) })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Battery slots installed</label>
            <input
              required
              type="number"
              min={0}
              value={form.totalBatterySlots}
              onChange={(e) => setForm({ ...form, totalBatterySlots: Number(e.target.value) })}
              className="field-input"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="field-label">Opens</label>
              <input
                required
                type="time"
                value={form.openTime}
                onChange={(e) => setForm({ ...form, openTime: e.target.value })}
                className="field-input"
              />
            </div>
            <div>
              <label className="field-label">Closes</label>
              <input
                required
                type="time"
                value={form.closeTime}
                onChange={(e) => setForm({ ...form, closeTime: e.target.value })}
                className="field-input"
              />
            </div>
          </div>

          <p className="text-xs text-ink-400 sm:col-span-2">
            Coordinates are stored as a GeoJSON point so the mobile application can find
            nearby nodes through the service.
          </p>
        </form>
      </Modal>
    </>
  )
}
