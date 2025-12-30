import { defineStore } from 'pinia'
import { http } from '../lib/api'
import { telemetryClient } from '../lib/telemetryClient'

type AdSlot = { slot: string; html: string; enabled: boolean }
type AdConfig = { enabled: boolean; frequencyCapPerSession: number; maxSlotsPerPage: number; slots: AdSlot[] }

const SESSION_KEY = 'tripshare.ads.session'
const SESSION_ID_KEY = 'tripshare.ads.sessionId'
const USER_REDUCED_KEY = 'tripshare.ads.reduced'

function loadSessionCounts(): Record<string, number> {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY)
    return raw ? JSON.parse(raw) : {}
  } catch {
    return {}
  }
}

function saveSessionCounts(counts: Record<string, number>) {
  try {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(counts))
  } catch {
    // ignore
  }
}

function getSessionId(): string {
  try {
    const existing = sessionStorage.getItem(SESSION_ID_KEY)
    if (existing) return existing
    const id = crypto.randomUUID()
    sessionStorage.setItem(SESSION_ID_KEY, id)
    return id
  } catch {
    return 'anon'
  }
}

export const useAdsStore = defineStore('ads', {
  state: () => ({
    config: null as AdConfig | null,
    loaded: false,
    pageImpressions: {} as Record<string, number>,
    userReduced: false
  }),
  getters: {
    slotConfig: (state) => (slotName: string) => {
      const candidates = state.config?.slots.filter((s) => s.slot === slotName) ?? []
      if (candidates.length === 0) return undefined
      if (candidates.length === 1) return candidates[0]
      const sessionId = getSessionId()
      const hash = [...(sessionId + slotName)].reduce((acc, ch) => (acc * 31 + ch.charCodeAt(0)) % 100000, 7)
      const index = hash % candidates.length
      return candidates[index]
    }
  },
  actions: {
    async loadConfig() {
      if (this.loaded) return
      try {
        const resp = await http.get<AdConfig>('/ads/config')
        this.config = {
          ...resp.data,
          maxSlotsPerPage: resp.data?.maxSlotsPerPage ?? 0
        }
        this.userReduced = localStorage.getItem(USER_REDUCED_KEY) === '1'
      } catch (e) {
        console.warn('Ads config fetch failed', e)
      } finally {
        this.loaded = true
      }
    },
    resetPageImpressions(pageKey: string) {
      this.pageImpressions = { [pageKey]: 0 }
    },
    canShow(slotName: string, pageKey: string) {
      if (!this.config?.enabled) return false
      if (this.userReduced) return false
      const maxSlots = this.config.maxSlotsPerPage ?? 0
      if (maxSlots > 0 && (this.pageImpressions[pageKey] ?? 0) >= maxSlots) return false
      const slot = this.slotConfig(slotName)
      if (!slot?.enabled) return false
      const cap = this.config.frequencyCapPerSession ?? 0
      if (cap <= 0) return true
      const counts = loadSessionCounts()
      return (counts[slotName] ?? 0) < cap
    },
    async recordImpression(slotName: string, pageKey: string) {
      const counts = loadSessionCounts()
      counts[slotName] = (counts[slotName] ?? 0) + 1
      saveSessionCounts(counts)
      this.pageImpressions[pageKey] = (this.pageImpressions[pageKey] ?? 0) + 1
      try {
        await http.post('/ads/impression', { slot: slotName, sessionId: getSessionId() })
      } catch (e) {
        console.warn('Ad impression post failed', e)
      }
      telemetryClient.trackEvent({ name: 'ad_impression', properties: { slot: slotName } })
    },
    setUserReduced(enable: boolean) {
      this.userReduced = enable
      try {
        if (enable) localStorage.setItem(USER_REDUCED_KEY, '1')
        else localStorage.removeItem(USER_REDUCED_KEY)
      } catch {
        // ignore
      }
    }
  }
})
