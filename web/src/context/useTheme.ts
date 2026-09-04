// -----------------------------------------------------------------------------
// File        : context/useTheme.ts
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Hook for reading and changing the theme. Kept separate from the
//               provider so that module exports only components, which keeps
//               React fast refresh reliable during development.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useContext } from 'react'
import { ThemeContext } from './ThemeContext'

/**
 * Returns the current theme and the setter for it.
 * Throws when used outside the provider, so a wiring mistake fails loudly
 * rather than silently returning nothing.
 */
export function useTheme() {
  const context = useContext(ThemeContext)

  if (context === undefined) {
    throw new Error('useTheme must be used inside a ThemeProvider.')
  }

  return context
}
