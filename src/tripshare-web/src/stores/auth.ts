import { defineStore } from 'pinia'
import { http } from '../lib/api'

export type Me = {
  id: string
  email: string
  emailVerified: boolean
  displayName: string
  photoUrl?: string
  isDriver: boolean
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
    normalizeMe(raw: any): Me {
      const rawRole = (raw?.role ?? '').toString().toLowerCase()
      const role: Role = rawRole === 'admin' ? 'admin' : 'user'
      return { ...raw, role }
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

    async googleLogin(idToken: string) {
      const resp = await http.post<AuthResponse>('/auth/google', { idToken })
      this.accessToken = resp.data.accessToken
      this.refreshToken = resp.data.refreshToken
      this.me = this.normalizeMe(resp.data.me)
      this.persist()
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
