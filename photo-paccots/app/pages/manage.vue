<script setup lang="ts">
import type { FolderTreeNodeDto, ImageDto, ManageFolderTreeRequest, UpdateMetadataRequest } from '~/types/image'

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

// Folder tree management
const folderPathsInput = ref('photos')
const tagRulesInput = ref('')
const folderTreeLoading = ref(true)
const folderTreeSaving = ref(false)
const folderTreeError = ref<string | null>(null)
const folderTreeSuccess = ref<string | null>(null)
const selectedFolderPreviewPath = ref<string | null>(null)
const recomputeRunning = ref(false)

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

const formatTagRules = (rules: Record<string, string>) => {
  return Object.entries(rules)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([tag, path]) => `${tag}=${path}`)
    .join('\n')
}

const parseTagRules = (raw: string): Record<string, string> => {
  const output: Record<string, string> = {}
  for (const line of raw.split('\n')) {
    const trimmed = line.trim()
    if (!trimmed) continue
    const idx = trimmed.indexOf('=')
    if (idx <= 0) continue
    const key = trimmed.slice(0, idx).trim()
    const value = trimmed.slice(idx + 1).trim()
    if (!key || !value) continue
    output[key] = value
  }
  return output
}

const fetchFolderTree = async () => {
  folderTreeLoading.value = true
  folderTreeError.value = null
  folderTreeSuccess.value = null
  try {
    const data = await api.getManageFolderTree()
    folderPathsInput.value = (data.folderPaths?.length ? data.folderPaths : ['photos']).join('\n')
    tagRulesInput.value = formatTagRules(data.tagRules || {})
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    folderTreeError.value = `Failed to load folder structure. ${detail}`
  } finally {
    folderTreeLoading.value = false
  }
}

const saveFolderTree = async () => {
  folderTreeSaving.value = true
  folderTreeError.value = null
  folderTreeSuccess.value = null
  try {
    const payload: ManageFolderTreeRequest = {
      folderPaths: folderPathsInput.value
        .split('\n')
        .map((x) => x.trim())
        .filter(Boolean),
      tagRules: parseTagRules(tagRulesInput.value),
    }
    const saved = await api.updateManageFolderTree(payload)
    folderPathsInput.value = (saved.folderPaths?.length ? saved.folderPaths : ['photos']).join('\n')
    tagRulesInput.value = formatTagRules(saved.tagRules || {})
    folderTreeSuccess.value = 'Folder structure saved.'
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    folderTreeError.value = `Failed to save folder structure. ${detail}`
  } finally {
    folderTreeSaving.value = false
  }
}

const sanitizeFolderSegment = (value: string) => {
  const ascii = value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim()
  const cleaned = ascii
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
  return cleaned || 'tag'
}

const generateFoldersFromTags = () => {
  folderTreeError.value = null
  folderTreeSuccess.value = null

  const imagesWithTags = images.value
    .map((img) => (img.tags ?? []).map((tag) => tag.trim()).filter(Boolean))
    .filter((tags) => tags.length > 0)

  if (imagesWithTags.length === 0) {
    folderTreeError.value = 'No tags available to generate folder structure.'
    return
  }

  const folderPaths = new Set<string>(['photos'])
  const rules: Record<string, string> = {}

  for (const tags of imagesWithTags) {
    let currentPath = 'photos'
    const usedSiblings = new Set<string>()

    for (const tag of tags) {
      const base = sanitizeFolderSegment(tag)
      let segment = base
      let i = 2

      // Avoid duplicate sibling segments for repeated tags in same chain.
      while (usedSiblings.has(segment)) {
        segment = `${base}-${i}`
        i++
      }
      usedSiblings.add(segment)

      currentPath = `${currentPath}/${segment}`
      folderPaths.add(currentPath)

      // Keep first inferred taxonomy path for a given tag.
      if (!rules[tag]) {
        rules[tag] = currentPath
      }
    }
  }

  folderPathsInput.value = Array.from(folderPaths).sort((a, b) => a.localeCompare(b)).join('\n')
  tagRulesInput.value = formatTagRules(rules)
  folderTreeSuccess.value = 'Suggested hierarchical folder structure generated from current image tags. Review and click "Save folders" to persist.'
}

const recomputeAllFolderAssignments = async () => {
  recomputeRunning.value = true
  folderTreeError.value = null
  folderTreeSuccess.value = null
  try {
    const result = await api.recomputeFolderAssignments()
    folderTreeSuccess.value = `Recomputed folder assignments for ${result.updated}/${result.total} images.`
    await fetchImages()
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    folderTreeError.value = `Failed to recompute folder assignments. ${detail}`
  } finally {
    recomputeRunning.value = false
  }
}

