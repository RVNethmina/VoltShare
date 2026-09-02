// -----------------------------------------------------------------------------
// File        : context/useAuth.ts
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Hook for reading the authentication state. Kept in its own file
//               so the context module exports only components, which keeps the
//               React fast refresh behaviour reliable during development.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useContext } from 'react'
import { AuthContext } from './AuthContext'

/**
 * Returns the signed in user and the sign in and sign out actions.
 * Throws when used outside the provider, which turns a wiring mistake into an
 * immediate, obvious error rather than a confusing undefined value.
 */
export function useAuth() {
  const context = useContext(AuthContext)

  if (context === undefined) {
    throw new Error('useAuth must be used inside an AuthProvider.')
  }

  return context
}
