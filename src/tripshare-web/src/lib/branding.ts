import { reactive } from 'vue'

const env = (import.meta as any).env || {}

function envOr(defaultValue: string | null, key: string) {
  const value = env[key]
  return value && typeof value === 'string' ? value : defaultValue
}

/**
 * Centralized brand surface configuration so images can be updated by admins
 * without touching the codebase. Either override with env vars (VITE_*) or
 * swap the files placed under /public/branding in deployments.
 */
const defaultConfig = {
  logoUrl: envOr('/branding/logo.svg', 'VITE_BRAND_LOGO_URL'),
  heroImageUrl: envOr('/branding/hero.svg', 'VITE_BRAND_HERO_URL'),
  mapOverlayUrl: envOr('/branding/map-overlay.svg', 'VITE_BRAND_MAP_OVERLAY_URL'),
  loginIllustrationUrl: envOr('/branding/login-illustration.svg', 'VITE_BRAND_LOGIN_ILLUSTRATION_URL')
}

export const brandConfig = reactive({ ...defaultConfig })

export type BrandingConfig = {
  logoUrl?: string | null
  heroImageUrl?: string | null
  mapOverlayUrl?: string | null
  loginIllustrationUrl?: string | null
}

export function applyBrandingConfig(config?: BrandingConfig | null) {
  if (!config) return
  brandConfig.logoUrl = config.logoUrl || defaultConfig.logoUrl
  brandConfig.heroImageUrl = config.heroImageUrl || defaultConfig.heroImageUrl
  brandConfig.mapOverlayUrl = config.mapOverlayUrl || defaultConfig.mapOverlayUrl
  brandConfig.loginIllustrationUrl = config.loginIllustrationUrl || defaultConfig.loginIllustrationUrl
}

export async function loadBrandingConfig() {
  const apiBase = envOr('http://localhost:8080', 'VITE_API_BASE') ?? 'http://localhost:8080'
  const url = apiBase.replace(/\/$/, '') + '/api/branding'
  const resp = await fetch(url, { credentials: 'include' })
  if (!resp.ok) return
  const data = await resp.json()
  applyBrandingConfig(data)
}

export function hasImage(url?: string | null) {
  return !!url && typeof url === 'string'
}
