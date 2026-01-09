<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-4">
      <div class="card p-5">
        <div class="text-xl font-semibold">Site settings</div>
        <p class="text-sm text-slate-600 mt-1">Platform-wide configuration for ads, branding, maps, and verification.</p>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="font-semibold">Driver verification gate</div>
            <p class="text-sm text-slate-600 mt-1">
              Require admin-verified drivers before creating trips. Applies platform-wide.
            </p>
          </div>
          <button class="btn-outline" @click="toggleDriverVerification" :disabled="settingsSaving">
            {{ driverVerificationRequired ? 'Disable' : 'Enable' }}
          </button>
        </div>
        <p class="text-sm text-slate-600 mt-3">
          Current state: <span class="badge">{{ driverVerificationRequired ? 'Required' : 'Off' }}</span>
        </p>
        <p v-if="settingsMsg" class="text-sm text-emerald-700 mt-2">{{ settingsMsg }}</p>
        <p v-if="settingsErr" class="text-sm text-red-600 mt-2">{{ settingsErr }}</p>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="font-semibold">Branding assets</div>
            <p class="text-sm text-slate-600 mt-1">Upload logos and banners used across the site.</p>
          </div>
          <button class="btn-primary" :disabled="brandingSaving" @click="saveBranding">
            <span v-if="brandingSaving">Saving…</span>
            <span v-else>Save branding</span>
          </button>
        </div>

        <div class="mt-4 grid grid-cols-1 md:grid-cols-2 gap-4">
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Logo</div>
            <img v-if="branding.logoUrl" :src="branding.logoUrl" class="h-12 object-contain rounded-xl border border-slate-100 bg-white p-2" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('logoUrl', $event)" />
          </div>
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Hero banner</div>
            <img v-if="branding.heroImageUrl" :src="branding.heroImageUrl" class="h-20 w-full object-cover rounded-xl border border-slate-100" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('heroImageUrl', $event)" />
          </div>
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Map overlay</div>
            <img v-if="branding.mapOverlayUrl" :src="branding.mapOverlayUrl" class="h-20 w-full object-cover rounded-xl border border-slate-100" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('mapOverlayUrl', $event)" />
          </div>
          <div class="space-y-2">
            <div class="text-xs text-slate-600">Login illustration</div>
            <img v-if="branding.loginIllustrationUrl" :src="branding.loginIllustrationUrl" class="h-20 w-full object-cover rounded-xl border border-slate-100" />
            <input type="file" accept="image/*" class="input" @change="onBrandFile('loginIllustrationUrl', $event)" />
          </div>
        </div>

        <p v-if="brandingMsg" class="text-sm text-emerald-700 mt-3">{{ brandingMsg }}</p>
        <p v-if="brandingErr" class="text-sm text-red-600 mt-3">{{ brandingErr }}</p>
      </div>

      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="font-semibold">Google Maps API</div>
            <p class="text-sm text-slate-600 mt-1">Update the Maps API key used across the site.</p>
          </div>
          <button class="btn-primary" :disabled="mapsSaving" @click="saveMapsApiKey">
            <span v-if="mapsSaving">Saving…</span>
            <span v-else>Save Maps key</span>
          </button>
        </div>
        <div class="mt-4">
          <label class="text-xs text-slate-600">API key</label>
          <input v-model="mapsApiKey" type="text" class="input mt-1" placeholder="Paste Google Maps API key" />
          <p class="text-xs text-slate-500 mt-2">Changes apply immediately to map-based features.</p>
        </div>
        <p v-if="mapsMsg" class="text-sm text-emerald-700 mt-3">{{ mapsMsg }}</p>
        <p v-if="mapsErr" class="text-sm text-red-600 mt-3">{{ mapsErr }}</p>
      </div>

      <div class="card p-5">
        <div class="flex items-center justify-between gap-4">
          <div>
            <div class="font-semibold">Ad configuration</div>
            <p class="text-sm text-slate-600 mt-1">Control slots, enablement, and frequency caps.</p>
          </div>
          <div class="flex items-center gap-3">
            <label class="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" v-model="adsEnabled" /> Enable ads
            </label>
            <button class="btn-outline" @click="loadAdConfig">Reload</button>
          </div>
        </div>

        <div class="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3">
          <div>
            <label class="text-xs text-slate-600">Frequency cap (per session)</label>
            <input v-model.number="adsFrequency" type="number" min="0" max="100" class="input mt-1" />
          </div>
          <div>
            <label class="text-xs text-slate-600">Max slots rendered per page</label>
            <input v-model.number="adsMaxSlotsPerPage" type="number" min="0" max="10" class="input mt-1" />
            <p class="text-xs text-slate-500 mt-1">0 means unlimited, otherwise caps how many ad blocks can appear on a single page.</p>
          </div>
        </div>

        <div class="mt-4 rounded-2xl border border-slate-100 p-4 bg-white">
          <div class="font-semibold">Google Ads settings</div>
          <p class="text-xs text-slate-500 mt-1">Configure Google Ads client IDs and script URLs without code changes.</p>
          <div class="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label class="text-xs text-slate-600">Client ID</label>
              <input v-model="googleAdsClientId" class="input mt-1" placeholder="ca-pub-xxxxxxxxxxxxxxxx" />
            </div>
            <div>
              <label class="text-xs text-slate-600">Slot ID</label>
              <input v-model="googleAdsSlotId" class="input mt-1" placeholder="Optional slot identifier" />
            </div>
            <div class="md:col-span-2">
              <label class="text-xs text-slate-600">Script URL</label>
              <input v-model="googleAdsScriptUrl" class="input mt-1" placeholder="https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js" />
            </div>
          </div>
        </div>

        <div class="mt-4 space-y-3">
          <div class="flex items-center justify-between">
            <div class="font-semibold">Slots</div>
            <button class="btn-outline" @click="addAdSlot">Add slot</button>
          </div>

          <div v-for="(slot, idx) in adSlots" :key="slot.slot + idx" class="rounded-2xl border border-slate-100 p-4 bg-white">
            <div class="flex flex-col gap-2">
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="text-xs text-slate-600">Slot key</label>
                  <input v-model="slot.slot" class="input mt-1" />
                </div>
                <div class="flex items-center gap-2 mt-1 sm:mt-6">
                  <input type="checkbox" v-model="slot.enabled" />
                  <span class="text-sm text-slate-700">Enabled</span>
                </div>
              </div>
              <div>
                <label class="text-xs text-slate-600">Markup</label>
                <textarea v-model="slot.html" class="input mt-1" rows="3"></textarea>
              </div>
              <div class="flex justify-end">
                <button class="btn-ghost text-sm" @click="removeAdSlot(idx)">Remove</button>
              </div>
            </div>
          </div>
        </div>

        <div class="mt-4 flex items-center gap-3">
          <button class="btn-primary" :disabled="adsSaving" @click="saveAdConfig">{{ adsSaving ? 'Saving…' : 'Save config' }}</button>
          <p v-if="adsMsg" class="text-sm text-emerald-700">{{ adsMsg }}</p>
          <p v-if="adsErr" class="text-sm text-red-600">{{ adsErr }}</p>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot name="admin-sidebar" />
      <div class="card p-5">
        <div class="font-semibold">Note</div>
        <p class="text-sm text-slate-600 mt-2">Site settings apply immediately across the platform.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { http } from '../../lib/api'
