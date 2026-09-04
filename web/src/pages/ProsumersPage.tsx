// -----------------------------------------------------------------------------
// File        : pages/ProsumersPage.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Solar prosumer accounts, keyed by NIC. Staff search and review
//               the accounts; back-office officers create them, edit them and
//               control activation. Only a back-office officer can reactivate a
//               deactivated account, which the service enforces.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { prosumersApi } from '../api/resources'
import type { CreateProsumerPayload } from '../api/resources'
import { ApiError } from '../api/client'
import { useAuth } from '../context/useAuth'
import {
  ActiveBadge,
  Alert,
  EmptyState,
  Loading,
  Modal,
  PageHeader,
  formatDate,
} from '../components/Ui'
import type { User } from '../types'

const BLANK_FORM: CreateProsumerPayload = {
  nic: '',
  fullName: '',
  email: '',
  phone: '',
  address: '',
  password: '',
  activateImmediately: true,
}

export default function ProsumersPage() {
  const { user } = useAuth()
  const isBackoffice = user?.role === 'Backoffice'

  const [prosumers, setProsumers] = useState<User[]>([])
  const [search, setSearch] = useState('')
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all')

  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [isFormOpen, setIsFormOpen] = useState(false)
  const [form, setForm] = useState(BLANK_FORM)
  const [isSaving, setIsSaving] = useState(false)

  /**
   * Loads the prosumer list using the current search and activation filter.
   */
  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      setProsumers(
        await prosumersApi.list({
          search: search.trim() || undefined,
          isActive: activeFilter === 'all' ? undefined : activeFilter === 'active',
        }),
      )
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not load prosumers.')
    } finally {
      setIsLoading(false)
    }
  }, [search, activeFilter])

  useEffect(() => {
    void load()
  }, [load])

  /**
   * Registers a prosumer on behalf of a walk-in applicant.
   */
  async function handleCreate(event: FormEvent) {
    event.preventDefault()
    setIsSaving(true)
    setError(null)

    try {
      await prosumersApi.create({ ...form, nic: form.nic.trim().toUpperCase() })
      setNotice(`Prosumer ${form.nic.toUpperCase()} was registered.`)
      setIsFormOpen(false)
      setForm(BLANK_FORM)
      await load()
    } catch (caught) {
      // A NIC or email already in use is rejected by the service.
      setError(caught instanceof ApiError ? caught.message : 'Could not register the prosumer.')
    } finally {
      setIsSaving(false)
    }
  }

  /**
   * Activates or deactivates an account.
   */
  async function handleToggleActive(prosumer: User) {
    setError(null)
    setNotice(null)

    try {
      if (prosumer.isActive) {
        await prosumersApi.deactivate(prosumer.id)
        setNotice(`${prosumer.fullName} was deactivated.`)
      } else {
        await prosumersApi.activate(prosumer.id)
        setNotice(`${prosumer.fullName} was activated and can now sign in.`)
      }

      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not change the account status.')
    }
  }

  return (
    <>
      <PageHeader
        title="Solar Prosumers"
        description="Property owners trading energy with the microgrid, identified by NIC."
        actions={
          isBackoffice ? (
            <button
              type="button"
              onClick={() => {
                setForm(BLANK_FORM)
                setIsFormOpen(true)
              }}
              className="btn-primary"
            >
              + Register prosumer
            </button>
          ) : undefined
        }
      />

      {error && <Alert kind="error" message={error} onDismiss={() => setError(null)} />}
      {notice && <Alert kind="success" message={notice} onDismiss={() => setNotice(null)} />}

      <div className="card">
        <div className="card-header flex-wrap gap-3">
          <div className="flex gap-1">
            {(['all', 'active', 'inactive'] as const).map((value) => (
              <button
                key={value}
                type="button"
                onClick={() => setActiveFilter(value)}
                className={`chip capitalize ${activeFilter === value ? 'chip-active' : ''}`}
              >
                {value}
              </button>
            ))}
          </div>

          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search NIC, name or email…"
            className="field-input max-w-xs"
          />
        </div>

        {isLoading ? (
          <Loading />
        ) : prosumers.length === 0 ? (
          <EmptyState title="No prosumers found" hint="Try a different filter or search term." />
        ) : (
          <>
            <ul className="divide-y divide-line md:hidden">
              {prosumers.map((prosumer) => (
                <li key={prosumer.id} className="p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-medium text-ink-900">{prosumer.fullName}</p>
                      <p className="font-mono text-xs text-ink-400">{prosumer.id}</p>
                    </div>
                    <div className="flex shrink-0 flex-col items-end gap-1">
                      <ActiveBadge isActive={prosumer.isActive} />
                      {prosumer.deactivationRequested && (
                        <span className="badge bg-warn-bg text-warn-fg">Closure requested</span>
                      )}
                    </div>
                  </div>

                  <p className="mt-2 text-xs text-ink-500">{prosumer.email}</p>
                  <p className="text-xs text-ink-400">{prosumer.phone ?? '—'}</p>

                  {isBackoffice && (
                    <button
                      type="button"
                      onClick={() => void handleToggleActive(prosumer)}
                      className={`mt-3 ${prosumer.isActive ? 'btn-danger btn-sm' : 'btn-success btn-sm'}`}
                    >
                      {prosumer.isActive ? 'Deactivate' : 'Activate'}
                    </button>
                  )}
                </li>
              ))}
            </ul>

          <div className="table-wrap hidden md:block">
            <table className="table">
              <thead>
                <tr>
                  <th>NIC</th>
                  <th>Name</th>
                  <th>Contact</th>
                  <th>Registered</th>
                  <th>Status</th>
                  <th className="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {prosumers.map((p) => (
                  <tr key={p.id}>
                    <td className="font-mono text-xs font-medium text-ink-900">{p.id}</td>
                    <td className="font-medium text-ink-900">{p.fullName}</td>
                    <td>
                      <p className="text-xs">{p.email}</p>
                      <p className="text-xs text-ink-400">{p.phone ?? '—'}</p>
                    </td>
                    <td className="whitespace-nowrap text-xs">{formatDate(p.createdAtUtc)}</td>
                    <td>
                      <div className="flex flex-wrap gap-1">
                        <ActiveBadge isActive={p.isActive} />
                        {p.deactivationRequested && (
                          <span className="badge bg-warn-bg text-warn-fg">
                            Closure requested
                          </span>
                        )}
                      </div>
                    </td>
                    <td>
                      <div className="flex justify-end gap-2">
                        {isBackoffice && (
                          <button
                            type="button"
                            onClick={() => void handleToggleActive(p)}
                            className={p.isActive ? 'btn-danger btn-sm' : 'btn-success btn-sm'}
                          >
                            {p.isActive ? 'Deactivate' : 'Activate'}
                          </button>
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
        title="Register a solar prosumer"
        isOpen={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        footer={
          <>
            <button type="button" onClick={() => setIsFormOpen(false)} className="btn-secondary">
              Cancel
            </button>
            <button type="submit" form="prosumer-form" disabled={isSaving} className="btn-primary">
              {isSaving ? 'Saving…' : 'Register prosumer'}
            </button>
          </>
        }
      >
        <form id="prosumer-form" onSubmit={handleCreate} className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="field-label">NIC (primary key)</label>
            <input
              required
              value={form.nic}
              onChange={(e) => setForm({ ...form, nic: e.target.value.toUpperCase() })}
              className="field-input font-mono"
              placeholder="200145600789"
            />
          </div>

          <div>
            <label className="field-label">Full name</label>
            <input
              required
              value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Email</label>
            <input
              required
              type="email"
              value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })}
              className="field-input"
            />
          </div>

          <div>
            <label className="field-label">Phone</label>
            <input
              value={form.phone}
              onChange={(e) => setForm({ ...form, phone: e.target.value })}
              className="field-input"
            />
          </div>

          <div className="sm:col-span-2">
            <label className="field-label">Address</label>
            <input
              value={form.address}
              onChange={(e) => setForm({ ...form, address: e.target.value })}
              className="field-input"
            />
          </div>

          <div className="sm:col-span-2">
            <label className="field-label">Temporary password</label>
            <input
              required
              type="password"
              minLength={6}
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              className="field-input"
              placeholder="At least 6 characters"
            />
          </div>

          <label className="flex items-center gap-2 text-sm sm:col-span-2">
            <input
              type="checkbox"
              checked={form.activateImmediately}
              onChange={(e) => setForm({ ...form, activateImmediately: e.target.checked })}
              className="h-4 w-4 rounded border-line-strong"
            />
            Activate straight away
          </label>

          <p className="text-xs text-ink-400 sm:col-span-2">
            Prosumers who register themselves from the mobile application always arrive
            inactive and appear under Pending Activations.
          </p>
        </form>
      </Modal>
    </>
  )
}
