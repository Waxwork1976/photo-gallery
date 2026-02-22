<script setup lang="ts">
const emit = defineEmits<{
  uploaded: []
}>()

const { state, uploadImage, reset } = useUpload()

const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']
const MAX_SIZE_MB = 10
const MAX_SIZE_BYTES = MAX_SIZE_MB * 1024 * 1024

// Form fields
const file = ref<File | null>(null)
const preview = ref<string | null>(null)
const title = ref('')
const description = ref('')
const tagsInput = ref('')
const validationError = ref<string | null>(null)
const uploadSuccess = ref(false)

/** Derive tags array from comma-separated string. */
const tags = computed(() =>
  tagsInput.value
    .split(',')
    .map((t) => t.trim())
    .filter(Boolean),
)

const fileInputRef = ref<HTMLInputElement | null>(null)

const openFilePicker = () => {
  fileInputRef.value?.click()
}

const onFileChange = (e: Event) => {
  const input = e.target as HTMLInputElement
  const selected = input.files?.[0]
  if (!selected) return

  validationError.value = null

  // Validate type
  if (!ALLOWED_TYPES.includes(selected.type)) {
    validationError.value = `Invalid file type. Allowed: JPEG, PNG, WebP, GIF.`
    return
  }

  // Validate size
  if (selected.size > MAX_SIZE_BYTES) {
    validationError.value = `File too large. Maximum size is ${MAX_SIZE_MB} MB.`
    return
  }

  file.value = selected
  title.value = selected.name.replace(/\.[^/.]+$/, '') // default title = filename without extension

  // Create preview URL
  if (preview.value) URL.revokeObjectURL(preview.value)
  preview.value = URL.createObjectURL(selected)
}

const onDrop = (e: DragEvent) => {
  e.preventDefault()
  const dropped = e.dataTransfer?.files?.[0]
  if (!dropped) return

  // Simulate file input change
  const dt = new DataTransfer()
  dt.items.add(dropped)
  if (fileInputRef.value) {
    fileInputRef.value.files = dt.files
    onFileChange({ target: fileInputRef.value } as unknown as Event)
  }
}

const onDragOver = (e: DragEvent) => {
  e.preventDefault()
}

const removeFile = () => {
  file.value = null
  if (preview.value) {
    URL.revokeObjectURL(preview.value)
    preview.value = null
  }
  if (fileInputRef.value) fileInputRef.value.value = ''
}

const handleSubmit = async () => {
  if (!file.value) return

  validationError.value = null
  uploadSuccess.value = false

  try {
    await uploadImage(file.value, {
      title: title.value || file.value.name,
      description: description.value,
      tags: tags.value,
    })

    uploadSuccess.value = true

    // Reset form after short delay so user sees success
    setTimeout(() => {
      removeFile()
      title.value = ''
      description.value = ''
      tagsInput.value = ''
      uploadSuccess.value = false
      reset()
      emit('uploaded')
    }, 2000)
  } catch {
    // Error is already captured in state.error
  }
}

onUnmounted(() => {
  if (preview.value) URL.revokeObjectURL(preview.value)
})
</script>

<template>
  <form class="space-y-6" @submit.prevent="handleSubmit">
    <!-- Drop zone / file picker -->
    <div
      class="relative rounded-xl border-2 border-dashed transition-colors"
      :class="file ? 'border-emerald-300 bg-emerald-50/50' : 'border-stone-300 bg-white hover:border-emerald-400 hover:bg-emerald-50/30'"
      @drop="onDrop"
      @dragover="onDragOver"
    >
      <input
        ref="fileInputRef"
        type="file"
        accept="image/jpeg,image/png,image/webp,image/gif"
        class="hidden"
        @change="onFileChange"
      >

      <!-- Preview -->
      <div v-if="file && preview" class="p-4">
        <div class="relative mx-auto max-w-md overflow-hidden rounded-lg">
          <img :src="preview" :alt="file.name" class="w-full rounded-lg object-cover" style="max-height: 320px">
          <button
            type="button"
            class="absolute right-2 top-2 rounded-full bg-black/50 p-1 text-white hover:bg-black/70 transition-colors"
            @click="removeFile"
          >
            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
              <path fill-rule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clip-rule="evenodd" />
            </svg>
          </button>
        </div>
        <p class="mt-2 text-center text-xs text-stone-500">
          {{ file.name }} &mdash; {{ (file.size / (1024 * 1024)).toFixed(1) }} MB
        </p>
      </div>

      <!-- Placeholder -->
      <div v-else class="flex flex-col items-center justify-center px-6 py-16 text-center">
        <svg xmlns="http://www.w3.org/2000/svg" class="mb-3 h-12 w-12 text-stone-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
        </svg>
        <p class="text-sm font-medium text-stone-600">
          Drag & drop an image here, or
          <button type="button" class="text-emerald-600 underline hover:text-emerald-700" @click="openFilePicker">
            browse
          </button>
        </p>
        <p class="mt-1 text-xs text-stone-400">
          JPEG, PNG, WebP or GIF &mdash; up to {{ MAX_SIZE_MB }} MB
        </p>
      </div>
    </div>

    <!-- Validation error -->
    <p v-if="validationError" class="rounded-lg bg-red-50 px-4 py-2 text-sm text-red-600">
      {{ validationError }}
    </p>

    <!-- Metadata fields (only shown when a file is selected) -->
    <template v-if="file">
      <div>
        <label for="title" class="block text-sm font-medium text-stone-700">Title</label>
        <input
          id="title"
          v-model="title"
          type="text"
          class="mt-1 block w-full rounded-lg border border-stone-300 px-3 py-2 text-sm shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          placeholder="Give your photo a title"
        >
      </div>

      <div>
        <label for="description" class="block text-sm font-medium text-stone-700">Description</label>
        <textarea
          id="description"
          v-model="description"
          rows="3"
          class="mt-1 block w-full rounded-lg border border-stone-300 px-3 py-2 text-sm shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          placeholder="Optional description"
        />
      </div>

      <div>
        <label for="tags" class="block text-sm font-medium text-stone-700">Tags</label>
        <input
          id="tags"
          v-model="tagsInput"
          type="text"
          class="mt-1 block w-full rounded-lg border border-stone-300 px-3 py-2 text-sm shadow-sm focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          placeholder="nature, garden, flowers (comma-separated)"
        >
        <div v-if="tags.length" class="mt-2 flex flex-wrap gap-1.5">
          <span
            v-for="tag in tags"
            :key="tag"
            class="rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-medium text-emerald-700"
          >
            {{ tag }}
          </span>
        </div>
      </div>

      <!-- Upload progress -->
      <UploadProgress
        v-if="state.isUploading"
        :progress="state.progress"
      />

      <!-- Error message -->
      <p v-if="state.error" class="rounded-lg bg-red-50 px-4 py-2 text-sm text-red-600">
        {{ state.error }}
      </p>

      <!-- Success message -->
      <p v-if="uploadSuccess" class="rounded-lg bg-emerald-50 px-4 py-3 text-sm font-medium text-emerald-700">
        Photo uploaded successfully!
      </p>

      <!-- Submit button -->
      <button
        type="submit"
        :disabled="state.isUploading || uploadSuccess"
        class="w-full rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
      >
        <span v-if="state.isUploading">Uploading...</span>
        <span v-else-if="uploadSuccess">Done!</span>
        <span v-else>Upload Photo</span>
      </button>
    </template>
  </form>
</template>
