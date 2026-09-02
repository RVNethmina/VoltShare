// -----------------------------------------------------------------------------
// File        : App.tsx
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : Route table for the whole application. Each protected route is
//               wrapped in the guard, and the back-office only sections declare
//               the roles allowed to reach them. The guard is a navigation
//               convenience: the Web API enforces the same restrictions itself.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { Navigate, Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import ProtectedRoute from './components/ProtectedRoute'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import StationsPage from './pages/StationsPage'
import StationDetailPage from './pages/StationDetailPage'
import ReservationsPage from './pages/ReservationsPage'
import ReservationDetailPage from './pages/ReservationDetailPage'
import ProsumersPage from './pages/ProsumersPage'
import PendingActivationsPage from './pages/PendingActivationsPage'
import UsersPage from './pages/UsersPage'

export default function App() {
  return (
    <Routes>
      {/* The login screen is the only page reachable while signed out. */}
      <Route path="/login" element={<LoginPage />} />

      {/* Everything inside the shell requires a signed in staff account. */}
      <Route
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route path="/dashboard" element={<DashboardPage />} />

        <Route path="/stations" element={<StationsPage />} />
        <Route path="/stations/:id" element={<StationDetailPage />} />

        <Route path="/reservations" element={<ReservationsPage />} />
        <Route path="/reservations/:id" element={<ReservationDetailPage />} />

        <Route path="/prosumers" element={<ProsumersPage />} />

        {/* Account administration belongs to back-office officers only. */}
        <Route
          path="/activations"
          element={
            <ProtectedRoute roles={['Backoffice']}>
              <PendingActivationsPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/users"
          element={
            <ProtectedRoute roles={['Backoffice']}>
              <UsersPage />
            </ProtectedRoute>
          }
        />
      </Route>

      {/* Anything unrecognised goes to the dashboard, which in turn redirects
          to the login page when nobody is signed in. */}
      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )
}
