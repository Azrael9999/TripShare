import axios, { AxiosError } from 'axios'
import { useAuthStore } from '../stores/auth'

const apiBase = (import.meta as any).env.VITE_API_BASE ?? 'http://localhost:8080'

export const http = axios.create({
  baseURL: apiBase.replace(/\/$/, '') + '/api',
  timeout: 20000
})

http.interceptors.request.use((config) => {
  const auth = useAuthStore()
  if (auth.accessToken) {
    config.headers = config.headers ?? {}
    ;(config.headers as any)['Authorization'] = `Bearer ${auth.accessToken}`
  }
  config.headers = config.headers ?? {}
  ;(config.headers as any)['X-Client'] = 'tripshare-web'
  return config
})

http.interceptors.response.use(
  (resp) => resp,
  async (err: AxiosError) => {
    const auth = useAuthStore()
    const original: any = err.config
    if (err?.response?.status === 401 && auth.refreshToken && original && !original._retry) {
      original._retry = true
      try {
        await auth.refresh()
        original.headers = original.headers ?? {}
        original.headers['Authorization'] = `Bearer ${auth.accessToken}`
        return http.request(original)
      } catch {
        auth.logout()
      }
    }
    throw err
  }
)

// --- API types (minimal, extend as needed) ---
export type IdName = { id: string; name: string }

export type RoutePoint = {
  id: string
  orderIndex: number
  type: 'Start' | 'Stop' | 'End'
  lat: number
  lng: number
  displayAddress: string
}

export type TripListItem = {
  id: string
  driverId?: string
  driverName: string
  driverPhotoUrl?: string
  driverVerified?: boolean
  driverIdentityVerified?: boolean
  driverEmailVerified?: boolean
  departureTimeUtc: string
  seatsTotal: number
  currency?: string
  instantBook?: boolean
  bookingCutoffMinutes?: number
  pendingBookingExpiryMinutes?: number
  status?: string
  statusUpdatedAtUtc?: string
  routePoints: RoutePoint[]
  segments?: { id: string; orderIndex: number; fromRoutePointId: string; toRoutePointId: string; price: number; currency: string }[]
}

export type Booking = {
  id: string
  tripId: string
  status: 'Pending' | 'Accepted' | 'Rejected' | 'Cancelled' | 'Expired' | 'Completed'
  progressStatus?: string
  seats: number
  pickupLat?: number
  pickupLng?: number
  dropoffLat?: number
  dropoffLng?: number
  pickupRoutePointId: string
  dropoffRoutePointId: string
  totalPrice: number
  currency: string
  createdAtUtc: string
  statusUpdatedAtUtc?: string
  progressUpdatedAtUtc?: string
}

export type NotificationItem = {
  id: string
  type: string
  title: string
  message: string
  isRead: boolean
  createdAtUtc: string
  relatedTripId?: string
  relatedBookingId?: string
}

export type VehicleProfile = {
  make: string
  model: string
  color: string
  plateNumber: string
  seatCount: number
}
