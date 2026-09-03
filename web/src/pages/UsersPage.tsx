// -----------------------------------------------------------------------------
// File        : pages/UsersPage.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Back-office administration of the two web application roles,
//               Backoffice and Grid Operator. The whole page is restricted to
//               back-office officers, and the service refuses these calls from
//               any other role regardless of what the browser allows.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { usersApi } from '../api/resources'
import type { CreateStaffUserPayload } from '../api/resources'
import { ApiError } from '../api/client'
import { useAuth } from '../context/useAuth'
import {
  ActiveBadge,
  Alert,
  EmptyState,
  Loading,
  Modal,
  PageHeader,
  RoleBadge,
  formatDate,
} from '../components/Ui'
import type { User } from '../types'

const BLANK_FORM: CreateStaffUserPayload = {
  fullName: '',
  email: '',
  phone: '',
  role: 'GridOperator',
  password: '',
}

export default function UsersPage() {
  const { user: currentUser } = useAuth()

  const [users, setUsers] = useState<User[]>([])
  const [roleFilter, setRoleFilter] = useState<'' | 'Backoffice' | 'GridOperator'>('')
  const [search, setSearch] = useState('')

  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [isFormOpen, setIsFormOpen] = useState(false)
  const [form, setForm] = useState(BLANK_FORM)
  const [isSaving, setIsSaving] = useState(false)

  /**
   * Loads the staff accounts. Prosumers are excluded because they are managed
   * on their own screen, keyed by NIC.
   */
  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)

    try {
      const all = await usersApi.list({
        role: roleFilter || undefined,
        search: search.trim() || undefined,
      })

      setUsers(roleFilter ? all : all.filter((u) => u.role !== 'Prosumer'))
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not load system users.')
    } finally {
      setIsLoading(false)
    }
  }, [roleFilter, search])

  useEffect(() => {
    void load()
  }, [load])

  /**
   * Creates a Backoffice or Grid Operator account.
   */
  async function handleCreate(event: FormEvent) {
    event.preventDefault()
    setIsSaving(true)
    setError(null)

    try {
      await usersApi.create(form)
      setNotice(`${form.fullName} was added as a ${form.role}.`)
      setIsFormOpen(false)
      setForm(BLANK_FORM)
      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not create the account.')
    } finally {
      setIsSaving(false)
    }
  }

  /**
   * Activates or deactivates a staff account.
   */
  async function handleToggleActive(target: User) {
    setError(null)
    setNotice(null)

    try {
      if (target.isActive) {
        await usersApi.deactivate(target.id)
        setNotice(`${target.fullName} was deactivated.`)
      } else {
        await usersApi.activate(target.id)
        setNotice(`${target.fullName} was activated.`)
      }

      await load()
    } catch (caught) {
      setError(caught instanceof ApiError ? caught.message : 'Could not change the account status.')
    }
  }

  return (
    <>
      <PageHeader
        title="System Users"
        description="Back-office officers and grid operators who use the web application."
        actions={
          <button
            type="button"
            onClick={() => {
              setForm(BLANK_FORM)
              setIsFormOpen(true)
            }}
            className="btn-primary"
          >
            + Add user
          </button>
        }
      />

      {error && <Alert kind="error" message={error} onDismiss={() => setError(null)} />}
      {notice && <Alert kind="success" message={notice} onDismiss={() => setNotice(null)} />}

      <div className="card">
        <div className="card-header flex-wrap gap-3">
          <div className="flex gap-1">
            {([
              ['', 'All'],
              ['Backoffice', 'Back-office'],
              ['GridOperator', 'Operators'],
            ] as const).map(([value, label]) => (
              <button
                key={value || 'all'}
                type="button"
                onClick={() => setRoleFilter(value)}
                className={`chip ${roleFilter === value ? 'chip-active' : ''}`}
              >
                {label}
              </button>
            ))}
          </div>

          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search name or email…"
            className="field-input max-w-xs"
          />
        </div>

        {isLoading ? (
          <Loading />
        ) : users.length === 0 ? (
          <EmptyState title="No system users found" hint="Add a back-office officer or operator." />
        ) : (
          <>
            <ul className="divide-y divide-line md:hidden">
              {users.map((u) => (
                <li key={u.id} className="p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-medium text-ink-900">
                        {u.fullName}
                        {u.id === currentUser?.id && (
                          <span className="ml-2 text-xs font-normal text-ink-400">(you)</span>
                        )}
                      </p>
                      <p className="text-xs text-ink-500">{u.email}</p>
                    </div>
                    <ActiveBadge isActive={u.isActive} />
                  </div>

                  <div className="mt-3 flex flex-wrap items-center gap-2">
                    <RoleBadge role={u.role} />
                    {u.id !== currentUser?.id && (
                      <button
                        type="button"
                        onClick={() => void handleToggleActive(u)}
                        className={u.isActive ? 'btn-danger btn-sm' : 'btn-success btn-sm'}
                      >
                        {u.isActive ? 'Deactivate' : 'Activate'}
                      </button>
                    )}
                  </div>
                </li>
              ))}
            </ul>

          <div className="table-wrap hidden md:block">
            <table className="table">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Created</th>
                  <th>Status</th>
                  <th className="text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id}>
                    <td className="font-medium text-ink-900">
                      {u.fullName}
                      {u.id === currentUser?.id && (
                        <span className="ml-2 text-xs font-normal text-ink-400">(you)</span>
                      )}
                    </td>
                    <td className="text-xs">{u.email}</td>
                    <td>
                      <RoleBadge role={u.role} />
                    </td>
                    <td className="whitespace-nowrap text-xs">{formatDate(u.createdAtUtc)}</td>
                    <td>
                      <ActiveBadge isActive={u.isActive} />
                    </td>
                    <td className="text-right">
                      {/* Deactivating your own account would immediately lock
                          you out, so the option is withheld. */}
                      {u.id !== currentUser?.id && (
                        <button
                          type="button"
                          onClick={() => void handleToggleActive(u)}
                          className={u.isActive ? 'btn-danger btn-sm' : 'btn-success btn-sm'}
                        >
                          {u.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      )}
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
        title="Add a system user"
        isOpen={isFormOpen}
        onClose={() => setIsFormOpen(false)}
        footer={
          <>
            <button type="button" onClick={() => setIsFormOpen(false)} className="btn-secondary">
              Cancel
            </button>
            <button type="submit" form="user-form" disabled={isSaving} className="btn-primary">
              {isSaving ? 'Saving…' : 'Create user'}
            </button>
          </>
        }
      >
        <form id="user-form" onSubmit={handleCreate} className="grid gap-4 sm:grid-cols-2">
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
            <label className="field-label">Role</label>
            <select
              value={form.role}
              onChange={(e) =>
                setForm({ ...form, role: e.target.value as 'Backoffice' | 'GridOperator' })
              }
              className="field-input"
            >
              <option value="GridOperator">Grid Operator</option>
              <option value="Backoffice">Back-office</option>
            </select>
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
            <label className="field-label">Password</label>
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

          <p className="text-xs text-ink-400 sm:col-span-2">
            Only these two roles can be created here. Prosumer accounts are keyed by NIC and
            are managed on the Prosumers screen.
          </p>
        </form>
      </Modal>
    </>
  )
}
