<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-7 space-y-6">
      <div class="card p-5">
        <div class="text-xl font-semibold">Admin accounts</div>
        <p class="text-sm text-slate-600 mt-1">Create or approve admin access. Only super admins can access this page.</p>
      </div>

      <div class="card p-5 space-y-4">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div class="space-y-1">
            <label class="block text-xs text-slate-600">Email</label>
            <input v-model="form.email" class="input" placeholder="admin@example.com" type="email" />
          </div>
          <div class="space-y-1">
            <label class="block text-xs text-slate-600">Display name</label>
            <input v-model="form.displayName" class="input" placeholder="Admin name" />
          </div>
          <div class="space-y-1">
            <label class="block text-xs text-slate-600">Password (optional for updates)</label>
            <input v-model="form.password" class="input" placeholder="••••••••" type="password" />
          </div>
          <div class="flex items-center gap-2">
            <input id="approved" v-model="form.approved" type="checkbox" class="h-4 w-4" />
            <label for="approved" class="text-sm text-slate-600">Approved for admin access</label>
          </div>
        </div>
        <div class="flex flex-wrap gap-2">
          <button class="btn-primary" :disabled="busy || !form.email" @click="save">Save admin</button>
          <button class="btn-ghost" type="button" @click="resetForm">Clear</button>
        </div>
        <p v-if="msg" class="text-sm text-emerald-700">{{ msg }}</p>
        <p v-if="err" class="text-sm text-red-600">{{ err }}</p>
      </div>
    </section>

    <aside class="lg:col-span-5 space-y-6">
      <div class="card p-5">
        <div class="flex items-center justify-between">
          <div class="font-semibold">Existing admins</div>
          <button class="btn-ghost text-xs" :disabled="loading" @click="load">Refresh</button>
        </div>
        <div v-if="loading" class="text-sm text-slate-600 mt-3">Loading…</div>
        <div v-else-if="admins.length === 0" class="text-sm text-slate-600 mt-3">No admin accounts.</div>
        <div v-else class="mt-3 space-y-2">
          <div v-for="admin in admins" :key="admin.id" class="border border-slate-100 rounded-xl p-3">
            <div class="flex items-center justify-between gap-2">
              <div>
                <div class="font-semibold text-sm">{{ admin.displayName || admin.email }}</div>
                <div class="text-xs text-slate-500">{{ admin.email }}</div>
              </div>
              <button class="btn-outline text-xs" @click="edit(admin)">Edit</button>
            </div>
            <div class="mt-2 flex flex-wrap gap-2 text-xs">
              <span class="badge">{{ admin.role }}</span>
              <span class="chip">{{ admin.adminApproved ? 'Approved' : 'Pending' }}</span>
              <span v-if="admin.isSuspended" class="chip">Suspended</span>
            </div>
          </div>
        </div>
      </div>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { http } from '../../lib/api'

type AdminRow = {
  id: string
  email: string
  displayName?: string
  role: string
  adminApproved: boolean
  isSuspended: boolean
}

const admins = ref<AdminRow[]>([])
const loading = ref(false)
const busy = ref(false)
const msg = ref('')
const err = ref('')

const form = reactive({
  email: '',
  displayName: '',
  password: '',
  approved: false
})

function resetForm() {
  form.email = ''
  form.displayName = ''
  form.password = ''
  form.approved = false
}

function edit(admin: AdminRow) {
  form.email = admin.email
  form.displayName = admin.displayName ?? ''
  form.password = ''
  form.approved = admin.adminApproved
}

async function load() {
  loading.value = true
  err.value = ''
  try {
    const resp = await http.get<AdminRow[]>('/admin/admins')
    admins.value = resp.data ?? []
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? 'Failed to load admin accounts.'
  } finally {
    loading.value = false
  }
}

async function save() {
  busy.value = true
  msg.value = ''
  err.value = ''
  try {
    await http.post('/admin/admins', {
      email: form.email.trim(),
      displayName: form.displayName.trim() || null,
      password: form.password || null,
      approved: form.approved
    })
    msg.value = 'Admin account saved.'
    await load()
    form.password = ''
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? 'Failed to save admin account.'
  } finally {
    busy.value = false
  }
}

load()
</script>
