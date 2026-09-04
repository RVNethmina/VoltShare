// -----------------------------------------------------------------------------
// File        : context/AuthContext.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Holds the signed in user for the whole application and restores
//               the session on page load. The only rule this client applies is
//               "is there a valid token"; every permission decision is still
//               made and enforced by the Web API.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { createContext, useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { authApi } from '../api/resources'
import { getToken, setToken } from '../api/client'
import type { User } from '../types'

interface AuthContextValue {
  user: User | null

  // True while the stored token is being exchanged for a profile on start-up,
  // so the router can wait instead of briefly redirecting to the login page.
  isRestoring: boolean

  login: (email: string, password: string) => Promise<User>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

/**
 * Provides the authentication state to the component tree.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [isRestoring, setIsRestoring] = useState(true)

  // On first load, ask the API who the stored token belongs to. This both
  // restores the session after a refresh and proves the token is still valid.
  useEffect(() => {
    let cancelled = false

    async function restore() {
      if (!getToken()) {
        setIsRestoring(false)
        return
      }

      try {
        const profile = await authApi.me()
        if (!cancelled) setUser(profile)
      } catch {
        // The token was rejected, so treat this as signed out. The client has
        // already discarded it.
        if (!cancelled) setUser(null)
      } finally {
        if (!cancelled) setIsRestoring(false)
      }
    }

    void restore()

    // Guards against setting state after the component has been unmounted.
    return () => {
      cancelled = true
    }
  }, [])

  /**
   * Signs in and keeps both the token and the profile.
   */
  const login = useCallback(async (email: string, password: string) => {
    const result = await authApi.login(email, password)

    setToken(result.accessToken)
    setUser(result.user)

    return result.user
  }, [])

  /**
   * Signs out by discarding the token and the profile.
   */
  const logout = useCallback(() => {
    setToken(null)
    setUser(null)
  }, [])

  // Memoised so consumers do not re-render on every provider render.
  const value = useMemo(
    () => ({ user, isRestoring, login, logout }),
    [user, isRestoring, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
