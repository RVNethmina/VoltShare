// -----------------------------------------------------------------------------
// File        : pages/StationDetailPage.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : One microgrid node and the energy booking windows it offers.
//               Staff add, edit and remove windows here. The API refuses to
//               delete a window that prosumers have booked, and refuses to cut
//               its capacity below the places already taken.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { slotsApi, stationsApi } from '../api/resources'
import { ApiError } from '../api/client'
import {
  ActiveBadge,
  Alert,
  EmptyState,
  Loading,
  Modal,
  PageHeader,
  formatDateTime,
} from '../components/Ui'
import type { Slot, Station } from '../types'

/**
 * Converts a UTC timestamp into the value a datetime-local input expects,
 * which is local time with no zone marker.
 */
function toLocalInputValue(utc: string): string {
  const date = new Date(utc)
  const offsetMs = date.getTimezoneOffset() * 60_000

  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16)
}

/**
 * Converts the local value typed into a datetime-local input back into the UTC
 * string the API expects. All times cross the wire as UTC.
 */
function toUtcIso(localValue: string): string {
  return new Date(localValue).toISOString()
}

/** Sensible default window: tomorrow morning, two hours long. */
function defaultSlotForm() {
  const start = new Date()
  start.setDate(start.getDate() + 1)
  start.setHours(9, 0, 0, 0)

  const end = new Date(start)
  end.setHours(start.getHours() + 2)

  return {
    startLocal: toLocalInputValue(start.toISOString()),
    endLocal: toLocalInputValue(end.toISOString()),
    capacity: 5,
    energyKwhPerSlot: 15,
    isActive: true,
  }
}