import { applyBrandingConfig, type BrandingConfig } from '../../lib/branding'
import AdSlot from '../../components/AdSlot.vue'

type AdSlotConfig = { slot: string; html: string; enabled: boolean }

const driverVerificationRequired = ref(false)
const settingsMsg = ref('')
const settingsErr = ref('')
const settingsSaving = ref(false)
const branding = ref<BrandingConfig>({})
const brandingSaving = ref(false)
const brandingMsg = ref('')
const brandingErr = ref('')
const mapsApiKey = ref('')
const mapsSaving = ref(false)
const mapsMsg = ref('')
const mapsErr = ref('')
const adsEnabled = ref(false)
const adsFrequency = ref(3)
const adsMaxSlotsPerPage = ref(3)
const googleAdsClientId = ref('')
const googleAdsScriptUrl = ref('')
const googleAdsSlotId = ref('')
const adsMsg = ref('')
const adsErr = ref('')
const adsSaving = ref(false)
const adSlots = reactive<AdSlotConfig[]>([])

function normalizeSlots(raw: any[] = []) {
  adSlots.splice(0, adSlots.length)
  raw.forEach((s) => adSlots.push({ slot: s.slot ?? '', html: s.html ?? '', enabled: !!s.enabled }))
}

async function loadSettings() {
  const resp = await http.get('/admin/settings')
  driverVerificationRequired.value = !!resp.data?.driverVerificationRequired
}

