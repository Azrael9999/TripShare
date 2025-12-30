import { defineStore } from 'pinia'
import { http } from '../lib/api'
import { telemetryClient } from '../lib/telemetryClient'

type AdSlot = { slot: string; html: string; enabled: boolean }
type AdConfig = { enabled: boolean; frequencyCapPerSession: number; slots: AdSlot[] }

const SESSION_KEY = 'tripshare.ads.session'

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

export const useAdsStore = defineStore('ads', {
  state: () => ({
    config: null as AdConfig | null,
    loaded: false
  }),
  getters: {
    slotConfig: (state) => (slotName: string) => state.config?.slots.find((s) => s.slot === slotName)
  },
  actions: {
    async loadConfig() {
      if (this.loaded) return
      try {
        const resp = await http.get<AdConfig>('/ads/config')
        this.config = resp.data
      } catch (e) {
        console.warn('Ads config fetch failed', e)
      } finally {
        this.loaded = true
      }
    },
    canShow(slotName: string) {
      if (!this.config?.enabled) return false
      const slot = this.slotConfig(slotName)
      if (!slot?.enabled) return false
      const cap = this.config.frequencyCapPerSession ?? 0
      if (cap <= 0) return true
      const counts = loadSessionCounts()
      return (counts[slotName] ?? 0) < cap
    },
    recordImpression(slotName: string) {
      const counts = loadSessionCounts()
      counts[slotName] = (counts[slotName] ?? 0) + 1
      saveSessionCounts(counts)
      telemetryClient.trackEvent({ name: 'ad_impression', properties: { slot: slotName } })
    }
  }
})
