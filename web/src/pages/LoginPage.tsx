// -----------------------------------------------------------------------------
// File        : pages/LoginPage.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Sign in screen and the landing page of the web application.
//               The Web API decides whether the credentials are valid and
//               whether the account is active; this screen only shows the
//               outcome and routes the user according to the role it returns.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../context/useAuth'
import { ApiError } from '../api/client'
import { Alert } from '../components/Ui'

export default function LoginPage() {
  const { user, login, isRestoring } = useAuth()
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  // Somebody already signed in has no reason to see this page.
  if (!isRestoring && user) {
    return <Navigate to="/dashboard" replace />
  }

  /**
   * Signs in, then routes by the role the API reports.
   */
  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      const profile = await login(email.trim(), password)

      // A prosumer account belongs to the mobile application. Signing them out
      // again is clearer than dropping them into a back-office screen where
      // every request would be refused.
      if (profile.role === 'Prosumer') {
        setError(
          'Prosumer accounts are served by the VoltShare mobile application. ' +
            'Please sign in there instead.',
        )
        setIsSubmitting(false)
        return
      }

      navigate('/dashboard', { replace: true })
    } catch (caught) {
      // The API explains why a sign in failed, including an account that is
      // awaiting activation, so its wording is shown unchanged.
      setError(
        caught instanceof ApiError
          ? caught.message
          : 'Something went wrong while signing in. Please try again.',
      )
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center bg-gradient-to-br from-ink-900 via-ink-800 to-ink-900 p-4">
      <div className="w-full max-w-4xl overflow-hidden rounded-2xl bg-white shadow-2xl lg:grid lg:grid-cols-2">
        {/* Brand panel, hidden on small screens where the form matters more. */}
        <div className="hidden bg-gradient-to-br from-brand-500 to-brand-700 p-10 text-white lg:flex lg:flex-col lg:justify-between">
          <div>
            <span className="flex h-12 w-12 items-center justify-center rounded-xl bg-white/20 text-2xl">
              ⚡
            </span>
            <h1 className="mt-6 text-3xl font-bold">VoltShare</h1>
            <p className="mt-2 text-brand-100">Smart Solar Microgrid Trading System</p>
          </div>

          <ul className="space-y-3 text-sm text-brand-50">
            <li>◈ Manage microgrid nodes and battery capacity</li>
            <li>⇄ Approve and track energy trading reservations</li>
            <li>☀ Activate and support solar prosumers</li>
          </ul>

          <p className="text-xs text-brand-200">
            Back-office and grid operator access only. Prosumers use the mobile app.
          </p>
        </div>

        <div className="p-8 sm:p-10">
          <h2 className="text-2xl font-semibold text-ink-900">Sign in</h2>
          <p className="mt-1 mb-6 text-sm text-ink-500">
            Use your back-office or grid operator account.
          </p>

          {error && <Alert kind="error" message={error} onDismiss={() => setError(null)} />}

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label htmlFor="email" className="field-label">
                Email address
              </label>
              <input
                id="email"
                type="email"
                required
                autoComplete="username"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="field-input"
                placeholder="name@voltshare.lk"
              />
            </div>

            <div>
              <label htmlFor="password" className="field-label">
                Password
              </label>
              <input
                id="password"
                type="password"
                required
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="field-input"
                placeholder="••••••••"
              />
            </div>

            <button type="submit" disabled={isSubmitting} className="btn-primary w-full">
              {isSubmitting ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}
