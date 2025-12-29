<template>
  <div class="rounded-xl border border-slate-100 p-4">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-2">
        <span class="badge">{{ item.type }}</span>
        <span class="text-sm text-slate-500">#{{ item.orderIndex }}</span>
      </div>
      <button v-if="canRemove" class="btn-ghost" @click="$emit('remove')">
        <TrashIcon class="h-5 w-5" />
      </button>
    </div>

    <div class="mt-3 grid grid-cols-1 sm:grid-cols-3 gap-3">
      <div class="sm:col-span-3">
        <label class="text-sm text-slate-600">Address</label>
        <input v-model="item.displayAddress" class="input mt-1" placeholder="e.g., Galle, Sri Lanka" />
      </div>
      <div>
        <label class="text-sm text-slate-600">Lat</label>
        <input v-model.number="item.lat" type="number" class="input mt-1" />
      </div>
      <div>
        <label class="text-sm text-slate-600">Lng</label>
        <input v-model.number="item.lng" type="number" class="input mt-1" />
      </div>
      <div>
        <label class="text-sm text-slate-600">PlaceId</label>
        <input v-model="item.placeId" class="input mt-1" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { TrashIcon } from '@heroicons/vue/24/outline'
defineProps<{
  item: any
  canRemove: boolean
}>()
defineEmits<{(e:'remove'):void}>()
</script>
