// -----------------------------------------------------------------------------
// File        : api/resources.ts
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : One typed function per Web API endpoint the back-office uses.
//               These are thin wrappers on purpose: no decisions are made here,
//               they only describe which endpoint a screen is calling.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

import { api, buildQuery } from './client'
import type {
  LoginResponse,
  NearbyStation,
  OperatorDashboard,
  Reservation,
  ReservationSummary,
  Slot,
  Station,
  User,
} from '../types'

/* -------------------------------------------------------------------------- */
/* Authentication                                                             */
/* -------------------------------------------------------------------------- */

export const authApi = {
  /** Signs in and returns the token plus the caller's profile. */
  login: (email: string, password: string) =>
    api.post<LoginResponse>('/auth/login', { email, password }),

  /** Restores the session on page load using the stored token. */
  me: () => api.get<User>('/auth/me'),
}

/* -------------------------------------------------------------------------- */
/* Staff accounts                                                             */
/* -------------------------------------------------------------------------- */

export interface CreateStaffUserPayload {
  fullName: string
  email: string
  phone?: string
  role: 'Backoffice' | 'GridOperator'
  password: string
}

export interface UpdateUserPayload {
  fullName: string
  phone?: string
  address?: string
}

export const usersApi = {
  list: (filters: { role?: string; isActive?: boolean; search?: string } = {}) =>
    api.get<User[]>(`/users${buildQuery(filters)}`),

  get: (id: string) => api.get<User>(`/users/${id}`),

  create: (payload: CreateStaffUserPayload) => api.post<User>('/users', payload),

  update: (id: string, payload: UpdateUserPayload) => api.put<User>(`/users/${id}`, payload),

  activate: (id: string) => api.patch<User>(`/users/${id}/activate`),

  deactivate: (id: string) => api.patch<User>(`/users/${id}/deactivate`),
}

/* -------------------------------------------------------------------------- */
/* Prosumers                                                                  */
/* -------------------------------------------------------------------------- */

export interface CreateProsumerPayload {
  nic: string
  fullName: string
  email: string
  phone?: string
  address?: string
  password: string
  activateImmediately: boolean
}

export const prosumersApi = {
  list: (filters: { isActive?: boolean; search?: string } = {}) =>
    api.get<User[]>(`/prosumers${buildQuery(filters)}`),

  /** Prosumers who have registered from the mobile app and await activation. */
  pending: () => api.get<User[]>('/prosumers/pending'),

  /** Prosumers who have asked for their account to be closed. */
  deactivationRequests: () => api.get<User[]>('/prosumers/deactivation-requests'),

  get: (nic: string) => api.get<User>(`/prosumers/${nic}`),

  create: (payload: CreateProsumerPayload) => api.post<User>('/prosumers', payload),

  update: (nic: string, payload: UpdateUserPayload) =>
    api.put<User>(`/prosumers/${nic}`, payload),

  activate: (nic: string) => api.patch<User>(`/prosumers/${nic}/activate`),

  deactivate: (nic: string) => api.patch<User>(`/prosumers/${nic}/deactivate`),
}

/* -------------------------------------------------------------------------- */
/* Stations and booking windows                                               */
/* -------------------------------------------------------------------------- */

export interface StationPayload {
  code?: string
  name: string
  addressLine: string
  city: string
  latitude: number
  longitude: number
  capacityKwh: number
  totalBatterySlots: number
  availableBatterySlots?: number
  openTime: string
  closeTime: string
}

export interface SlotPayload {
  startTimeUtc: string
  endTimeUtc: string
  capacity: number
  energyKwhPerSlot: number
  isActive?: boolean
}

export const stationsApi = {
  list: (filters: { isActive?: boolean; city?: string; search?: string } = {}) =>
    api.get<Station[]>(`/stations${buildQuery(filters)}`),

  get: (id: string) => api.get<Station>(`/stations/${id}`),

  /** Server side geographic search; the distance is calculated by MongoDB. */
  nearby: (lat: number, lng: number, radiusKm = 10, limit = 50) =>
    api.get<NearbyStation[]>(`/stations/nearby${buildQuery({ lat, lng, radiusKm, limit })}`),

  create: (payload: StationPayload) => api.post<Station>('/stations', payload),

  update: (id: string, payload: StationPayload) => api.put<Station>(`/stations/${id}`, payload),

  activate: (id: string) => api.patch<Station>(`/stations/${id}/activate`),

  /** Refused by the API while the station holds active reservations. */
  deactivate: (id: string) => api.patch<Station>(`/stations/${id}/deactivate`),

  updateBatterySlots: (id: string, availableBatterySlots: number) =>
    api.patch<Station>(`/stations/${id}/battery-slots`, { availableBatterySlots }),

  listSlots: (id: string, filters: { from?: string; to?: string; isActive?: boolean } = {}) =>
    api.get<Slot[]>(`/stations/${id}/slots${buildQuery(filters)}`),

  createSlot: (id: string, payload: SlotPayload) =>
    api.post<Slot>(`/stations/${id}/slots`, payload),
}

export const slotsApi = {
  get: (id: string) => api.get<Slot>(`/slots/${id}`),

  update: (id: string, payload: SlotPayload) => api.put<Slot>(`/slots/${id}`, payload),

  /** Refused by the API while prosumers hold reservations against the window. */
  remove: (id: string) => api.del<void>(`/slots/${id}`),
}

/* -------------------------------------------------------------------------- */
/* Reservations                                                               */
/* -------------------------------------------------------------------------- */

export interface ReservationFilters {
  nic?: string
  stationId?: string
  status?: string
  from?: string
  to?: string
  q?: string
  limit?: number
}

export const reservationsApi = {
  search: (filters: ReservationFilters = {}) =>
    api.get<Reservation[]>(`/reservations${buildQuery({ ...filters })}`),

  pending: () => api.get<Reservation[]>('/reservations/pending'),

  get: (id: string) => api.get<Reservation>(`/reservations/${id}`),

  create: (slotId: string, type: string, prosumerNic?: string) =>
    api.post<ReservationSummary>('/reservations', { slotId, type, prosumerNic }),

  update: (id: string, slotId: string, type: string) =>
    api.put<ReservationSummary>(`/reservations/${id}`, { slotId, type }),

  cancel: (id: string) => api.patch<ReservationSummary>(`/reservations/${id}/cancel`),

  approve: (id: string) => api.patch<Reservation>(`/reservations/${id}/approve`),

  reject: (id: string) => api.patch<Reservation>(`/reservations/${id}/reject`),

  /** Used by an operator after scanning a prosumer QR code. */
  verifyQr: (token: string) => api.post<Reservation>('/reservations/verify-qr', { token }),

  complete: (id: string) => api.post<ReservationSummary>(`/reservations/${id}/complete`),
}

/* -------------------------------------------------------------------------- */
/* Dashboard                                                                  */
/* -------------------------------------------------------------------------- */

export const dashboardApi = {
  /** Counts for staff, computed entirely by the server. */
  operator: () => api.get<OperatorDashboard>('/dashboard/operator'),
}
