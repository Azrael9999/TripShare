import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './styles.css'
import { telemetryClient } from './lib/telemetryClient'

router.afterEach((to) => {
  telemetryClient.trackPageView(to.name?.toString() ?? to.path, window.location.href)
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

app.use(createPinia()).use(router).mount('#app')
