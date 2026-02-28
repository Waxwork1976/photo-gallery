<script setup lang="ts">
import type { FolderTreeNodeDto, ImageDto } from '~/types/image'
import { useTranslations } from '~/composables/useTranslations'

const api = useApi()
const { t } = useTranslations()

const images = ref<ImageDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedImage = ref<ImageDto | null>(null)
const folderTree = ref<FolderTreeNodeDto[]>([])
const folderTagRules = ref<Record<string, string>>({})
const selectedFolderPath = ref<string | null>(null)

const fetchImages = async () => {
  loading.value = true
  error.value = null

  try {
    const data = await api.listImages()
    images.value = data.images
  } catch (err: unknown) {
    error.value = err instanceof Error ? err.message : t('gallery.loadError')
  } finally {
    loading.value = false
  }
}

const fetchFolderTree = async () => {
  try {
    const data = await api.getFolderTree()
    folderTree.value = data.tree
    folderTagRules.value = data.tagRules || {}
  } catch {
    folderTree.value = []
    folderTagRules.value = {}
  }
}

const filteredImages = computed(() => {
  if (!selectedFolderPath.value)
    return images.value
  const selected = selectedFolderPath.value
  return images.value.filter((img) => {
    const mappedFromTags = (img.tags ?? [])
      .map((tag) => folderTagRules.value[tag])
      .filter((p): p is string => Boolean(p))

    return img.folderPaths?.some((p) => p === selected || p.startsWith(`${selected}/`))
      || img.primaryFolderPath === selected
      || img.primaryFolderPath?.startsWith(`${selected}/`)
      || mappedFromTags.some((p) => p === selected || p.startsWith(`${selected}/`))
  })
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
        {{ t('gallery.title') }}
      </h1>
      <p class="mt-2 text-base text-stone-500">
        {{ t('gallery.subtitle') }}
      </p>
    </section>

    <!-- Error -->
    <div
      v-if="error"
      class="mb-8 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-600"
    >
      {{ error }}
      <button class="ml-2 font-medium underline hover:text-red-700" @click="fetchImages">
        {{ t('gallery.retry') }}
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
