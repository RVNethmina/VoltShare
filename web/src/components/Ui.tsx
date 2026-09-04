// -----------------------------------------------------------------------------
// File        : components/Ui.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Small presentational building blocks shared by every screen:
//               status badges, page headers, empty and loading states, alerts
//               and a modal dialog.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import type { ReactNode } from 'react'
import { useEffect } from 'react'
import type { ReservationStatus, UserRole } from '../types'

/* -------------------------------------------------------------------------- */
/* Status badges                                                              */
/* -------------------------------------------------------------------------- */

// Each reservation status gets its own colour so a long list can be scanned at
// a glance rather than read word by word.
const RESERVATION_STATUS_STYLES: Record<ReservationStatus, string> = {
  Pending: 'bg-warn-bg text-warn-fg',
  Approved: 'bg-info-bg text-info-fg',
  Completed: 'bg-success-bg text-success-fg',
  Cancelled: 'bg-neutral-bg text-neutral-fg',
  Rejected: 'bg-danger-bg text-danger-fg',
}

/** Coloured label for a reservation status. */
export function StatusBadge({ status }: { status: ReservationStatus }) {
  return <span className={`badge ${RESERVATION_STATUS_STYLES[status]}`}>{status}</span>
}

const ROLE_STYLES: Record<UserRole, string> = {
  Backoffice: 'bg-accent-bg text-accent-fg',
  GridOperator: 'bg-info-bg text-info-fg',
  Prosumer: 'bg-success-bg text-success-fg',
}

/** Coloured label for a user role. */
export function RoleBadge({ role }: { role: UserRole }) {
  // The stored value is one word; a space makes "Grid Operator" read properly.
  const label = role === 'GridOperator' ? 'Grid Operator' : role
  return <span className={`badge ${ROLE_STYLES[role]}`}>{label}</span>
}

/** Green or grey label showing whether an account or station is active. */
export function ActiveBadge({ isActive }: { isActive: boolean }) {
  return (
    <span className={`badge ${isActive ? 'bg-success-bg text-success-fg' : 'bg-neutral-bg text-neutral-fg'}`}>
      {isActive ? 'Active' : 'Inactive'}
    </span>
  )
}

/* -------------------------------------------------------------------------- */
/* Page furniture                                                             */
/* -------------------------------------------------------------------------- */

/** Title, optional description and an action area at the top of a page. */
export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string
  description?: string
  actions?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 className="text-2xl font-semibold text-ink-900">{title}</h1>
        {description && <p className="mt-1 text-sm text-ink-500">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

/** Placeholder shown while a request is in flight. */
export function Loading({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-3 py-16 text-sm text-ink-500">
      <span className="h-4 w-4 animate-spin rounded-full border-2 border-ink-300 border-t-brand-500" aria-hidden="true" />
      {label}
    </div>
  )
}

/** Placeholder shown when a list has no rows. */
export function EmptyState({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="py-12 text-center">
      <p className="text-sm font-medium text-ink-700">{title}</p>
      {hint && <p className="mt-1 text-sm text-ink-400">{hint}</p>}
    </div>
  )
}

/**
 * Message banner. Errors carry the wording the API sent back, so the reason a
 * request was refused is always the server's own explanation.
 */
export function Alert({
  kind,
  message,
  onDismiss,
}: {
  kind: 'error' | 'success' | 'info'
  message: string
  onDismiss?: () => void
}) {
  // Each banner uses the same status pair as the badges, so a failure looks
  // the same wherever it is reported and both themes are covered at once.
  const styles = {
    error: 'bg-danger-bg text-danger-fg border-danger-fg/20',
    success: 'bg-success-bg text-success-fg border-success-fg/20',
    info: 'bg-info-bg text-info-fg border-info-fg/20',
  }[kind]

  return (
    <div className={`mb-4 flex items-start justify-between gap-3 rounded-lg border px-4 py-3 text-sm ${styles}`}>
      <span>{message}</span>
      {onDismiss && (
        <button type="button" onClick={onDismiss} className="shrink-0 font-medium opacity-70 hover:opacity-100">
          Dismiss
        </button>
      )}
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/* Modal dialog                                                               */
/* -------------------------------------------------------------------------- */

/**
 * Centred dialog used for the create and edit forms.
 */
export function Modal({
  title,
  isOpen,
  onClose,
  children,
  footer,
}: {
  title: string
  isOpen: boolean
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
}) {
  // Escape closes the dialog, which is what any keyboard user will try first.
  useEffect(() => {
    if (!isOpen) return

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isOpen, onClose])

  if (!isOpen) return null

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/50 p-4 backdrop-blur-[2px] sm:p-8">
      <div className="card w-full max-w-2xl">
        <div className="card-header">
          <h2 className="card-title">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-md p-1.5 text-ink-400 transition-colors hover:bg-ink-100 hover:text-ink-800"
          >
            ✕
          </button>
        </div>

        <div className="px-5 py-4">{children}</div>

        {footer && (
          <div className="flex justify-end gap-2 border-t border-line px-5 py-4">{footer}</div>
        )}
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/* Formatting helpers                                                         */
/* -------------------------------------------------------------------------- */

/**
 * Renders a UTC timestamp from the API in the reader's local time.
 * The API stores and returns UTC throughout; conversion happens only here, at
 * the very edge of the system.
 */
export function formatDateTime(utc: string | null | undefined): string {
  if (!utc) return '—'

  const date = new Date(utc)
  if (Number.isNaN(date.getTime())) return '—'

  return date.toLocaleString(undefined, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** Renders just the date part of a UTC timestamp in local time. */
export function formatDate(utc: string | null | undefined): string {
  if (!utc) return '—'

  const date = new Date(utc)
  if (Number.isNaN(date.getTime())) return '—'

  return date.toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
}
