<script setup lang="ts">
import type { ImageDto, UpdateMetadataRequest } from '~/types/image'

definePageMeta({
  middleware: 'auth',
})

useHead({
  title: 'Manage — Photo Paccots',
})

const api = useApi()

const images = ref<ImageDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

// Editing state
const editingId = ref<string | null>(null)
const editTitle = ref('')
const editDescription = ref('')
const editTagsInput = ref('')
const saving = ref(false)

// Delete confirmation
const deletingId = ref<string | null>(null)
const deleting = ref(false)

const fetchImages = async () => {
  loading.value = true
  error.value = null
  try {
    const res = await api.listAllImages()
    images.value = res.images
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `Failed to load images. ${detail}`
  } finally {
    loading.value = false
  }
}

const startEdit = (img: ImageDto) => {
  editingId.value = img.id
  editTitle.value = img.title
  editDescription.value = img.description
  editTagsInput.value = img.tags.join(', ')
}

const cancelEdit = () => {
  editingId.value = null
}

const saveEdit = async () => {
  if (!editingId.value) return
  saving.value = true
  try {
    const data: UpdateMetadataRequest = {
      title: editTitle.value,
      description: editDescription.value,
      tags: editTagsInput.value.split(',').map(t => t.trim()).filter(Boolean),
    }
    await api.updateMetadata(editingId.value, data)

    const img = images.value.find(i => i.id === editingId.value)
    if (img) {
      img.title = data.title
      img.description = data.description
      img.tags = data.tags
    }
    editingId.value = null
  } catch {
    error.value = 'Failed to update image.'
  } finally {
    saving.value = false
  }
}

const confirmDelete = (id: string) => {
  deletingId.value = id
}

const cancelDelete = () => {
  deletingId.value = null
}

const executeDelete = async () => {
  if (!deletingId.value) return
  deleting.value = true
  try {
    await api.deleteImage(deletingId.value)
    images.value = images.value.filter(i => i.id !== deletingId.value)
    deletingId.value = null
  } catch {
    error.value = 'Failed to delete image.'
  } finally {
    deleting.value = false
  }
}

const formatDate = (iso: string) => {
  try {
    return new Date(iso).toLocaleDateString('en-US', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    })
  } catch {
    return iso
  }
}

const formatSize = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

