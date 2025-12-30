<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8 space-y-4">
      <div class="card p-5">
        <div class="flex items-center justify-between gap-4">
          <div>
            <div class="text-xl font-semibold">Ad configuration</div>
            <p class="text-sm text-slate-600 mt-1">Control slots, enablement, and frequency caps.</p>
          </div>
          <div class="flex items-center gap-3">
            <label class="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" v-model="enabled" /> Enable ads
            </label>
            <button class="btn-outline" @click="load">Reload</button>
          </div>
        </div>

        <div class="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3">
          <div>
            <label class="text-xs text-slate-600">Frequency cap (per session)</label>
            <input v-model.number="frequency" type="number" min="0" max="100" class="input mt-1" />
          </div>
          <div>
            <label class="text-xs text-slate-600">Max slots rendered per page</label>
            <input v-model.number="maxSlotsPerPage" type="number" min="0" max="10" class="input mt-1" />
            <p class="text-xs text-slate-500 mt-1">0 means unlimited, otherwise caps how many ad blocks can appear on a single page.</p>
          </div>
        </div>

        <div class="mt-4 space-y-3">
          <div class="flex items-center justify-between">
            <div class="font-semibold">Slots</div>
            <button class="btn-outline" @click="addSlot">Add slot</button>
          </div>

          <div v-for="(slot, idx) in slots" :key="slot.slot + idx" class="rounded-2xl border border-slate-100 p-4 bg-white">
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
                <button class="btn-ghost text-sm" @click="remove(idx)">Remove</button>
              </div>
            </div>
          </div>
        </div>

        <div class="mt-4 flex items-center gap-3">
          <button class="btn-primary" :disabled="saving" @click="save">{{ saving ? 'Saving…' : 'Save config' }}</button>
          <p v-if="msg" class="text-sm text-emerald-700">{{ msg }}</p>
          <p v-if="err" class="text-sm text-red-600">{{ err }}</p>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot name="admin-sidebar" />
      <div class="card p-5">
        <div class="font-semibold">Hints</div>
        <p class="text-sm text-slate-600 mt-2">Frequency caps are enforced client-side; keep values small to protect UX.</p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { http } from '../../lib/api'
import AdSlot from '../../components/AdSlot.vue'

type Slot = { slot: string; html: string; enabled: boolean }

const slots = reactive<Slot[]>([])
const enabled = ref(false)
const frequency = ref(3)
const maxSlotsPerPage = ref(3)
const msg = ref('')
const err = ref('')
const saving = ref(false)

function normalizeSlots(raw: any[] = []) {
  slots.splice(0, slots.length)
  raw.forEach((s) => slots.push({ slot: s.slot ?? '', html: s.html ?? '', enabled: !!s.enabled }))
}

async function load() {
  msg.value = ''
  err.value = ''
  try {
    const resp = await http.get('/admin/ads/config')
    enabled.value = !!resp.data?.enabled
    frequency.value = resp.data?.frequencyCapPerSession ?? 3
    maxSlotsPerPage.value = resp.data?.maxSlotsPerPage ?? 3
    normalizeSlots(resp.data?.slots ?? [])
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? 'Failed to load.'
  }
}

async function save() {
  saving.value = true
  msg.value = ''
  err.value = ''
  try {
    await http.post('/admin/ads/config', {
      enabled: enabled.value,
      frequencyCapPerSession: frequency.value,
      maxSlotsPerPage: maxSlotsPerPage.value,
      slots: slots.map((s) => ({ ...s }))
    })
    msg.value = 'Saved.'
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? 'Failed to save.'
  } finally {
    saving.value = false
  }
}

function addSlot() {
  slots.push({ slot: 'new-slot', html: '<div>Ad</div>', enabled: true })
}

function remove(idx: number) {
  slots.splice(idx, 1)
}

load()
</script>
