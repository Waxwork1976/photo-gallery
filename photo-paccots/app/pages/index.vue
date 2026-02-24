<script setup lang="ts">
import type { FolderTreeNodeDto, ImageDto } from '~/types/image'

const api = useApi()

const images = ref<ImageDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedImage = ref<ImageDto | null>(null)
const folderTree = ref<FolderTreeNodeDto[]>([])
const selectedFolderPath = ref<string | null>(null)

const fetchImages = async () => {
  loading.value = true
  error.value = null

  try {
    const data = await api.listImages()
    images.value = data.images
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : 'Failed to load images'
  } finally {
    loading.value = false
  }
}

const fetchFolderTree = async () => {
  try {
    const data = await api.getFolderTree()
    folderTree.value = data.tree
  } catch {
    folderTree.value = []
  }
}

const filteredImages = computed(() => {
  if (!selectedFolderPath.value)
    return images.value
  return images.value.filter((img) =>
    img.folderPaths?.includes(selectedFolderPath.value as string)
    || img.primaryFolderPath === selectedFolderPath.value)
})

onMounted(() => {
  fetchImages()
  fetchFolderTree()
})

useHead({
  title: 'Photo Paccots — Gallery',
})
</script>

<template>
  <div>
    <!-- Hero -->
    <section class="mb-10 text-center">
      <h1 class="text-3xl font-bold tracking-tight text-stone-900 sm:text-4xl">
        Nature &amp; Garden Gallery
      </h1>
      <p class="mt-2 text-base text-stone-500">
        A curated collection of nature and garden photography
      </p>
    </section>

    <!-- Error -->
    <div
      v-if="error"
      class="mb-8 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600"
    >
      {{ error }}
      <button class="ml-2 font-medium underline hover:text-red-700" @click="fetchImages">
        Retry
      </button>
    </div>

    <section class="mb-8 grid gap-6 lg:grid-cols-[260px_1fr]">
      <GalleryFolderTree
        :tree="folderTree"
        :selected-path="selectedFolderPath"
        @select="selectedFolderPath = $event"
        @clear="selectedFolderPath = null"
      />

      <!-- Grid -->
      <GalleryImageGrid
        :images="filteredImages"
        :loading="loading"
        @select="selectedImage = $event"
      />
    </section>

    <!-- Lightbox -->
    <GalleryImageLightbox
      :image="selectedImage"
      @close="selectedImage = null"
    />
  </div>
</template>
