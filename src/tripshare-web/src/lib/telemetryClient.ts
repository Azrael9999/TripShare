import { configureTelemetry } from './telemetry'

export const telemetryClient = configureTelemetry({
  apiBase: import.meta.env.VITE_API_BASE,
  enabled: import.meta.env.VITE_TELEMETRY_ENABLED !== 'false',
  app: 'tripshare-web'
})
