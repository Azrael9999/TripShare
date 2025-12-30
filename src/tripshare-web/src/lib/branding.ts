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
export const brandConfig = {
  logoUrl: envOr('/branding/logo.svg', 'VITE_BRAND_LOGO_URL'),
  heroImageUrl: envOr('/branding/hero.svg', 'VITE_BRAND_HERO_URL'),
  mapOverlayUrl: envOr('/branding/map-overlay.svg', 'VITE_BRAND_MAP_OVERLAY_URL'),
  loginIllustrationUrl: envOr('/branding/login-illustration.svg', 'VITE_BRAND_LOGIN_ILLUSTRATION_URL')
}

export function hasImage(url?: string | null) {
  return !!url && typeof url === 'string'
}
