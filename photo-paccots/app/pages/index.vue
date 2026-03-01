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
const folderMode = ref<'primary' | 'yearMonth'>('primary')
const selectedPrimaryFolderPath = ref<string | null>(null)
const selectedYearMonthPath = ref<string | null>(null)
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

const selectedPrimaryNode = computed(() => {
  if (!selectedPrimaryFolderPath.value) return null
  return treeIndex.value.get(selectedPrimaryFolderPath.value) ?? null
})

const isPrimaryLeafSelection = computed(() => {
  if (!selectedPrimaryNode.value) return false
  return (selectedPrimaryNode.value.children?.length ?? 0) === 0
})

const primaryViewMode = computed<'root' | 'intermediate' | 'leaf'>(() => {
  if (!selectedPrimaryNode.value) return 'root'
  return isPrimaryLeafSelection.value ? 'leaf' : 'intermediate'
})

const secondLevelNodes = computed(() => {
  if (!rootNode.value) return []
  const grandchildren = rootNode.value.children.flatMap(child => child.children ?? [])
  return grandchildren
})

const primaryDisplayFolderNodes = computed(() => {
  if (primaryViewMode.value === 'root') return secondLevelNodes.value
  if (primaryViewMode.value === 'intermediate') return selectedPrimaryNode.value?.children ?? []
  return []
})

const primaryContextImages = computed(() => {
  if (!selectedPrimaryFolderPath.value) return images.value
  return imagesForSubtree(selectedPrimaryFolderPath.value)
})

type YearMonthNode = {
  name: string
  path: string
  children: YearMonthNode[]
}

const yearMonthTree = computed<YearMonthNode[]>(() => {
  const grouped = new Map<string, Set<string>>()
  for (const img of primaryContextImages.value) {
    const date = new Date(img.uploadedAt)
    if (Number.isNaN(date.getTime())) continue
    const year = String(date.getFullYear())
    const month = String(date.getMonth() + 1).padStart(2, '0')
    if (!grouped.has(year)) grouped.set(year, new Set())
    grouped.get(year)!.add(month)
  }

  const years = [...grouped.keys()].sort((a, b) => b.localeCompare(a))
  return years.map((year) => {
    const months = [...(grouped.get(year) ?? new Set())].sort((a, b) => b.localeCompare(a))
    return {
      name: year,
      path: `ym/${year}`,
      children: months.map(month => ({
        name: `${year}-${month}`,
        path: `ym/${year}/${month}`,
        children: [],
      })),
    }
  })
})

const yearMonthTreeAsFolderNodes = computed<FolderTreeNodeDto[]>(() => {
  const toNode = (n: YearMonthNode): FolderTreeNodeDto => ({
    name: n.name,
    path: n.path,
    children: n.children.map(toNode),
  })
  return yearMonthTree.value.map(toNode)
})

const yearMonthTreeIndex = computed(() => {
  const map = new Map<string, YearMonthNode>()
  const walk = (node: YearMonthNode) => {
    map.set(node.path, node)
    for (const child of node.children) walk(child)
  }
  for (const node of yearMonthTree.value) walk(node)
  return map
})

const selectedYearMonthNode = computed(() => {
  if (!selectedYearMonthPath.value) return null
  return yearMonthTreeIndex.value.get(selectedYearMonthPath.value) ?? null
})

const yearMonthViewMode = computed<'root' | 'intermediate' | 'leaf'>(() => {
  if (!selectedYearMonthNode.value) return 'root'
  return selectedYearMonthNode.value.children.length === 0 ? 'leaf' : 'intermediate'
})

const yearMonthDisplayFolderNodes = computed<FolderTreeNodeDto[]>(() => {
  if (yearMonthViewMode.value === 'root') return yearMonthTreeAsFolderNodes.value
  if (yearMonthViewMode.value === 'intermediate') {
    const parent = selectedYearMonthNode.value
    if (!parent) return []
    return parent.children.map(child => ({
      name: child.name,
      path: child.path,
      children: [],
    }))
  }
  return []
})

const imageInYearMonthPath = (img: ImageDto, path: string) => {
  const match = path.match(/^ym\/(\d{4})(?:\/(\d{2}))?$/)
  if (!match) return false
  const year = match[1]
  const month = match[2] ?? null
  const date = new Date(img.uploadedAt)
  if (Number.isNaN(date.getTime())) return false
  const imgYear = String(date.getFullYear())
  const imgMonth = String(date.getMonth() + 1).padStart(2, '0')
  return month ? (imgYear === year && imgMonth === month) : (imgYear === year)
}

const yearMonthImagesForPath = (path: string) =>
  primaryContextImages.value.filter(img => imageInYearMonthPath(img, path))

const activeViewMode = computed<'root' | 'intermediate' | 'leaf'>(() => {
  return folderMode.value === 'primary' ? primaryViewMode.value : yearMonthViewMode.value
})

