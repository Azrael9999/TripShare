<template>
  <div class="space-y-2">
    <label v-if="label" class="text-sm text-slate-600">{{ label }}</label>
    <div class="relative">
      <input
        v-model="inputValue"
        :placeholder="placeholder"
        class="input w-full"
        @focus="showDropdown = true"
        @input="onInput"
      />
      <div
        v-if="showDropdown && suggestions.length"
        class="absolute z-20 mt-1 w-full rounded-xl border border-slate-200 bg-white shadow-lg"
      >
        <ul class="divide-y divide-slate-100 max-h-60 overflow-auto">
          <li
            v-for="p in suggestions"
            :key="p.place_id"
            class="px-3 py-2 hover:bg-slate-50 cursor-pointer text-sm"
            @click="select(p)"
          >
            {{ p.description || p.name }}
          </li>
        </ul>
      </div>
      <div v-else-if="showDropdown && !loading && inputValue.length >= 3" class="absolute z-20 mt-1 w-full rounded-xl border border-slate-200 bg-white shadow px-3 py-2 text-sm text-slate-500">
        No matches. Try a nearby landmark.
      </div>
      <div v-if="loading" class="absolute right-3 top-2 text-xs text-slate-500">Loading…</div>
    </div>
    <p v-if="helper" class="text-xs text-slate-500">{{ helper }}</p>
    <p v-if="error" class="text-xs text-red-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { createSessionToken, getAutocomplete, getPlaceDetails, loadGoogleMapsPlaces } from '../lib/googleMaps'

const props = defineProps<{
  modelValue: string
  placeholder?: string
  label?: string
  helper?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'select', payload: { description: string; placeId: string; lat: number; lng: number }): void
}>()

const inputValue = ref(props.modelValue || '')
const suggestions = ref<any[]>([])
const loading = ref(false)
const error = ref('')
const showDropdown = ref(false)

let sessionToken: any = null
let debounceTimer: any = null

watch(
  () => props.modelValue,
  (v) => {
    if (v !== inputValue.value) {
      inputValue.value = v
    }
  }
)

onMounted(async () => {
  await loadGoogleMapsPlaces()
  sessionToken = createSessionToken()
})

onBeforeUnmount(() => {
  clearTimeout(debounceTimer)
})

function onInput() {
  emit('update:modelValue', inputValue.value)
  showDropdown.value = true
  queueAutocomplete()
}

function queueAutocomplete() {
  clearTimeout(debounceTimer)
  if (!inputValue.value || inputValue.value.trim().length < 3) {
    suggestions.value = []
    return
  }
  debounceTimer = setTimeout(fetchAutocomplete, 280)
}

async function fetchAutocomplete() {
  loading.value = true
  error.value = ''
  try {
    if (!sessionToken) {
      sessionToken = createSessionToken()
    }
    if (!sessionToken) {
      loading.value = false
      return
    }
    const preds = await getAutocomplete(inputValue.value.trim(), sessionToken, { minLength: 3 })
    suggestions.value = preds
  } catch (e: any) {
    console.error(e)
    error.value = 'Could not fetch suggestions right now.'
  } finally {
    loading.value = false
  }
}

async function select(pred: any) {
  showDropdown.value = false
  suggestions.value = []
  inputValue.value = pred.description || pred.name || ''
  emit('update:modelValue', inputValue.value)
  if (!sessionToken) {
    sessionToken = createSessionToken()
  }
  if (!sessionToken) return

  const details = await getPlaceDetails(pred.place_id, sessionToken)
  const loc = details?.geometry?.location
  if (!loc) return

  emit('select', {
    description: details.formatted_address || pred.description || pred.name || '',
    placeId: details.place_id || pred.place_id,
    lat: loc.lat(),
    lng: loc.lng()
  })

  // new session token for subsequent rounds to keep sessions scoped
  sessionToken = createSessionToken()
}
</script>