const buildTreeFromPaths = (paths: string[]): FolderTreeNodeDto[] => {
  type MutableNode = {
    name: string
    path: string
    children: Record<string, MutableNode>
  }

  const root: MutableNode = { name: 'photos', path: 'photos', children: {} }
  const normalized = new Set<string>(['photos'])
  for (const raw of paths) {
    const cleaned = raw.trim().replaceAll('\\', '/')
    if (!cleaned) continue
    const segments = cleaned.split('/').filter(Boolean)
    const path = segments[0] === 'photos' ? segments.join('/') : `photos/${segments.join('/')}`
    normalized.add(path)
  }

  for (const path of normalized) {
    const segments = path.split('/').filter(Boolean)
    if (segments.length <= 1) continue
    let cursor = root
    let currentPath = 'photos'
    for (let i = 1; i < segments.length; i++) {
      const seg = segments[i]
      if (!seg) continue
      currentPath = `${currentPath}/${seg}`
      if (!cursor.children[seg]) {
        cursor.children[seg] = { name: seg, path: currentPath, children: {} }
      }
      cursor = cursor.children[seg]
    }
  }

  const toDto = (node: MutableNode): FolderTreeNodeDto => ({
    name: node.name,
    path: node.path,
    children: Object.values(node.children)
      .sort((a, b) => a.name.localeCompare(b.name))
      .map(toDto),
  })

  return [toDto(root)]
}

const folderTreePreview = computed(() =>
  buildTreeFromPaths(
    folderPathsInput.value
      .split('\n')
      .map((x) => x.trim())
      .filter(Boolean),
  ),
)

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

onMounted(() => {
  fetchImages()
  fetchFolderTree()
})
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

    <section class="mb-8 rounded-xl border border-stone-200 bg-white p-4 shadow-sm sm:p-6">
      <div class="mb-4 flex items-center justify-between">
        <div>
          <h2 class="text-lg font-semibold text-stone-900">Folder Structure</h2>
          <p class="text-xs text-stone-500">Root must remain <code>photos</code>. Tag rules map tag to folder path.</p>
        </div>
        <div class="flex items-center gap-2">
          <button
            class="rounded-lg bg-stone-100 px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-200 disabled:opacity-50"
            :disabled="recomputeRunning || folderTreeLoading"
            @click="recomputeAllFolderAssignments"
          >
            <span v-if="recomputeRunning">Recomputing...</span>
            <span v-else>Recompute image folders</span>
          </button>
          <button
            class="rounded-lg bg-stone-100 px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-200 disabled:opacity-50"
            :disabled="folderTreeSaving || folderTreeLoading || loading"
            @click="generateFoldersFromTags"
          >
            Generate folders from tags
          </button>
          <button
            class="rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
            :disabled="folderTreeSaving || folderTreeLoading"
            @click="saveFolderTree"
          >
            <span v-if="folderTreeSaving">Saving...</span>
            <span v-else>Save folders</span>
          </button>
        </div>
      </div>

      <div v-if="folderTreeLoading" class="text-sm text-stone-500">Loading folder structure...</div>
      <template v-else>
        <p v-if="folderTreeError" class="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-600">{{ folderTreeError }}</p>
        <p v-if="folderTreeSuccess" class="mb-3 rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-700">{{ folderTreeSuccess }}</p>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="block text-xs font-medium text-stone-600">Folder paths (one per line)</label>
            <textarea
              v-model="folderPathsInput"
              rows="8"
              class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 font-mono text-xs shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
              placeholder="photos&#10;photos/garden&#10;photos/garden/flowers"
            />
          </div>
          <div>
            <label class="block text-xs font-medium text-stone-600">Tag rules (tag=folderPath)</label>
            <textarea
              v-model="tagRulesInput"
              rows="8"
              class="mt-1 block w-full rounded-md border border-stone-300 px-2 py-1.5 font-mono text-xs shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
              placeholder="rose=photos/garden/flowers&#10;tree=photos/garden/trees"
            />
          </div>
        </div>

        <div class="mt-4">
          <p class="mb-2 text-xs font-medium text-stone-600">Folder tree preview</p>
          <GalleryFolderTree
            :tree="folderTreePreview"
            :selected-path="selectedFolderPreviewPath"
            @select="selectedFolderPreviewPath = $event"
            @clear="selectedFolderPreviewPath = null"
          />
        </div>
      </template>
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
              :src="img.thumbnailUrl"
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
ull

              <p class="mt-2 text-xs text-stone-500">
                Folder: <span class="font-mono">{{ img.primaryFolderPath || 'photos' }}</span>
              </p>

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
