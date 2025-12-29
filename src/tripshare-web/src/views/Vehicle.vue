<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-8">
      <div class="card p-5">
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="text-xl font-semibold">Vehicle profile</div>
            <p class="text-sm text-slate-600 mt-1">Drivers must add a vehicle before creating trips.</p>
          </div>
          <span class="badge" v-if="saved">Saved</span>
        </div>

        <div class="mt-5 grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label class="text-sm text-slate-600">Make</label>
            <input v-model="make" class="input mt-1" placeholder="Toyota" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Model</label>
            <input v-model="model" class="input mt-1" placeholder="Aqua" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Color</label>
            <input v-model="color" class="input mt-1" placeholder="White" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Plate number (optional)</label>
            <input v-model="plate" class="input mt-1" placeholder="CAA-1234" />
          </div>
          <div>
            <label class="text-sm text-slate-600">Seat count</label>
            <input v-model.number="seats" type="number" min="1" max="12" class="input mt-1" />
          </div>
        </div>

        <div class="mt-5 flex items-center gap-3">
          <button class="btn-primary" :disabled="saving" @click="save">
            <span v-if="!saving">Save vehicle</span>
            <span v-else>Saving...</span>
          </button>
          <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
        </div>
      </div>
    </section>

    <aside class="lg:col-span-4 space-y-6">
      <AdSlot />
      <div class="card p-5">
        <div class="font-semibold">Why we ask this</div>
        <p class="text-sm text-slate-600 mt-2">
          A verified vehicle improves trust and reduces cancellations. Plate number stays hidden until a booking is accepted.
        </p>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { http, VehicleProfile } from '../lib/api'
import AdSlot from '../components/AdSlot.vue'

const make = ref('')
const model = ref('')
const color = ref('')
const plate = ref('')
const seats = ref(4)

const saving = ref(false)
const saved = ref(false)
const error = ref('')

async function load() {
  const resp = await http.get<VehicleProfile | null>('/users/me/vehicle')
  if (resp.data) {
    make.value = resp.data.make
    model.value = resp.data.model
    color.value = resp.data.color
    plate.value = resp.data.plateNumber ?? ''
    seats.value = resp.data.seatCount ?? 4
  }
}
async function save() {
  saved.value = false
  error.value = ''
  saving.value = true
  try {
    await http.put('/users/me/vehicle', {
      make: make.value,
      model: model.value,
      color: color.value,
      plateNumber: plate.value || null,
      seats: seats.value
    })
    saved.value = true
  } catch (e: any) {
    error.value = e?.response?.data?.message ?? e?.message ?? 'Failed to save.'
  } finally {
    saving.value = false
  }
}

load()
</script>
