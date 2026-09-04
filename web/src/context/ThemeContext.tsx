// -----------------------------------------------------------------------------
// File        : context/ThemeContext.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Light and dark theme state for the whole application.
//
//               Three settings are offered: light, dark, and following the
//               operating system. The choice is remembered per browser, and
//               while "system" is selected the page keeps following the system
//               even if it changes while the tab is open.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { createContext, useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'

/** What the user chose, which may be to follow the operating system. */
export type ThemePreference = 'light' | 'dark' | 'system'

/** What is actually being displayed once "system" has been resolved. */
export type ResolvedTheme = 'light' | 'dark'

interface ThemeContextValue {
  preference: ThemePreference
  resolved: ResolvedTheme
  setPreference: (preference: ThemePreference) => void
}

export const ThemeContext = createContext<ThemeContextValue | undefined>(undefined)

const STORAGE_KEY = 'voltshare.theme'

/** Reads the stored choice, defaulting to following the operating system. */
function readStoredPreference(): ThemePreference {
  // Storage access throws outright in some privacy modes, so a failure has to
  // fall back rather than break the application on load.
  try {
    const stored = localStorage.getItem(STORAGE_KEY)

    if (stored === 'light' || stored === 'dark' || stored === 'system') {
      return stored
    }
  } catch {
    // Ignored: the default below is used instead.
  }

  return 'system'
}

/** True when the operating system is currently asking for a dark appearance. */
function systemPrefersDark(): boolean {
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredPreference)
  const [systemIsDark, setSystemIsDark] = useState<boolean>(systemPrefersDark)

  // Follow the operating system while it is being tracked, so switching the
  // machine to dark at sunset changes the page without a reload.
  useEffect(() => {
    const query = window.matchMedia('(prefers-color-scheme: dark)')

    const onChange = (event: MediaQueryListEvent) => setSystemIsDark(event.matches)
    query.addEventListener('change', onChange)

    return () => query.removeEventListener('change', onChange)
  }, [])

  const resolved: ResolvedTheme =
    preference === 'system' ? (systemIsDark ? 'dark' : 'light') : preference

  // One class on the root element drives the whole palette.
  useEffect(() => {
    const root = document.documentElement

    root.classList.toggle('dark', resolved === 'dark')

    // Tells the browser which appearance to use for form controls and
    // scrollbars it draws itself.
    root.style.colorScheme = resolved
  }, [resolved])

  const setPreference = useCallback((next: ThemePreference) => {
    setPreferenceState(next)

    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // The theme still applies for this visit; it just will not be
      // remembered for the next one.
    }
  }, [])

  const value = useMemo(
    () => ({ preference, resolved, setPreference }),
    [preference, resolved, setPreference],
  )

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}