async function toggleDriverVerification() {
  settingsMsg.value = ''
  settingsErr.value = ''
  settingsSaving.value = true
  try {
    const newVal = !driverVerificationRequired.value
    await http.post('/admin/settings/driver-verification', { required: newVal })
    driverVerificationRequired.value = newVal
    settingsMsg.value = `Driver verification requirement ${newVal ? 'enabled' : 'disabled'}.`
  } catch (e:any) {
    settingsErr.value = e?.response?.data?.message ?? 'Failed to update setting.'
  } finally {
    settingsSaving.value = false
  }
}

async function loadBranding() {
  try {
    const resp = await http.get<BrandingConfig | null>('/admin/branding')
    branding.value = resp.data ?? {}
  } catch {
    branding.value = {}
  }
}

async function loadMapsApiKey() {
  try {
    const resp = await http.get<{ apiKey?: string | null; ApiKey?: string | null }>('/admin/maps/config')
    mapsApiKey.value = (resp.data?.apiKey ?? resp.data?.ApiKey ?? '') as string
  } catch {
    mapsApiKey.value = ''
  }
}

function onBrandFile(key: keyof BrandingConfig, event: Event) {
  const input = event.target as HTMLInputElement | null
  const file = input?.files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = () => {
    branding.value = { ...branding.value, [key]: String(reader.result) }
  }
  reader.readAsDataURL(file)
}

async function saveBranding() {
  brandingMsg.value = ''
  brandingErr.value = ''
  brandingSaving.value = true
  try {
    await http.post('/admin/branding', branding.value)
    applyBrandingConfig(branding.value)
    brandingMsg.value = 'Branding updated.'
  } catch (e:any) {
    brandingErr.value = e?.response?.data?.message ?? 'Failed to update branding.'
  } finally {
    brandingSaving.value = false
  }
}

async function saveMapsApiKey() {
  mapsMsg.value = ''
  mapsErr.value = ''
  mapsSaving.value = true
  try {
    await http.post('/admin/maps/config', { apiKey: mapsApiKey.value || null })
    mapsMsg.value = 'Maps API key updated.'
  } catch (e:any) {
    mapsErr.value = e?.response?.data?.message ?? 'Failed to update Maps API key.'
  } finally {
    mapsSaving.value = false
  }
}

async function loadAdConfig() {
  adsMsg.value = ''
  adsErr.value = ''
  try {
    const resp = await http.get('/admin/ads/config')
    adsEnabled.value = !!resp.data?.enabled
    adsFrequency.value = resp.data?.frequencyCapPerSession ?? 3
    adsMaxSlotsPerPage.value = resp.data?.maxSlotsPerPage ?? 3
    googleAdsClientId.value = resp.data?.googleAdsClientId ?? ''
    googleAdsScriptUrl.value = resp.data?.googleAdsScriptUrl ?? ''
    googleAdsSlotId.value = resp.data?.googleAdsSlotId ?? ''
    normalizeSlots(resp.data?.slots ?? [])
  } catch (e: any) {
    adsErr.value = e?.response?.data?.message ?? 'Failed to load.'
  }
}

async function saveAdConfig() {
  adsSaving.value = true
  adsMsg.value = ''
  adsErr.value = ''
  try {
    await http.post('/admin/ads/config', {
      enabled: adsEnabled.value,
      frequencyCapPerSession: adsFrequency.value,
      maxSlotsPerPage: adsMaxSlotsPerPage.value,
      googleAdsClientId: googleAdsClientId.value || null,
      googleAdsScriptUrl: googleAdsScriptUrl.value || null,
      googleAdsSlotId: googleAdsSlotId.value || null,
      slots: adSlots.map((s) => ({ ...s }))
    })
    adsMsg.value = 'Saved.'
  } catch (e: any) {
    adsErr.value = e?.response?.data?.message ?? 'Failed to save.'
  } finally {
    adsSaving.value = false
  }
}

function addAdSlot() {
  adSlots.push({ slot: 'new-slot', html: '<div>Ad</div>', enabled: true })
}

function removeAdSlot(idx: number) {
  adSlots.splice(idx, 1)
}

async function load() {
  await Promise.all([loadSettings(), loadBranding(), loadMapsApiKey(), loadAdConfig()])
}

load()
</script>
