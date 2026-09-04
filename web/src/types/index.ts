// -----------------------------------------------------------------------------
// File        : types/index.ts
// Project     : VoltShare Web - Smart Solar Microgrid Trading System
// Description : TypeScript mirrors of the contracts returned by the Web API.
//               Keeping them in one file means a change to the API surfaces as
//               a compile error here rather than as a runtime surprise.
// Author      : <IT Number - Member Name>
// Created     : 2026-09-03
// -----------------------------------------------------------------------------

// The three roles the API recognises.
export type UserRole = 'Backoffice' | 'GridOperator' | 'Prosumer'

// Lifecycle states of a reservation, as defined by the API.
export type ReservationStatus =
  | 'Pending'
  | 'Approved'
  | 'Cancelled'
  | 'Rejected'
  | 'Completed'

// Direction of the energy transfer.
export type ReservationType = 'Injection' | 'Withdrawal'

/** An account, whether staff or prosumer. */
export interface User {
  id: string
  fullName: string
  email: string
  phone: string | null
  address: string | null
  role: UserRole
  isActive: boolean
  deactivationRequested: boolean
  createdAtUtc: string
}

/** Successful login result. */
export interface LoginResponse {
  accessToken: string
  expiresAtUtc: string
  user: User
}

/** Daily operating window of a station. */
export interface OperatingHours {
  openTime: string
  closeTime: string
}

/** A solar microgrid node. */
export interface Station {
  id: string
  code: string
  name: string
  addressLine: string
  city: string
  latitude: number
  longitude: number
  capacityKwh: number
  totalBatterySlots: number
  availableBatterySlots: number
  operatingHours: OperatingHours
  isActive: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

/** A station together with its distance from a searched point. */
export interface NearbyStation {
  station: Station
  distanceMeters: number
}

/** A bookable energy transfer window. */
export interface Slot {
  id: string
  stationId: string
  startTimeUtc: string
  endTimeUtc: string
  capacity: number
  bookedCount: number
  remainingCapacity: number
  energyKwhPerSlot: number
  isActive: boolean
}

/**
 * A reservation.
 *
 * canBeModified and canBeCancelled are decided by the server from the twelve
 * hour notice rule. The UI only reads them to enable or disable buttons; it
 * never works the rule out for itself.
 */
export interface Reservation {
  id: string
  reservationNo: string
  prosumerNic: string
  prosumerName: string | null
  stationId: string
  stationName: string | null
  slotId: string
  reservationStartUtc: string
  reservationEndUtc: string
  energyKwh: number
  type: ReservationType
  status: ReservationStatus
  canBeModified: boolean
  canBeCancelled: boolean
  hasQrCode: boolean
  createdAtUtc: string
  cancelledAtUtc: string | null
  completedAtUtc: string | null
}

/** Confirmation returned after a booking action, worded by the server. */
export interface ReservationSummary {
  action: string
  message: string
  reservation: Reservation
}

/** Counts shown to a prosumer. */
export interface ProsumerDashboard {
  prosumerNic: string
  pendingCount: number
  approvedFutureCount: number
  completedCount: number
  cancelledCount: number
  nextReservation: Reservation | null
}

/** Counts and today's workload shown to staff. */
export interface OperatorDashboard {
  pendingCount: number
  approvedFutureCount: number
  completedTodayCount: number
  activeStationCount: number
  todaySchedule: Reservation[]
}
