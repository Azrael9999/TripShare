import { ref } from 'vue'

type GoogleMaps = any
declare const google: any

declare global {
  interface Window {
    google?: GoogleMaps
  }
}

const scriptId = 'google-maps-js'
let loadPromise: Promise<GoogleMaps | null> | null = null
let apiKeyPromise: Promise<string | null> | null = null

export const mapsWarning = ref<string | null>(null)

const autocompleteCache = new Map<string, { ts: number; predictions: any[] }>()
const placeDetailsCache = new Map<string, { ts: number; result: any }>()

const AUTOCOMPLETE_TTL_MS = 5 * 60 * 1000
const PLACE_DETAILS_TTL_MS = 10 * 60 * 1000

export async function loadGoogleMapsPlaces(opts?: { language?: string; region?: string }): Promise<GoogleMaps | null> {
  if (loadPromise) return loadPromise

  loadPromise = new Promise((resolve) => {
    const run = async () => {
      const apiKey = await getMapsApiKey()
      if (!apiKey) {
        mapsWarning.value = 'Google Maps is not configured. Please contact support.'
        resolve(null)
        return
      }

      if (typeof window === 'undefined') {
        resolve(null)
        return
      }

      if (document.getElementById(scriptId)) {
        resolve(window.google ?? null)
        return
      }

      const script = document.createElement('script')
      script.id = scriptId
      const params = new URLSearchParams({
        key: apiKey,
        libraries: 'places',
        language: opts?.language ?? 'en',
        region: opts?.region ?? 'us',
        v: 'weekly'
      })
      script.src = `https://maps.googleapis.com/maps/api/js?${params.toString()}`
      script.async = true
      script.defer = true
      script.onerror = () => {
        mapsWarning.value = 'Google Maps failed to load. Check the API key configuration.'
        resolve(null)
      }
      script.onload = () => resolve(window.google ?? null)
      document.head.appendChild(script)
    }

    void run()
  })

  return loadPromise
}

async function getMapsApiKey(): Promise<string | null> {
  if (apiKeyPromise) return apiKeyPromise
  const apiBase = (import.meta as any).env.VITE_API_BASE ?? 'http://localhost:8080'
  const url = apiBase.replace(/\/$/, '') + '/api/maps/config'
  apiKeyPromise = fetch(url)
    .then(async (resp) => {
      if (!resp.ok) {
        throw new Error(`Maps config request failed: ${resp.status}`)
      }
      const data = (await resp.json()) as { apiKey?: string | null; ApiKey?: string | null }
      const value = data.apiKey ?? data.ApiKey ?? null
      if (!value) {
        mapsWarning.value = 'Google Maps is not configured. Please contact support.'
      }
      return value
    })
    .catch((err) => {
      console.warn(err)
      mapsWarning.value = 'Google Maps settings could not be loaded. Some features may be unavailable.'
      return null
    })

  return apiKeyPromise
}

export async function getAutocomplete(query: string, sessionToken: any, opts?: { debounceMs?: number; minLength?: number }) {
  const minLength = opts?.minLength ?? 3
  if (!query || query.trim().length < minLength) return []

  const now = Date.now()
  const cached = autocompleteCache.get(query)
  if (cached && now - cached.ts < AUTOCOMPLETE_TTL_MS) {
    return cached.predictions
  }

  const g: any = await loadGoogleMapsPlaces()
  if (!g?.maps?.places) return []

  const service = new g.maps.places.AutocompleteService()

  return new Promise<any[]>((resolve) => {
    service.getPlacePredictions(
      {
        input: query,
        sessionToken
      },
      (predictions: any) => {
        const results = predictions ?? []
        autocompleteCache.set(query, { ts: now, predictions: results })
        resolve(results)
      }
    )
  })
}

export async function getPlaceDetails(placeId: string, sessionToken: any): Promise<any | null> {
  if (!placeId) return null
  const now = Date.now()
  const cached = placeDetailsCache.get(placeId)
  if (cached && now - cached.ts < PLACE_DETAILS_TTL_MS) {
    return cached.result
  }

  const g: any = await loadGoogleMapsPlaces()
  if (!g?.maps?.places) return null

  const service = new g.maps.places.PlacesService(document.createElement('div'))
  return new Promise((resolve) => {
    service.getDetails(
      {
        placeId,
        fields: ['geometry', 'formatted_address', 'name', 'place_id'],
        sessionToken
      },
      (result, status) => {
        if (status === g.maps.places.PlacesServiceStatus.OK && result) {
          placeDetailsCache.set(placeId, { ts: now, result })
          resolve(result)
        } else {
          resolve(null)
        }
      }
    )
  })
}

export function createSessionToken() {
  if (!(window as any)?.google?.maps?.places) return null
  return new (window as any).google.maps.places.AutocompleteSessionToken()
}
