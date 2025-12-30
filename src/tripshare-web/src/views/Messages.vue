<template>
  <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
    <section class="lg:col-span-4 space-y-4">
      <div class="card p-5">
        <div class="flex items-center justify-between gap-3">
          <div>
            <div class="text-xl font-semibold">Messages</div>
            <p class="text-sm text-slate-600 mt-1">Chat with drivers/passengers per booking.</p>
          </div>
          <button class="btn-outline" @click="loadThreads">Refresh</button>
        </div>
      </div>

      <div class="card p-5 space-y-3">
        <div class="text-sm font-semibold">Start a thread</div>
        <label class="text-xs text-slate-600">Booking ID</label>
        <input v-model="newBookingId" class="input mt-1" placeholder="Booking GUID" />
        <p v-if="newBookingId && !isGuid(newBookingId)" class="text-xs text-red-600">Enter a valid GUID.</p>
        <button class="btn-primary w-full" :disabled="creatingThread || !isGuid(newBookingId)" @click="createThread">
          <span v-if="creatingThread">Creating…</span>
          <span v-else>Create thread</span>
        </button>
        <p v-if="threadErr" class="text-sm text-red-600">{{ threadErr }}</p>
      </div>

      <div class="card p-5 space-y-3">
        <div class="text-sm font-semibold">Threads</div>
        <div v-if="threads.length===0" class="text-sm text-slate-600">No threads yet.</div>
        <button
          v-for="t in threads"
          :key="t.id"
          class="btn-ghost w-full justify-between"
          :class="{ 'border border-brand-200 bg-brand-50': t.id===selectedThreadId }"
          @click="selectThread(t.id)"
        >
          <div class="text-left">
            <div class="font-semibold text-sm truncate">Booking {{ t.bookingId || '—' }}</div>
            <div class="text-xs text-slate-500">Trip {{ t.tripId || '—' }}</div>
          </div>
          <span class="text-xs text-slate-500">{{ dayjs(t.updatedAt).fromNow() }}</span>
        </button>
      </div>
    </section>

    <section class="lg:col-span-8 space-y-4">
      <div class="card p-5 min-h-[360px] flex flex-col">
        <div class="flex items-center justify-between">
          <div class="font-semibold">Thread</div>
          <div class="text-xs text-slate-500" v-if="selectedThread">Updated {{ dayjs(selectedThread.updatedAt).fromNow() }}</div>
        </div>

        <div class="mt-4 flex-1 overflow-y-auto space-y-3">
          <div v-if="loadingMessages" class="text-slate-600">Loading messages…</div>
          <div v-else-if="!selectedThreadId" class="text-slate-600">Select or create a thread to chat.</div>
          <div v-else-if="messages.length===0" class="text-slate-600">No messages yet.</div>

          <div v-for="m in messages" :key="m.id" class="rounded-xl border border-slate-100 p-3 bg-white">
            <div class="text-xs text-slate-500 flex items-center justify-between">
              <span>{{ m.senderId === meId ? 'You' : 'Other' }}</span>
              <span>{{ dayjs(m.sentAt).fromNow() }}</span>
            </div>
            <div class="mt-1 text-sm text-slate-800 whitespace-pre-wrap">{{ m.body }}</div>
          </div>
        </div>

        <div class="mt-4 border-t border-slate-100 pt-4 space-y-2" v-if="selectedThreadId">
          <textarea v-model="draft" rows="2" class="textarea" placeholder="Type a message"></textarea>
          <div class="flex items-center justify-between gap-3">
            <p v-if="sendErr" class="text-sm text-red-600">{{ sendErr }}</p>
            <button class="btn-primary" :disabled="sending || !canSend" @click="send">
              <span v-if="sending">Sending…</span>
              <span v-else>Send</span>
            </button>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import dayjs from 'dayjs'
import relativeTime from 'dayjs/plugin/relativeTime'
import { http, MessageItem, MessageThread } from '../lib/api'
import { useAuthStore } from '../stores/auth'
dayjs.extend(relativeTime)

const auth = useAuthStore()
const meId = computed(() => auth.me?.id ?? '')
const threads = ref<MessageThread[]>([])
const messages = ref<MessageItem[]>([])
const selectedThreadId = ref<string>('')
const loadingMessages = ref(false)
const draft = ref('')
const sendErr = ref('')
const sending = ref(false)
const newBookingId = ref('')
const creatingThread = ref(false)
const threadErr = ref('')

const selectedThread = computed(() => threads.value.find(t => t.id === selectedThreadId.value))
const canSend = computed(() => !!draft.value.trim())

async function loadThreads() {
  const resp = await http.get<MessageThread[]>('/messages/threads')
  threads.value = resp.data ?? []
  if (threads.value.length && !selectedThreadId.value) {
    selectedThreadId.value = threads.value[0].id
    await loadMessages()
  }
}

async function selectThread(id: string) {
  selectedThreadId.value = id
  await loadMessages()
}

async function loadMessages() {
  if (!selectedThreadId.value) return
  loadingMessages.value = true
  sendErr.value = ''
  try {
    const resp = await http.get<MessageItem[]>(`/messages/threads/${selectedThreadId.value}/messages`, { params: { take: 50 } })
    messages.value = resp.data ?? []
  } finally {
    loadingMessages.value = false
  }
}

async function send() {
  if (!draft.value.trim()) {
    sendErr.value = 'Message cannot be empty.'
    return
  }
  sending.value = true
  sendErr.value = ''
  try {
    await http.post(`/messages/threads/${selectedThreadId.value}/messages`, { body: draft.value.trim() })
    draft.value = ''
    await loadMessages()
  } catch (e:any) {
    sendErr.value = e?.response?.data?.message ?? 'Failed to send.'
  } finally {
    sending.value = false
  }
}

async function createThread() {
  if (!isGuid(newBookingId.value)) {
    threadErr.value = 'Enter a valid booking ID.'
    return
  }
  creatingThread.value = true
  threadErr.value = ''
  try {
    const resp = await http.post<MessageThread>('/messages/threads', { bookingId: newBookingId.value })
    threads.value.unshift(resp.data)
    selectedThreadId.value = resp.data.id
    newBookingId.value = ''
    await loadMessages()
  } catch (e:any) {
    threadErr.value = e?.response?.data?.message ?? 'Failed to create thread.'
  } finally {
    creatingThread.value = false
  }
}

function isGuid(value: string) {
  return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/.test(value.trim())
}

loadThreads()
</script>
