// -----------------------------------------------------------------------------
// File        : components/Layout.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Application shell: the branded sidebar, the navigation links
//               filtered by the signed in role, and the header showing who is
//               signed in.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import { RoleBadge } from './Ui'
import type { UserRole } from '../types'

interface NavItem {
  to: string
  label: string
  icon: string

  // Which roles may see the link. The API enforces the same restriction, so
  // hiding a link is a convenience, never the actual protection.
  roles: UserRole[]
}

const NAV_ITEMS: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: '◈', roles: ['Backoffice', 'GridOperator'] },
  { to: '/stations', label: 'Microgrid Nodes', icon: '⬡', roles: ['Backoffice', 'GridOperator'] },
  { to: '/reservations', label: 'Reservations', icon: '⇄', roles: ['Backoffice', 'GridOperator'] },
  { to: '/prosumers', label: 'Prosumers', icon: '☀', roles: ['Backoffice', 'GridOperator'] },
  { to: '/activations', label: 'Pending Activations', icon: '⚑', roles: ['Backoffice'] },
  { to: '/users', label: 'System Users', icon: '⚙', roles: ['Backoffice'] },
]

/**
 * Wraps every signed in page with the sidebar and header.
 */
export default function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [isSidebarOpen, setIsSidebarOpen] = useState(false)

  /**
   * Signs the user out and returns them to the login screen.
   */
  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  // Only the links this role is allowed to use are rendered.
  const links = NAV_ITEMS.filter((item) => user && item.roles.includes(user.role))

  return (
    <div className="flex min-h-full">
      {/* Backdrop shown only on small screens while the sidebar is open. */}
      {isSidebarOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          onClick={() => setIsSidebarOpen(false)}
          className="fixed inset-0 z-30 bg-ink-900/40 lg:hidden"
        />
      )}

      <aside
        className={`fixed inset-y-0 left-0 z-40 w-64 transform bg-ink-900 text-ink-200 transition-transform
                    lg:static lg:translate-x-0
                    ${isSidebarOpen ? 'translate-x-0' : '-translate-x-full'}`}
      >
        <div className="flex h-16 items-center gap-3 border-b border-ink-700/60 px-5">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-500 text-lg font-bold text-white">
            ⚡
          </span>
          <div>
            <p className="text-sm font-semibold text-white">VoltShare</p>
            <p className="text-[11px] text-ink-400">Microgrid Trading</p>
          </div>
        </div>

        <nav className="space-y-1 p-3">
          {links.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              onClick={() => setIsSidebarOpen(false)}
              className={({ isActive }) =>
                `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors ${
                  isActive
                    ? 'bg-brand-500 font-medium text-white'
                    : 'text-ink-300 hover:bg-ink-800 hover:text-white'
                }`
              }
            >
              <span className="w-4 text-center">{item.icon}</span>
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-16 items-center justify-between border-b border-ink-200 bg-white px-4 sm:px-6">
          <button
            type="button"
            onClick={() => setIsSidebarOpen(true)}
            aria-label="Open navigation"
            className="rounded p-2 text-ink-600 hover:bg-ink-100 lg:hidden"
          >
            ☰
          </button>

          <div className="ml-auto flex items-center gap-4">
            <div className="text-right">
              <p className="text-sm font-medium text-ink-900">{user?.fullName}</p>
              <p className="text-xs text-ink-500">{user?.email}</p>
            </div>

            {user && <RoleBadge role={user.role} />}

            <button type="button" onClick={handleLogout} className="btn-secondary btn-sm">
              Sign out
            </button>
          </div>
        </header>

        <main className="flex-1 p-4 sm:p-6">
          {/* Each routed page renders here. */}
          <Outlet />
        </main>
      </div>
    </div>
  )
}
