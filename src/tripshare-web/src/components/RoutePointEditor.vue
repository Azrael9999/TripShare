<template>
  <div class="rounded-xl border border-slate-100 p-4">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <span class="badge">{{ item.type }}</span>
        <span class="text-sm text-slate-500">#{{ item.orderIndex }}</span>
      </div>
      <button v-if="canRemove" class="btn-ghost" @click="emit('remove')">
        <TrashIcon class="h-5 w-5" />
      </button>
    </div>

    <div class="mt-3 grid grid-cols-1 gap-4">
      <div>
        <PlacesAutocomplete
          v-model="item.displayAddress"
          label="Address"
          placeholder="Search address or landmark"
          helper="Search is debounced and cached to minimize Google Maps calls."
          @select="onSelect"
        />
      </div>
      <MapPinPicker
        v-model:lat="item.lat"
        v-model:lng="item.lng"
        label="Pin location"
        helper="Drag the pin or click the map to fine-tune the pickup/drop-off point."
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { TrashIcon } from '@heroicons/vue/24/outline'
import PlacesAutocomplete from './PlacesAutocomplete.vue'
import MapPinPicker from './MapPinPicker.vue'
const props = defineProps<{
  item: any
  canRemove: boolean
}>()
const emit = defineEmits<{(e:'remove'):void}>()

function onSelect(payload: { description: string; placeId: string; lat: number; lng: number }) {
  const { description, placeId, lat, lng } = payload
  props.item.displayAddress = description
  props.item.placeId = placeId
  props.item.lat = lat
  props.item.lng = lng
}
</script>
