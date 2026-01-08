import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './styles.css'
import { telemetryClient } from './lib/telemetryClient'
import { applySeo, upsertLdJson } from './lib/seo'
import { brandConfig, loadBrandingConfig } from './lib/branding'

async function bootstrap() {
  await loadBrandingConfig()

  router.afterEach((to) => {
    telemetryClient.trackPageView(to.name?.toString() ?? to.path, window.location.href)
    const title = to.meta?.title as string | undefined
    applySeo({
      title,
      url: window.location.href,
      image: brandConfig.heroImageUrl
    })
  })

  const app = createApp(App)

  app.config.errorHandler = (err, instance, info) => {
    console.error('Vue error:', err, info)
    telemetryClient.trackError(err instanceof Error ? err : new Error(String(err)), {
      component: instance?.type?.name ?? 'unknown',
      info
    })
  }

  window.addEventListener('error', (event) => {
    if (!event.error) return
    telemetryClient.trackError(event.error, {
      source: 'window.error',
      filename: event.filename,
      lineno: event.lineno,
      colno: event.colno
    })
  })

  window.addEventListener('unhandledrejection', (event) => {
    const reason = event.reason instanceof Error ? event.reason : new Error(String(event.reason ?? 'unknown rejection'))
    telemetryClient.trackError(reason, { source: 'unhandledrejection' })
  })

  // Organization structured data (static)
  upsertLdJson('org', {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    name: 'HopTrip Sri Lanka',
    url: window.location.origin,
    logo: brandConfig.logoUrl,
    sameAs: [
      'https://www.facebook.com',
      'https://www.instagram.com'
    ],
    areaServed: 'LK'
  })

  app.use(createPinia()).use(router).mount('#app')
}

bootstrap()