export default function StationDetailPage() {
  const { id = '' } = useParams()

  const [station, setStation] = useState<Station | null>(null)
  const [slots, setSlots] = useState<Slot[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [isFormOpen, setIsFormOpen] = useState(false)
  const [editing, setEditing] = useState<Slot | null>(null)
  const [form, setForm] = useState(defaultSlotForm)
  const [isSaving, setIsSaving] = useState(false)

  /**
   * Loads the node and its booking windows together.
   */
  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      const [stationResult, slotResult] = await Promise.all([
        stationsApi.get(id),
        stationsApi.listSlots(id),
      ])

      setStation(stationResult)
      setSlots(slotResult)
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not load the station.')
    } finally {
      setIsLoading(false)
    }
  }, [id])

  useEffect(() => {
    void load()
  }, [load])

  /** Opens the form ready to add a new window. */
  function openCreate() {
    setEditing(null)
    setForm(defaultSlotForm())
    setError(null)
    setIsFormOpen(true)
  }

  /** Opens the form populated with an existing window. */
  function openEdit(slot: Slot) {
    setEditing(slot)
    setForm({
      startLocal: toLocalInputValue(slot.startTimeUtc),
      endLocal: toLocalInputValue(slot.endTimeUtc),
      capacity: slot.capacity,
      energyKwhPerSlot: slot.energyKwhPerSlot,
      isActive: slot.isActive,
    })
    setError(null)
    setIsFormOpen(true)
  }

  /**
   * Creates or updates a booking window.
   */
  async function handleSave(event: FormEvent) {
    event.preventDefault()
    setIsSaving(true)
    setError(null)

    const payload = {
      startTimeUtc: toUtcIso(form.startLocal),
      endTimeUtc: toUtcIso(form.endLocal),
      capacity: form.capacity,
      energyKwhPerSlot: form.energyKwhPerSlot,
      isActive: form.isActive,
    }

    try {
      if (editing) {
        await slotsApi.update(editing.id, payload)
        setNotice('Booking window updated.')
      } else {
        await stationsApi.createSlot(id, payload)
        setNotice('Booking window added.')
      }

      setIsFormOpen(false)
      await load()
    } catch (caught) {
      // The API enforces the window rules: end after start, at most 24 hours,
      // no clash with another window, and capacity not below what is booked.
      setError(caught instanceof ApiError ? caught.message : 'Could not save the booking window.')
    } finally {
      setIsSaving(false)
    }
  }

  /**
   * Deletes a booking window, which the API refuses while it is booked.
   */
  async function handleDelete(slot: Slot) {
    setError(null)
    setNotice(null)

    try {
      await slotsApi.remove(slot.id)
      setNotice('Booking window deleted.')
      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not delete the booking window.')
    }
  }

  if (isLoading && !station) return <Loading label="Loading station…" />

  return (
    <>
      <PageHeader
        title={station ? `${station.name}` : 'Station'}
        description={station ? `${station.code} · ${station.addressLine}, ${station.city}` : undefined}
        actions={
          <>
            <Link to="/stations" className="btn-secondary">
              Back to nodes
            </Link>
            <button type="button" onClick={openCreate} className="btn-primary">
              + Add booking window
            </button>
          </>
        }
      />

      {error && <Alert kind="error" message={error} onDismiss={() => setError(null)} />}
      {notice && <Alert kind="success" message={notice} onDismiss={() => setNotice(null)} />}

      {station && (
        <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <div className="card p-5">
            <p className="text-xs uppercase tracking-wide text-ink-500">Capacity</p>
            <p className="mt-1 text-2xl font-semibold">{station.capacityKwh} kWh</p>
          </div>
          <div className="card p-5">
            <p className="text-xs uppercase tracking-wide text-ink-500">Battery slots free</p>
            <p className="mt-1 text-2xl font-semibold">
              {station.availableBatterySlots}
              <span className="text-base text-ink-400"> / {station.totalBatterySlots}</span>
            </p>
          </div>
          <div className="card p-5">
            <p className="text-xs uppercase tracking-wide text-ink-500">Operating hours</p>
            <p className="mt-1 text-2xl font-semibold">
              {station.operatingHours.openTime}–{station.operatingHours.closeTime}
            </p>
          </div>
          <div className="card p-5">
            <p className="text-xs uppercase tracking-wide text-ink-500">Status</p>
            <p className="mt-2">
              <ActiveBadge isActive={station.isActive} />
            </p>
            <p className="mt-2 text-xs text-ink-400">
              {station.latitude.toFixed(4)}, {station.longitude.toFixed(4)}
            </p>
          </div>
        </div>
      )}

      <div className="card">
        <div className="card-header">
          <div>
            <h2 className="card-title">Energy booking windows</h2>
            <p className="mt-0.5 text-xs text-ink-500">
              Prosumers reserve places in these windows from the mobile application.
            </p>
          </div>
        </div>

        {slots.length === 0 ? (
          <EmptyState
            title="No booking windows yet"
            hint="Add a window so prosumers can reserve energy transfers at this node."
          />
        ) : (
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Starts</th>
                  <th>Ends</th>
                  <th>Energy</th>
                  <th>Booked</th>
                  <th>Remaining</th>
                  <th>Status</th>
                  <th className="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {slots.map((slot) => (
                  <tr key={slot.id}>
                    <td className="whitespace-nowrap font-medium text-ink-900">
                      {formatDateTime(slot.startTimeUtc)}
                    </td>
                    <td className="whitespace-nowrap">{formatDateTime(slot.endTimeUtc)}</td>
                    <td className="whitespace-nowrap">{slot.energyKwhPerSlot} kWh</td>
                    <td>
                      {slot.bookedCount} / {slot.capacity}
                    </td>
                    <td>
                      <span
                        className={`badge ${
                          slot.remainingCapacity === 0
                            ? 'bg-red-100 text-red-800'
                            : 'bg-emerald-100 text-emerald-800'
                        }`}
                      >
                        {slot.remainingCapacity === 0 ? 'Full' : `${slot.remainingCapacity} free`}
                      </span>
                    </td>
                    <td>
                      <ActiveBadge isActive={slot.isActive} />
                    </td>
                    <td>
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => openEdit(slot)}
                          className="btn-secondary btn-sm"
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          onClick={() => void handleDelete(slot)}
                          className="btn-danger btn-sm"
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <Modal
        title={editing ? 'Edit booking window' : 'Add booking window'}
        isOpen={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        footer={
          <>
            <button type="button" onClick={() => setIsFormOpen(false)} className="btn-secondary">
              Cancel
            </button>
            <button type="submit" form="slot-form" disabled={isSaving} className="btn-primary">
              {isSaving ? 'Saving…' : 'Save window'}
            </button>
          </>
        }
      >
        <form id="slot-form" onSubmit={handleSave} className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="field-label">Starts</label>
            <input
              required
              type="datetime-local"
              value={form.startLocal}
              onChange={(e) => setForm({ ...form, startLocal: e.target.value })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Ends</label>
            <input
              required
              type="datetime-local"
              value={form.endLocal}
              onChange={(e) => setForm({ ...form, endLocal: e.target.value })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Places available</label>
            <input
              required
              type="number"
              min={1}
              value={form.capacity}
              onChange={(e) => setForm({ ...form, capacity: Number(e.target.value) })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Energy per place (kWh)</label>
            <input
              required
              type="number"
              min={0.1}
              step="0.1"
              value={form.energyKwhPerSlot}
              onChange={(e) => setForm({ ...form, energyKwhPerSlot: Number(e.target.value) })}
              className="field-input"
            />
          </div>

          {editing && (
            <label className="flex items-center gap-2 text-sm sm:col-span-2">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                className="h-4 w-4 rounded border-ink-300"
              />
              Window is open for booking
            </label>
          )}

          <p className="text-xs text-ink-400 sm:col-span-2">
            Times are entered in your local time and sent to the service as UTC. A window
            must end after it starts and cannot be longer than 24 hours.
          </p>
        </form>
      </Modal>
    </>
  )
}