onMounted(fetchImages)
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <section class="mb-8 flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
          Image Management
        </h1>
        <p class="mt-1 text-sm text-stone-500">
          {{ images.length }} image(s) total
        </p>
      </div>
      <button
        class="rounded-lg bg-stone-100 px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-200 transition-colors"
        :disabled="loading"
        @click="fetchImages"
      >
        Refresh
      </button>
    </section>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center justify-center py-16">
      <svg class="h-6 w-6 animate-spin text-emerald-600" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
      </svg>
      <span class="ml-2 text-sm text-stone-500">Loading...</span>
    </div>

    <!-- Error -->
    <p v-if="error" class="mb-4 rounded-lg bg-red-50 px-4 py-2 text-sm text-red-600">
      {{ error }}
    </p>

    <!-- Empty state -->
    <div v-if="!loading && images.length === 0" class="rounded-xl border-2 border-dashed border-stone-300 px-6 py-16 text-center">
      <p class="text-sm text-stone-500">No images yet.</p>
    </div>

    <!-- Image list -->
    <div v-if="!loading && images.length > 0" class="space-y-4">
      <div
        v-for="img in images"
        :key="img.id"
        class="overflow-hidden rounded-xl border border-stone-200 bg-white shadow-sm transition-shadow hover:shadow-md"
      >
        <div class="flex flex-col sm:flex-row">
          <!-- Thumbnail -->
          <div class="sm:w-40 sm:flex-shrink-0">
            <img
              :src="img.url"
              :alt="img.title"
              class="h-40 w-full object-cover sm:h-full"
            >
          </div>

          <!-- Content -->
          <div class="flex flex-1 flex-col p-4">
            <!-- View mode -->
            <template v-if="editingId !== img.id">
              <div class="flex items-start justify-between gap-2">
                <h2 class="text-sm font-semibold text-stone-900 line-clamp-1">
                  {{ img.title || '(untitled)' }}
                </h2>
                <span class="flex-shrink-0 text-xs text-stone-400">
                  {{ formatDate(img.uploadedAt) }}
                </span>
              </div>

              <p v-if="img.description" class="mt-1 text-xs text-stone-500 line-clamp-2">
                {{ img.description }}
              </p>

              <div v-if="img.tags.length" class="mt-2 flex flex-wrap gap-1">
                <span
                  v-for="tag in img.tags"
                  :key="tag"
                  class="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700"
                >
                  {{ tag }}
                </span>
              </div>

              <div class="mt-auto flex items-center gap-2 pt-3">
                <span class="text-xs text-stone-400">{{ formatSize(img.sizeBytes) }}</span>
                <span class="text-xs text-stone-300">|</span>
                <span class="text-xs text-stone-400">{{ img.width }}×{{ img.height }}</span>
                <div class="ml-auto flex gap-2">
                  <button
                    class="rounded-md bg-stone-100 px-3 py-1 text-xs font-medium text-stone-600 hover:bg-stone-200 transition-colors"
                    @click="startEdit(img)"
                  >
                    Edit
                  </button>
                  <button
                    class="rounded-md bg-red-50 px-3 py-1 text-xs font-medium text-red-600 hover:bg-red-100 transition-colors"
                    @click="confirmDelete(img.id)"
                  >
                    Delete
                  </button>
                </div>
              </div>
            </template>

            <!-- Edit mode -->
            <template v-else>
              <div class="space-y-3">
                <div>
                  <label class="block text-xs font-medium text-stone-600">Title</label>
                  <input
                    v-model="editTitle"
                    type="text"
                    class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
                  >
                </div>
                <div>
                  <label class="block text-xs font-medium text-stone-600">Description</label>
                  <textarea
                    v-model="editDescription"
                    rows="2"
                    class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
                  />
                </div>
                <div>
                  <label class="block text-xs font-medium text-stone-600">Tags (comma-separated)</label>
                  <input
                    v-model="editTagsInput"
                    type="text"
                    class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 text-sm shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
                  >
                </div>
                <div class="flex justify-end gap-2">
                  <button
                    class="rounded-md bg-stone-100 px-3 py-1.5 text-xs font-medium text-stone-600 hover:bg-stone-200 transition-colors"
                    :disabled="saving"
                    @click="cancelEdit"
                  >
                    Cancel
                  </button>
                  <button
                    class="rounded-md bg-emerald-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-emerald-700 transition-colors disabled:opacity-50"
                    :disabled="saving"
                    @click="saveEdit"
                  >
                    <span v-if="saving">Saving...</span>
                    <span v-else>Save</span>
                  </button>
                </div>
              </div>
            </template>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete confirmation modal -->
    <Teleport to="body">
      <Transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="opacity-0"
        enter-to-class="opacity-100"
        leave-active-class="transition duration-150 ease-in"
        leave-from-class="opacity-100"
        leave-to-class="opacity-0"
      >
        <div v-if="deletingId" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div class="w-full max-w-sm rounded-xl bg-white p-6 shadow-xl">
            <h3 class="text-lg font-semibold text-stone-900">Confirm deletion</h3>
            <p class="mt-2 text-sm text-stone-500">
              This action cannot be undone. The image and its metadata will be permanently deleted.
            </p>
            <div class="mt-6 flex justify-end gap-3">
              <button
                class="rounded-lg bg-stone-100 px-4 py-2 text-sm font-medium text-stone-700 hover:bg-stone-200 transition-colors"
                :disabled="deleting"
                @click="cancelDelete"
              >
                Cancel
              </button>
              <button
                class="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 transition-colors disabled:opacity-50"
                :disabled="deleting"
                @click="executeDelete"
              >
                <span v-if="deleting">Deleting...</span>
                <span v-else>Delete</span>
              </button>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>
