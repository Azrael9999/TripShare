import { defineStore } from 'pinia'
import { http } from '../lib/api'

export type Me = {
  id: string
  email: string
  emailVerified: boolean
  displayName: string
  photoUrl?: string
  phoneNumber?: string
  isDriver: boolean
  driverVerified: boolean
  identityVerified: boolean
  phoneVerified?: boolean
  role: Role
  ratingAverage?: number
  verified?: boolean
}

type Role = 'user' | 'admin'

type AuthResponse = {
  accessToken: string
  refreshToken: string
  me: Me
}

const LS_KEY = 'tripshare.auth.v1'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    accessToken: '' as string,
    refreshToken: '' as string,
    me: null as Me | null,
    initialized: false
  }),
  getters: {
    isAuthenticated: (s) => !!s.accessToken && !!s.me
  },
  actions: {
    clientContext() {
      const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone
      const locale = navigator.language
      return { timezone, locale }
    },
    normalizeMe(raw: any): Me {
      const rawRole = (raw?.role ?? '').toString().toLowerCase()
      const role: Role = rawRole === 'admin' ? 'admin' : 'user'
      return {
        ...raw,
        role,
        phoneNumber: raw?.phoneNumber ?? raw?.phone ?? null,
        driverVerified: !!raw?.driverVerified,
        identityVerified: !!raw?.identityVerified,
        phoneVerified: !!raw?.phoneVerified
      }
    },

    async init() {
      try {
        const raw = localStorage.getItem(LS_KEY)
        if (raw) {
          const parsed = JSON.parse(raw)
          this.accessToken = parsed.accessToken ?? ''
          this.refreshToken = parsed.refreshToken ?? ''
          this.me = parsed.me ? this.normalizeMe(parsed.me) : null
        }
      } catch {
        // ignore
      }
      this.initialized = true

      // if tokens exist, refresh me silently
      if (this.accessToken && this.me) {
        try {
          await this.loadMe()
        } catch {
          // token may be stale; try refresh once
          if (this.refreshToken) {
            try {
              await this.refresh()
              await this.loadMe()
            } catch {
              this.logout()
            }
          }
        }
      }
    },

    persist() {
      localStorage.setItem(
        LS_KEY,
        JSON.stringify({ accessToken: this.accessToken, refreshToken: this.refreshToken, me: this.me })
      )
    },

    logout() {
      this.accessToken = ''
      this.refreshToken = ''
      this.me = null
      localStorage.removeItem(LS_KEY)
    },

    async resendVerification() {
      await http.post('/users/me/resend-verification')
    },

    async googleLogin(idToken: string) {
      const ctx = this.clientContext()
      const resp = await http.post<AuthResponse>('/auth/google', { idToken, ...ctx })
      this.accessToken = resp.data.accessToken
      this.refreshToken = resp.data.refreshToken
      this.me = this.normalizeMe(resp.data.me)
      this.persist()
    },

    async passwordLogin(email: string, password: string) {
      const ctx = this.clientContext()
      const resp = await http.post<AuthResponse>('/auth/password/login', { email, password, ...ctx })
      this.accessToken = resp.data.accessToken
      this.refreshToken = resp.data.refreshToken
      this.me = this.normalizeMe(resp.data.me)
      this.persist()
    },

    async passwordRegister(email: string, password: string, displayName: string) {
      const ctx = this.clientContext()
      const resp = await http.post<AuthResponse>('/auth/password/register', { email, password, displayName, ...ctx })
      this.accessToken = resp.data.accessToken
      this.refreshToken = resp.data.refreshToken
      this.me = this.normalizeMe(resp.data.me)
      this.persist()
    },

    async requestPasswordReset(email: string) {
      await http.post('/auth/password/reset/request', { email })
    },

    async confirmPasswordReset(token: string, newPassword: string) {
      await http.post('/auth/password/reset/confirm', { token, newPassword })
    },

    async requestSmsOtp(phoneNumber: string) {
      await http.post('/auth/sms/request', { phoneNumber })
    },

    async verifySmsOtp(phoneNumber: string, otp: string) {
      const resp = await http.post<AuthResponse>('/auth/sms/verify', { phoneNumber, otp })
      this.accessToken = resp.data.accessToken
      this.refreshToken = resp.data.refreshToken
      this.me = this.normalizeMe(resp.data.me)
      this.persist()
    },

    async refresh() {
      if (!this.refreshToken) throw new Error('No refresh token')
      const resp = await http.post<AuthResponse>('/auth/refresh', { refreshToken: this.refreshToken })
      this.accessToken = resp.data.accessToken
      this.refreshToken = resp.data.refreshToken
      this.me = this.normalizeMe(resp.data.me)
      this.persist()
    },

    async loadMe() {
      const resp = await http.get<Me>('/users/me')
      this.me = this.normalizeMe(resp.data)
      this.persist()
    }
  }
})
