<script setup lang="ts">
import type { ImageDto } from '~/types/image'

const api = useApi()

const images = ref<ImageDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedImage = ref<ImageDto | null>(null)

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

onMounted(() => {
  fetchImages()
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

    <!-- Grid -->
    <GalleryImageGrid
      :images="images"
      :loading="loading"
      @select="selectedImage = $event"
    />

    <!-- Lightbox -->
    <GalleryImageLightbox
      :image="selectedImage"
      @close="selectedImage = null"
    />
  </div>
</template>