const activeDisplayFolderNodes = computed<FolderTreeNodeDto[]>(() => {
  return folderMode.value === 'primary' ? primaryDisplayFolderNodes.value : yearMonthDisplayFolderNodes.value
})

const activeSlideshowSourceImages = computed(() => {
  if (activeViewMode.value === 'leaf') return []
  if (folderMode.value === 'primary') {
    if (primaryViewMode.value === 'intermediate' && selectedPrimaryFolderPath.value)
      return imagesForSubtree(selectedPrimaryFolderPath.value)
    return images.value
  }
  if (yearMonthViewMode.value === 'intermediate' && selectedYearMonthPath.value)
    return yearMonthImagesForPath(selectedYearMonthPath.value)
  return primaryContextImages.value
})

watch([activeSlideshowSourceImages, slideshowPhotoCount], () => {
  slideshowSeed.value++
}, { immediate: true })

const orderedSlideshowImages = computed(() => {
  void slideshowSeed.value
  const max = Math.max(1, slideshowPhotoCount.value)
  return shuffled(activeSlideshowSourceImages.value).slice(0, max)
})

const leafImages = computed(() => {
  if (activeViewMode.value !== 'leaf') return []
  if (folderMode.value === 'primary') {
    if (!selectedPrimaryFolderPath.value) return []
    return imagesForSubtree(selectedPrimaryFolderPath.value)
  }
  if (!selectedYearMonthPath.value) return []
  return yearMonthImagesForPath(selectedYearMonthPath.value)
})

const folderCardPreviewMap = ref<Record<string, ImageDto | null>>({})

watch(
  [activeDisplayFolderNodes, images, selectedPrimaryFolderPath, selectedYearMonthPath, folderMode],
  () => {
    const next: Record<string, ImageDto | null> = {}
    for (const node of activeDisplayFolderNodes.value) {
      const candidates = folderMode.value === 'primary'
        ? imagesForSubtree(node.path)
        : yearMonthImagesForPath(node.path)
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
  if (node.path.startsWith('ym/')) {
    if (/^ym\/\d{4}$/.test(node.path)) return node.name
    if (/^ym\/\d{4}\/\d{2}$/.test(node.path)) {
      const month = node.path.split('/')[2] || node.name
      return month
    }
  }
  const key = folderRootLabels.value[node.path]
  if (key) {
    const translated = t(key)
    if (translated !== key) return translated
  }
  return humanizeSegment(node.name) || node.name
}

const folderCards = computed(() => {
  return activeDisplayFolderNodes.value.map(node => ({
    path: node.path,
    label: localizedFolderLabel(node),
    thumbnailUrl: folderCardPreviewMap.value[node.path]?.thumbnailUrl ?? null,
  }))
})

const pageBackgroundImageUrl = computed(() => {
  if (activeViewMode.value === 'leaf') {
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
  <div class="relative isolate overflow-x-hidden">
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
      class="alert-error mb-8"
    >
      {{ error }}
      <button class="ui-focus-ring ui-transition-color ml-2 rounded-sm font-medium underline hover:text-red-800" @click="fetchImages">
        {{ t('gallery.retry') }}
      </button>
    </div>

    <section class="mb-8 grid gap-6 lg:grid-cols-[260px_1fr]">
      <section class="min-w-0 space-y-3">
        <div class="flex flex-wrap gap-2">
          <button
            type="button"
            class="btn-secondary rounded-md px-3 py-1.5 text-xs"
            :class="folderMode === 'primary' ? '!bg-emerald-100 !text-emerald-800' : ''"
            @click="folderMode = 'primary'"
          >
            {{ t('folders.mode.primary') }}
          </button>
          <button
            type="button"
            class="btn-secondary rounded-md px-3 py-1.5 text-xs"
            :class="folderMode === 'yearMonth' ? '!bg-emerald-100 !text-emerald-800' : ''"
            @click="folderMode = 'yearMonth'"
          >
            {{ t('folders.mode.yearMonth') }}
          </button>
        </div>

        <GalleryFolderTree
          :tree="folderMode === 'primary' ? folderTree : yearMonthTreeAsFolderNodes"
          :root-labels="folderMode === 'primary' ? folderRootLabels : {}"
          :selected-path="folderMode === 'primary' ? selectedPrimaryFolderPath : selectedYearMonthPath"
          @select="folderMode === 'primary' ? selectedPrimaryFolderPath = $event : selectedYearMonthPath = $event"
          @clear="folderMode === 'primary' ? selectedPrimaryFolderPath = null : selectedYearMonthPath = null"
        />
      </section>

      <section class="min-w-0 space-y-6">
        <GalleryImageGrid
          v-if="loading"
          :images="[]"
          :loading="true"
        />

        <template v-else-if="activeViewMode === 'leaf'">
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
            @select="folderMode === 'primary' ? selectedPrimaryFolderPath = $event : selectedYearMonthPath = $event"
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
