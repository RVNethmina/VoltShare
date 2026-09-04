// -----------------------------------------------------------------------------
// File        : components/ProtectedRoute.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Route guard. Sends a signed out visitor to the login page and
//               keeps a signed in user away from screens their role does not
//               cover. This is a navigation convenience only: the Web API
//               refuses the same requests regardless of what the browser shows.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { Navigate, useLocation } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useAuth } from '../context/useAuth'
import { Loading } from './Ui'
import type { UserRole } from '../types'

/**
 * Renders its children only for an allowed, signed in user.
 */
export default function ProtectedRoute({
  children,
  roles,
}: {
  children: ReactNode
  roles?: UserRole[]
}) {
  const { user, isRestoring } = useAuth()
  const location = useLocation()

  // While the stored token is being checked, showing a spinner avoids a flash
  // of the login page for a user who is in fact still signed in.
  if (isRestoring) {
    return <Loading label="Restoring your session…" />
  }

  if (!user) {
    // The attempted address is remembered so the user lands where they meant
    // to go once they have signed in.
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  // A signed in user reaching a page outside their role is sent to their own
  // home rather than shown an error, because the link should not have been
  // available to them in the first place.
  if (roles && !roles.includes(user.role)) {
    return <Navigate to="/dashboard" replace />
  }

  return <>{children}</>
}
