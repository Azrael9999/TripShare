<template>
  <div class="space-y-2">
    <label v-if="label" class="text-sm text-slate-600">{{ label }}</label>
    <div ref="mapEl" class="h-56 w-full rounded-xl border border-slate-200"></div>
    <p v-if="helper" class="text-xs text-slate-500">{{ helper }}</p>
    <p v-if="error" class="text-xs text-red-600">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { loadGoogleMapsPlaces } from '../lib/googleMaps'

const props = defineProps<{
  lat: number
  lng: number
  label?: string
  helper?: string
}>()

const emit = defineEmits<{
  (e: 'update:lat', value: number): void
  (e: 'update:lng', value: number): void
}>()

const mapEl = ref<HTMLDivElement | null>(null)
const error = ref('')

let map: any = null
let marker: any = null
let mapListeners: any[] = []

const defaultCenter = { lat: 0, lng: 0 }

const isValidCoordinate = (lat: number, lng: number) =>
  Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat) <= 90 && Math.abs(lng) <= 180

const isSame = (a: number, b: number) => Math.abs(a - b) < 0.000001

function emitPosition(lat: number, lng: number) {
  if (!isSame(lat, props.lat)) emit('update:lat', lat)
  if (!isSame(lng, props.lng)) emit('update:lng', lng)
}

function setMarkerPosition(lat: number, lng: number, pan = true) {
  if (!map || !marker) return
  const pos = { lat, lng }
  marker.setPosition(pos)
  if (pan) {
    map.panTo(pos)
  }
}

function focusMap(lat: number, lng: number) {
  if (!map) return
  if (map.getZoom() < 12) {
    map.setZoom(14)
  }
  map.panTo({ lat, lng })
}

function handleMapClick(event: any) {
  const loc = event?.latLng
  if (!loc) return
  const lat = loc.lat()
  const lng = loc.lng()
  setMarkerPosition(lat, lng, false)
  focusMap(lat, lng)
  emitPosition(lat, lng)
}

function handleDragEnd() {
  if (!marker) return
  const pos = marker.getPosition()
  if (!pos) return
  emitPosition(pos.lat(), pos.lng())
}

onMounted(async () => {
  const g = await loadGoogleMapsPlaces()
  if (!g?.maps || !mapEl.value) {
    error.value = 'Map unavailable. Search for an address to set a location.'
    return
  }

  const initial = isValidCoordinate(props.lat, props.lng) ? { lat: props.lat, lng: props.lng } : defaultCenter
  map = new g.maps.Map(mapEl.value, {
    center: initial,
    zoom: isValidCoordinate(props.lat, props.lng) ? 14 : 2,
    streetViewControl: false,
    mapTypeControl: false,
    fullscreenControl: false
  })

  marker = new g.maps.Marker({
    position: initial,
    map,
    draggable: true
  })

  mapListeners.push(map.addListener('click', handleMapClick))
  mapListeners.push(marker.addListener('dragend', handleDragEnd))
})

onBeforeUnmount(() => {
  mapListeners.forEach((listener) => listener.remove?.())
  mapListeners = []
})

watch(
  () => [props.lat, props.lng],
  ([lat, lng]) => {
    if (!isValidCoordinate(lat, lng)) return
    setMarkerPosition(lat, lng)
    focusMap(lat, lng)
  }
)
</script>
