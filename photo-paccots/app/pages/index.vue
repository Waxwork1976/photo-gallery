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
const folderRootLabels = ref<Record<string, string>>({})
const selectedFolderPath = ref<string | null>(null)
const slideshowIntervalSeconds = ref(4)
const slideshowPhotoCount = ref(8)
const slideshowTransitionSeconds = ref(0.8)
const slideshowSeed = ref(0)

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
    folderRootLabels.value = data.rootLabels || {}
  } catch {
    folderTree.value = []
    folderTagRules.value = {}
    folderRootLabels.value = {}
  }
}

const fetchSlideshowSettings = async () => {
  try {
    const settings = await api.getSlideshowSettings()
    slideshowPhotoCount.value = Math.max(1, settings.photoCount)
    slideshowIntervalSeconds.value = Math.max(1, settings.intervalSeconds)
    slideshowTransitionSeconds.value = Math.max(0, settings.transitionSeconds ?? 0.8)
  } catch {
    slideshowPhotoCount.value = 8
    slideshowIntervalSeconds.value = 4
    slideshowTransitionSeconds.value = 0.8
  }
}

const normalizePath = (path: string) => path.replace(/\\/g, '/').replace(/\/+/g, '/').replace(/\/$/, '')

const humanizeSegment = (value: string) => value
  .replace(/[-_]+/g, ' ')
  .replace(/\s+/g, ' ')
  .trim()
  .replace(/^\w/, c => c.toUpperCase())

const isInPath = (candidate: string, selected: string) =>
  candidate === selected || candidate.startsWith(`${selected}/`)

const imageInFolderSubtree = (img: ImageDto, selected: string) => {
  const mappedFromTags = (img.tags ?? [])
    .map((tag) => folderTagRules.value[tag])
    .filter((p): p is string => Boolean(p))
    .map(normalizePath)

  const folderPaths = (img.folderPaths ?? []).map(normalizePath)
  const primaryFolderPath = normalizePath(img.primaryFolderPath || 'photos')
  const normalizedSelected = normalizePath(selected)

  return folderPaths.some(p => isInPath(p, normalizedSelected))
    || isInPath(primaryFolderPath, normalizedSelected)
    || mappedFromTags.some(p => isInPath(p, normalizedSelected))
}

const imagesForSubtree = (path: string) => images.value.filter(img => imageInFolderSubtree(img, path))

const shuffled = (input: ImageDto[]) => {
  const arr = [...input]
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[arr[i], arr[j]] = [arr[j]!, arr[i]!]
  }
  return arr
}

const treeIndex = computed(() => {
  const map = new Map<string, FolderTreeNodeDto>()
  const walk = (node: FolderTreeNodeDto) => {
    map.set(node.path, node)
    for (const child of node.children ?? []) {
      walk(child)
    }
  }
  for (const node of folderTree.value) {
    walk(node)
  }
  return map
})

const rootNode = computed(() => {
  if (folderTree.value.length === 0) return null
  return folderTree.value.find(n => n.path === 'photos') ?? folderTree.value[0]
})

const selectedNode = computed(() => {
  if (!selectedFolderPath.value) return null
  return treeIndex.value.get(selectedFolderPath.value) ?? null
})

const isLeafSelection = computed(() => {
  if (!selectedNode.value) return false
  return (selectedNode.value.children?.length ?? 0) === 0
})

const viewMode = computed<'root' | 'intermediate' | 'leaf'>(() => {
  if (!selectedNode.value) return 'root'
  return isLeafSelection.value ? 'leaf' : 'intermediate'
})

const secondLevelNodes = computed(() => {
  if (!rootNode.value) return []
  const grandchildren = rootNode.value.children.flatMap(child => child.children ?? [])
  return grandchildren
})

const displayFolderNodes = computed(() => {
  if (viewMode.value === 'root') return secondLevelNodes.value
  if (viewMode.value === 'intermediate') return selectedNode.value?.children ?? []
  return []
})

