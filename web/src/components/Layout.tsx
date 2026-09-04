// -----------------------------------------------------------------------------
// File        : components/Layout.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Application shell: the branded navigation rail, the links
//               filtered by the signed in role, the theme switch and the
//               account menu.
//
//               The rail is permanent from the large breakpoint upwards and
//               becomes a slide over drawer below it, so the same markup works
//               from a phone to a desktop.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import ThemeToggle from './ThemeToggle'
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

export default function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)

  // Close the drawer whenever the route changes, so tapping a link on a phone
  // does not leave the panel covering the page that just loaded.
  useEffect(() => {
    setIsDrawerOpen(false)
  }, [location.pathname])

  // Escape closes the drawer, which is what any keyboard user will try first.
  useEffect(() => {
    if (!isDrawerOpen) return

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsDrawerOpen(false)
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [isDrawerOpen])

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  const links = NAV_ITEMS.filter((item) => user && item.roles.includes(user.role))

  // Initials stand in for an avatar without needing an uploaded image.
  const initials = (user?.fullName ?? '')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')

  return (
    <div className="flex min-h-full bg-canvas">
      {/* Dimmed backdrop, only present while the drawer is open on small screens. */}
      {isDrawerOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          onClick={() => setIsDrawerOpen(false)}
          className="fixed inset-0 z-30 bg-black/50 backdrop-blur-[2px] lg:hidden"
        />
      )}

      <aside
        className={`fixed inset-y-0 left-0 z-40 flex w-64 flex-col bg-rail text-rail-fg
                    transition-transform duration-200 ease-out
                    lg:static lg:translate-x-0
                    ${isDrawerOpen ? 'translate-x-0' : '-translate-x-full'}`}
      >
        <div className="flex h-16 shrink-0 items-center gap-3 px-5">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-brand-500 text-lg font-bold text-brand-fg">
            ⚡
          </span>
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-white">VoltShare</p>
            <p className="truncate text-[11px] text-rail-muted">Microgrid Trading</p>
          </div>
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto p-3">
          {links.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors duration-150 ${
                  isActive
                    ? 'bg-brand-500 font-medium text-brand-fg'
                    : 'text-rail-fg/80 hover:bg-rail-hover hover:text-white'
                }`
              }
            >
              <span className="w-4 shrink-0 text-center">{item.icon}</span>
              <span className="truncate">{item.label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="shrink-0 border-t border-white/10 p-3">
          <p className="px-2 text-[11px] text-rail-muted">
            Signed in as {user?.role === 'GridOperator' ? 'Grid Operator' : user?.role}
          </p>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex h-16 shrink-0 items-center gap-3 border-b border-line bg-surface/85 px-4 backdrop-blur-md sm:px-6">
          <button
            type="button"
            onClick={() => setIsDrawerOpen(true)}
            aria-label="Open navigation"
            className="btn-ghost -ml-2 h-9 w-9 rounded-lg p-0 lg:hidden"
          >
            ☰
          </button>

          <div className="ml-auto flex items-center gap-2 sm:gap-3">
            <ThemeToggle />

            <div className="hidden h-6 w-px bg-line sm:block" />

            {user && (
              <div className="hidden items-center gap-2.5 sm:flex">
                <span className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-soft text-xs font-semibold text-brand-soft-fg">
                  {initials || '?'}
                </span>
                <div className="min-w-0 max-w-[11rem]">
                  <p className="truncate text-sm font-medium text-ink-900">{user.fullName}</p>
                  <p className="truncate text-xs text-ink-500">{user.email}</p>
                </div>
              </div>
            )}

            {user && <RoleBadge role={user.role} />}

            <button type="button" onClick={handleLogout} className="btn-secondary btn-sm">
              Sign out
            </button>
          </div>
        </header>

        <main className="flex-1 p-4 sm:p-6">
          {/* Capped so long tables stay readable on a very wide monitor. */}
          <div className="mx-auto w-full max-w-[92rem]">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
