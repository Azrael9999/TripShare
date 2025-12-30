type TelemetryOptions = {
  apiBase?: string
  enabled?: boolean
  app?: string
}

type TelemetryEvent = {
  name: string
  properties?: Record<string, string | number | boolean | null | undefined>
}

type TelemetryErrorContext = Record<string, string | number | boolean | null | undefined>

export type TelemetryClient = {
  trackEvent: (evt: TelemetryEvent) => void
  trackError: (error: Error, ctx?: TelemetryErrorContext) => void
  trackPageView: (name: string, uri?: string) => void
}

const defaultNoopClient: TelemetryClient = {
  trackEvent: () => undefined,
  trackError: () => undefined,
  trackPageView: () => undefined
}

export function configureTelemetry(options: TelemetryOptions): TelemetryClient {
  const enabled = options.enabled !== false
  const base = options.apiBase?.replace(/\/$/, '')

  if (!enabled || !base) {
    return defaultNoopClient
  }

  const endpoint = `${base}/api/telemetry/logs`
  const app = options.app ?? 'tripshare-web'

  const send = (payload: Record<string, unknown>) => {
    const body = JSON.stringify(payload)

    if (navigator.sendBeacon) {
      const blob = new Blob([body], { type: 'application/json' })
      if (navigator.sendBeacon(endpoint, blob)) {
        return
      }
    }

    void fetch(endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body
    }).catch(() => {
      // swallow telemetry failures to avoid breaking UX
    })
  }

  return {
    trackEvent: (evt) => {
      send({
        message: evt.name,
        severity: 'info',
        properties: { app, ...evt.properties }
      })
    },
    trackError: (error, ctx) => {
      send({
        message: error.message,
        severity: 'error',
        stack: error.stack,
        properties: { app, ...ctx }
      })
    },
    trackPageView: (name, uri) => {
      send({
        message: `page_view:${name}`,
        severity: 'info',
        uri,
        properties: { app }
      })
    }
  }
}