const slideshowSourceImages = computed(() => {
  if (viewMode.value === 'leaf') return []
  if (viewMode.value === 'intermediate' && selectedFolderPath.value)
    return imagesForSubtree(selectedFolderPath.value)
  return images.value
})

watch([slideshowSourceImages, slideshowPhotoCount], () => {
  slideshowSeed.value++
}, { immediate: true })

const orderedSlideshowImages = computed(() => {
  void slideshowSeed.value
  const max = Math.max(1, slideshowPhotoCount.value)
  return shuffled(slideshowSourceImages.value).slice(0, max)
})

const leafImages = computed(() => {
  if (viewMode.value !== 'leaf' || !selectedFolderPath.value) return []
  return imagesForSubtree(selectedFolderPath.value)
})

const folderCardPreviewMap = ref<Record<string, ImageDto | null>>({})

watch(
  [displayFolderNodes, images, selectedFolderPath],
  () => {
    const next: Record<string, ImageDto | null> = {}
    for (const node of displayFolderNodes.value) {
      const candidates = imagesForSubtree(node.path)
      if (candidates.length === 0) {
        next[node.path] = null
        continue
      }
      const randomIndex = Math.floor(Math.random() * candidates.length)
      next[node.path] = candidates[randomIndex] ?? null
    }
    folderCardPreviewMap.value = next
  },
  { immediate: true },
)

const localizedFolderLabel = (node: FolderTreeNodeDto) => {
  const key = folderRootLabels.value[node.path]
  if (key) {
    const translated = t(key)
    if (translated !== key) return translated
  }
  return humanizeSegment(node.name) || node.name
}

const folderCards = computed(() => {
  return displayFolderNodes.value.map(node => ({
    path: node.path,
    label: localizedFolderLabel(node),
    thumbnailUrl: folderCardPreviewMap.value[node.path]?.thumbnailUrl ?? null,
  }))
})

const pageBackgroundImageUrl = computed(() => {
  if (viewMode.value === 'leaf') {
    return leafImages.value[0]?.thumbnailUrl ?? null
  }
  return orderedSlideshowImages.value[0]?.thumbnailUrl ?? null
})

onMounted(() => {
  fetchImages()
  fetchFolderTree()
  fetchSlideshowSettings()
})

useHead({
  title: 'Photo Paccots — Gallery',
})
</script>

<template>
  <div class="relative isolate">
    <div
      v-if="pageBackgroundImageUrl"
      class="pointer-events-none fixed inset-0 -z-10"
      aria-hidden="true"
    >
      <img
        :src="pageBackgroundImageUrl"
        alt=""
        class="h-full w-full object-cover opacity-[0.16] saturate-60 blur-[1px]"
      >
      <div class="absolute inset-0 bg-emerald-100/30" />
    </div>

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
        :root-labels="folderRootLabels"
        :selected-path="selectedFolderPath"
        @select="selectedFolderPath = $event"
        @clear="selectedFolderPath = null"
      />

      <section class="space-y-6">
        <GalleryImageGrid
          v-if="loading"
          :images="[]"
          :loading="true"
        />

        <template v-else-if="viewMode === 'leaf'">
          <GalleryImageGrid
            :images="leafImages"
            :loading="false"
            @select="selectedImage = $event"
          />
        </template>

        <template v-else>
          <GalleryRandomSlideshow
            :images="orderedSlideshowImages"
            :interval-seconds="slideshowIntervalSeconds"
            :max-photos="slideshowPhotoCount"
            :transition-seconds="slideshowTransitionSeconds"
          />

          <GalleryFolderCardsGrid
            :cards="folderCards"
            @select="selectedFolderPath = $event"
          />
        </template>
      </section>
    </section>

    <!-- Lightbox -->
    <GalleryImageLightbox
      :image="selectedImage"
      @close="selectedImage = null"
    />
  </div>
</template>
