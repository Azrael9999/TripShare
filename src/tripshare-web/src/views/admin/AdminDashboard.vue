<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-6">
      <div class="card p-5">
        <div class="text-xl font-semibold">Admin dashboard</div>
        <p class="text-sm text-slate-600 mt-1">Overview of the platform health.</p>
      </div>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div class="card p-5" v-for="c in cards" :key="c.label">
          <div class="text-sm text-slate-600">{{ c.label }}</div>
          <div class="text-3xl font-semibold mt-2">{{ c.value }}</div>
        </div>
      </div>

      <div class="card p-5">
        <div class="flex items-center justify-between">
          <div>
            <div class="font-semibold">Quick actions</div>
            <p class="text-sm text-slate-600 mt-1">Moderate reports and hide abusive trips.</p>
          </div>
          <RouterLink to="/admin/reports" class="btn-primary">View reports</RouterLink>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot />
      <div class="card p-5">
        <div class="font-semibold">Note</div>
        <p class="text-sm text-slate-600 mt-2">Admin endpoints are protected by role and audited in logs.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { http } from '../../lib/api'
import AdSlot from '../../components/AdSlot.vue'

const metrics = ref<any|null>(null)

const cards = computed(() => {
  const m = metrics.value ?? {}
  return [
    { label: 'Users', value: m.usersTotal ?? 0 },
    { label: 'Trips', value: m.tripsTotal ?? 0 },
    { label: 'Bookings', value: m.bookingsTotal ?? 0 },
    { label: 'Reports (open)', value: m.reportsOpen ?? 0 }
  ]
})

async function load() {
  const resp = await http.get('/admin/metrics')
  metrics.value = resp.data
}
load()
</script>
