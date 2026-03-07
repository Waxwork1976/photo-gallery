<script setup lang="ts">
import type { FolderTreeNodeDto, ImageDto } from '~/types/image'
import { useTranslations } from '~/composables/useTranslations'

const api = useApi()
const { t, locale } = useTranslations()

const images = ref<ImageDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const selectedImage = ref<ImageDto | null>(null)
const suggestionImage = ref<ImageDto | null>(null)
const taxonomySuggestionFeedback = ref<string | null>(null)
const folderTree = ref<FolderTreeNodeDto[]>([])
const folderTagRules = ref<Record<string, string>>({})
const folderRootLabels = ref<Record<string, string>>({})
const folderMode = ref<'primary' | 'yearMonth' | 'alphabetical'>('primary')
const selectedPrimaryFolderPath = ref<string | null>(null)
const selectedYearMonthPath = ref<string | null>(null)
const selectedAlphabeticalPath = ref<string | null>(null)
const slideshowIntervalSeconds = ref(4)
const slideshowPhotoCount = ref(8)
const slideshowTransitionSeconds = ref(0.8)
const slideshowSeed = ref(0)
const searchQuery = ref('')
const searchSuggestions = ref<string[]>([])
const searchLoading = ref(false)
const searchError = ref<string | null>(null)
let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null
type BreadcrumbItem = {
  label: string
  path: string | null
}

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

const findFirstPrimaryLeafPath = (node: FolderTreeNodeDto | null): string | null => {
  if (!node) return null
  if ((node.children?.length ?? 0) === 0) return node.path

  for (const child of node.children ?? []) {
    const leafPath = findFirstPrimaryLeafPath(child)
    if (leafPath) return leafPath
  }
  return null
}

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

type AlphabeticalNode = {
  name: string
  path: string
}

const findFirstYearMonthLeafPath = (node: YearMonthNode | null): string | null => {
  if (!node) return null
  if ((node.children?.length ?? 0) === 0) return node.path

  for (const child of node.children ?? []) {
    const leafPath = findFirstYearMonthLeafPath(child)
    if (leafPath) return leafPath
  }
  return null
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

const alphabeticalDisplayNameForImage = (image: ImageDto) => {
  const localized = (image.commonNames?.[locale.value] ?? '').trim()
  if (localized) return localized
  return (image.scientificName ?? '').trim()
}

const alphabeticalLetterForName = (value: string) => {
  const normalized = value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
  if (!normalized) return '#'
  const firstChar = normalized[0]?.toUpperCase() ?? '#'
  return /^[A-Z]$/.test(firstChar) ? firstChar : '#'
}

const alphabeticalBuckets = computed(() => {
  const buckets = new Map<string, ImageDto[]>()
  for (const image of images.value) {
    const displayName = alphabeticalDisplayNameForImage(image)
    const letter = alphabeticalLetterForName(displayName)
    if (!buckets.has(letter)) {
      buckets.set(letter, [])
    }
    buckets.get(letter)!.push(image)
  }

  for (const [letter, entries] of buckets) {
    entries.sort((a, b) => {
      const nameA = alphabeticalDisplayNameForImage(a)
      const nameB = alphabeticalDisplayNameForImage(b)
      const byName = nameA.localeCompare(nameB, locale.value)
      if (byName !== 0) return byName
      return a.id.localeCompare(b.id)
    })
    buckets.set(letter, entries)
  }

  return buckets
})

const alphabeticalTree = computed<AlphabeticalNode[]>(() => {
  const letters = [...alphabeticalBuckets.value.keys()].sort((a, b) => {
    if (a === '#') return 1
    if (b === '#') return -1
    return a.localeCompare(b, locale.value)
  })

  return letters.map(letter => ({
    name: letter,
    path: `az/${letter}`,
  }))
})

const alphabeticalTreeAsFolderNodes = computed<FolderTreeNodeDto[]>(() => {
  return alphabeticalTree.value.map(node => ({
    name: node.name,
    path: node.path,
    children: [],
  }))
})

const alphabeticalTreeIndex = computed(() => {
  const map = new Map<string, AlphabeticalNode>()
  for (const node of alphabeticalTree.value) {
    map.set(node.path, node)
  }
  return map
})

const selectedAlphabeticalNode = computed(() => {
  if (!selectedAlphabeticalPath.value) return null
  return alphabeticalTreeIndex.value.get(selectedAlphabeticalPath.value) ?? null
})

const alphabeticalViewMode = computed<'root' | 'leaf'>(() => {
  if (!selectedAlphabeticalNode.value) return 'root'
  return 'leaf'
})

const alphabeticalDisplayFolderNodes = computed<FolderTreeNodeDto[]>(() => {
  if (alphabeticalViewMode.value === 'root') return alphabeticalTreeAsFolderNodes.value
  return []
})

const alphabeticalImagesForPath = (path: string) => {
  const match = path.match(/^az\/(.+)$/)
  if (!match?.[1]) return []
  return alphabeticalBuckets.value.get(match[1]) ?? []
}

const activeViewMode = computed<'root' | 'intermediate' | 'leaf'>(() => {
  if (folderMode.value === 'primary') return primaryViewMode.value
  if (folderMode.value === 'yearMonth') return yearMonthViewMode.value
  return alphabeticalViewMode.value
})

const activeDisplayFolderNodes = computed<FolderTreeNodeDto[]>(() => {
  if (folderMode.value === 'primary') return primaryDisplayFolderNodes.value
  if (folderMode.value === 'yearMonth') return yearMonthDisplayFolderNodes.value
  return alphabeticalDisplayFolderNodes.value
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
  return folderMode.value === 'yearMonth' ? primaryContextImages.value : images.value
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
  if (folderMode.value === 'yearMonth') {
    if (!selectedYearMonthPath.value) return []
    return yearMonthImagesForPath(selectedYearMonthPath.value)
  }
  if (!selectedAlphabeticalPath.value) return []
  return alphabeticalImagesForPath(selectedAlphabeticalPath.value)
})

const folderCardPreviewMap = ref<Record<string, ImageDto | null>>({})

const normalizeSearchText = (value: string) => value.trim().toLowerCase()

const searchableTextForImage = (image: ImageDto) => {
  const parts: string[] = [
    image.title ?? '',
    image.description ?? '',
    ...(image.tags ?? []),
    image.scientificName ?? '',
    image.taxonomyOrder ?? '',
    image.taxonomyFamily ?? '',
    image.taxonomyGenus ?? '',
  ]

  const localizedCommon = image.commonNames?.[locale.value]
  const englishCommon = image.commonNames?.en
  if (localizedCommon) parts.push(localizedCommon)
  if (englishCommon && englishCommon !== localizedCommon) parts.push(englishCommon)
  if (image.commonNames) {
    parts.push(...Object.values(image.commonNames))
  }

  return normalizeSearchText(parts.join(' '))
}

const buildContextSuggestionSet = (images: ImageDto[]) => {
  const values = new Set<string>()
  for (const image of images) {
    for (const tag of image.tags ?? []) {
      const cleaned = (tag ?? '').trim()
      if (cleaned) values.add(cleaned.toLowerCase())
    }

    for (const field of [
      image.scientificName,
      image.taxonomyOrder,
      image.taxonomyFamily,
      image.taxonomyGenus,
    ]) {
      const cleaned = (field ?? '').trim()
      if (cleaned) values.add(cleaned.toLowerCase())
    }

    for (const commonName of Object.values(image.commonNames ?? {})) {
      const cleaned = (commonName ?? '').trim()
      if (cleaned) values.add(cleaned.toLowerCase())
    }
  }
  return values
}

const activeSearchSourceImages = computed(() => {
  if (activeViewMode.value === 'leaf') return leafImages.value
  return activeSlideshowSourceImages.value
})

const filteredSearchImages = computed(() => {
  const query = normalizeSearchText(searchQuery.value)
  if (!query) return activeSearchSourceImages.value

  return activeSearchSourceImages.value.filter((img) => {
    const searchable = searchableTextForImage(img)
    return searchable.includes(query)
  })
})

const isSearchActive = computed(() => normalizeSearchText(searchQuery.value).length > 0)

const clearSearch = () => {
  searchQuery.value = ''
  searchSuggestions.value = []
  searchError.value = null
}

const openTaxonomySuggestion = (image: ImageDto) => {
  if (activeViewMode.value !== 'leaf')
    return
  suggestionImage.value = image
}

const closeTaxonomySuggestion = () => {
  suggestionImage.value = null
}

const onTaxonomyDraftSaved = () => {
  taxonomySuggestionFeedback.value = t('gallery.taxonomySuggest.saved')
}

const runSearchSuggestions = async (query: string) => {
  const normalized = normalizeSearchText(query)
  if (normalized.length < 3) {
    searchSuggestions.value = []
    searchError.value = null
    return
  }

  searchLoading.value = true
  searchError.value = null
  try {
    const response = await api.getSearchSuggestions(normalized, locale.value, 12)
    const contextValues = buildContextSuggestionSet(activeSearchSourceImages.value)
    searchSuggestions.value = response.suggestions
      .map(item => item.value)
      .filter(value => contextValues.has(value.trim().toLowerCase()))
  } catch {
    searchSuggestions.value = []
    searchError.value = t('gallery.search.loadError')
  } finally {
    searchLoading.value = false
  }
}

watch(searchQuery, (value) => {
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
  searchDebounceTimer = setTimeout(() => {
    runSearchSuggestions(value)
  }, 250)
})

watch(locale, () => {
  if (normalizeSearchText(searchQuery.value).length >= 3) {
    runSearchSuggestions(searchQuery.value)
  }
})

watch(
  [folderMode, selectedPrimaryFolderPath, selectedPrimaryNode, images, folderTree, folderTagRules],
  () => {
    if (folderMode.value !== 'primary') return
    const selectedPath = selectedPrimaryFolderPath.value
    const selectedNode = selectedPrimaryNode.value
    if (!selectedPath || !selectedNode) return
    if ((selectedNode.children?.length ?? 0) === 0) return

    const subtreeCount = imagesForSubtree(selectedPath).length
    if (subtreeCount >= 2) return

    const targetLeafPath = findFirstPrimaryLeafPath(selectedNode)
    if (!targetLeafPath || targetLeafPath === selectedPath) return

    selectedPrimaryFolderPath.value = targetLeafPath
  },
  { immediate: true },
)

watch(
  [folderMode, selectedYearMonthPath, selectedYearMonthNode, yearMonthTree, primaryContextImages],
  () => {
    if (folderMode.value !== 'yearMonth') return
    const selectedPath = selectedYearMonthPath.value
    const selectedNode = selectedYearMonthNode.value
    if (!selectedPath || !selectedNode) return
    if ((selectedNode.children?.length ?? 0) === 0) return

    const subtreeCount = yearMonthImagesForPath(selectedPath).length
    if (subtreeCount >= 2) return

    const targetLeafPath = findFirstYearMonthLeafPath(selectedNode)
    if (!targetLeafPath || targetLeafPath === selectedPath) return

    selectedYearMonthPath.value = targetLeafPath
  },
  { immediate: true },
)

watch(
  [folderMode, selectedAlphabeticalPath, alphabeticalTreeIndex],
  () => {
    if (folderMode.value !== 'alphabetical') return
    const selectedPath = selectedAlphabeticalPath.value
    if (!selectedPath) return
    if (alphabeticalTreeIndex.value.has(selectedPath)) return
    selectedAlphabeticalPath.value = null
  },
  { immediate: true },
)

watch(
  [activeDisplayFolderNodes, images, selectedPrimaryFolderPath, selectedYearMonthPath, folderMode],
  () => {
    const next: Record<string, ImageDto | null> = {}
    for (const node of activeDisplayFolderNodes.value) {
      const candidates = folderMode.value === 'primary'
        ? imagesForSubtree(node.path)
        : folderMode.value === 'yearMonth'
          ? yearMonthImagesForPath(node.path)
          : alphabeticalImagesForPath(node.path)
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

const primaryLabelForPath = (path: string) => {
  if (path === 'photos') {
    const key = folderRootLabels.value[path] || 'folders.root.photos'
    const translated = t(key)
    return translated === key ? 'Photos' : translated
  }

  const node = treeIndex.value.get(path)
  if (node)
    return localizedFolderLabel(node)

  const parts = path.split('/').filter(Boolean)
  return humanizeSegment(parts[parts.length - 1] || path)
}

const getPrimaryBreadcrumb = (): BreadcrumbItem[] => {
  const items: BreadcrumbItem[] = [{ label: primaryLabelForPath('photos'), path: null }]
  const selectedPath = selectedPrimaryFolderPath.value
  if (!selectedPath)
    return items

  const parts = normalizePath(selectedPath).split('/').filter(Boolean)
  let cumulative = ''
  for (const part of parts) {
    cumulative = cumulative ? `${cumulative}/${part}` : part
    if (cumulative === 'photos')
      continue
    items.push({
      label: primaryLabelForPath(cumulative),
      path: cumulative,
    })
  }

  return items
}

const getYearMonthBreadcrumb = (): BreadcrumbItem[] => {
  const items: BreadcrumbItem[] = [{ label: t('folders.mode.yearMonth'), path: null }]
  const selectedPath = selectedYearMonthPath.value
  if (!selectedPath)
    return items

  const match = selectedPath.match(/^ym\/(\d{4})(?:\/(\d{2}))?$/)
  if (!match)
    return items

  const year = match[1]
  if (!year)
    return items
  const month = match[2]
  items.push({ label: year, path: `ym/${year}` })
  if (month) {
    items.push({ label: month, path: `ym/${year}/${month}` })
  }
  return items
}

const getAlphabeticalBreadcrumb = (): BreadcrumbItem[] => {
  const items: BreadcrumbItem[] = [{ label: t('folders.mode.alphabetical'), path: null }]
  const selectedPath = selectedAlphabeticalPath.value
  if (!selectedPath) return items

  const match = selectedPath.match(/^az\/(.+)$/)
  const letter = match?.[1]
  if (!letter) return items
  items.push({ label: letter, path: selectedPath })
  return items
}

const activeBreadcrumb = computed(() => {
  if (folderMode.value === 'primary') return getPrimaryBreadcrumb()
  if (folderMode.value === 'yearMonth') return getYearMonthBreadcrumb()
  return getAlphabeticalBreadcrumb()
})

const navigateToBreadcrumb = (path: string | null) => {
  if (folderMode.value === 'primary') {
    selectedPrimaryFolderPath.value = path
    return
  }
  if (folderMode.value === 'yearMonth') {
    selectedYearMonthPath.value = path
    return
  }
  selectedAlphabeticalPath.value = path
}

const activeTree = computed<FolderTreeNodeDto[]>(() => {
  if (folderMode.value === 'primary') return folderTree.value
  if (folderMode.value === 'yearMonth') return yearMonthTreeAsFolderNodes.value
  return alphabeticalTreeAsFolderNodes.value
})

const activeRootLabels = computed<Record<string, string>>(() => {
  if (folderMode.value === 'primary') return folderRootLabels.value
  return {}
})

const activeSelectedPath = computed<string | null>(() => {
  if (folderMode.value === 'primary') return selectedPrimaryFolderPath.value
  if (folderMode.value === 'yearMonth') return selectedYearMonthPath.value
  return selectedAlphabeticalPath.value
})

const onSelectTreePath = (path: string) => {
  if (folderMode.value === 'primary') {
    selectedPrimaryFolderPath.value = path
    return
  }
  if (folderMode.value === 'yearMonth') {
    selectedYearMonthPath.value = path
    return
  }
  selectedAlphabeticalPath.value = path
}

const onClearTreeSelection = () => {
  if (folderMode.value === 'primary') {
    selectedPrimaryFolderPath.value = null
    return
  }
  if (folderMode.value === 'yearMonth') {
    selectedYearMonthPath.value = null
    return
  }
  selectedAlphabeticalPath.value = null
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

onUnmounted(() => {
  if (searchDebounceTimer) {
    clearTimeout(searchDebounceTimer)
    searchDebounceTimer = null
  }
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

    <section class="mb-6 space-y-2">
      <div class="flex flex-col gap-2 sm:flex-row">
        <input
          v-model="searchQuery"
          type="text"
          class="ui-input sm:flex-1"
          :placeholder="t('gallery.search.placeholder')"
        >
        <button
          v-if="isSearchActive"
          type="button"
          class="btn-secondary"
          @click="clearSearch"
        >
          {{ t('gallery.search.clear') }}
        </button>
      </div>

      <p v-if="searchError" class="text-xs text-red-600">
        {{ searchError }}
      </p>

      <div v-if="normalizeSearchText(searchQuery).length >= 3 && searchSuggestions.length" class="rounded-lg border border-stone-200 bg-white p-2">
        <p class="px-2 pb-1 text-xs font-medium text-stone-500">
          {{ t('gallery.search.suggestions') }}
        </p>
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="suggestion in searchSuggestions"
            :key="suggestion"
            type="button"
            class="btn-secondary rounded-md px-2 py-1 text-xs"
            @click="searchQuery = suggestion"
          >
            {{ suggestion }}
          </button>
        </div>
      </div>
      <p v-else-if="normalizeSearchText(searchQuery).length > 0 && normalizeSearchText(searchQuery).length < 3" class="text-xs text-stone-500">
        {{ t('gallery.search.minChars', { count: 3 }) }}
      </p>
      <p v-if="searchLoading" class="text-xs text-stone-500">
        {{ t('gallery.search.loading') }}
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
          <button
            type="button"
            class="btn-secondary rounded-md px-3 py-1.5 text-xs"
            :class="folderMode === 'alphabetical' ? '!bg-emerald-100 !text-emerald-800' : ''"
            @click="folderMode = 'alphabetical'"
          >
            {{ t('folders.mode.alphabetical') }}
          </button>
        </div>

        <GalleryFolderTree
          :tree="activeTree"
          :root-labels="activeRootLabels"
          :selected-path="activeSelectedPath"
          @select="onSelectTreePath"
          @clear="onClearTreeSelection"
        />
      </section>

      <section class="min-w-0 space-y-6">
        <p v-if="taxonomySuggestionFeedback" class="alert-success">
          {{ taxonomySuggestionFeedback }}
          <span class="ml-1 text-emerald-700">{{ t('gallery.taxonomySuggest.savedDetail') }}</span>
        </p>

        <nav class="overflow-x-auto rounded-md border border-stone-200 bg-white px-2 py-1 text-xs text-stone-600">
          <ol class="flex min-w-max items-center gap-1">
            <li
              v-for="(item, idx) in activeBreadcrumb"
              :key="`${item.path ?? 'root'}-${idx}`"
              class="flex items-center gap-1"
            >
              <button
                type="button"
                class="ui-focus-ring ui-transition-color rounded px-1 py-0.5"
                :class="idx === activeBreadcrumb.length - 1 ? 'font-semibold text-stone-900' : 'hover:bg-stone-100 hover:text-stone-800'"
                @click="navigateToBreadcrumb(item.path)"
              >
                {{ item.label }}
              </button>
              <span v-if="idx < activeBreadcrumb.length - 1" class="text-stone-400">/</span>
            </li>
          </ol>
        </nav>

        <GalleryImageGrid
          v-if="loading"
          :images="[]"
          :loading="true"
        />

        <template v-else-if="isSearchActive">
          <p class="text-sm text-stone-600">
            {{ t('gallery.search.resultsCount', { count: filteredSearchImages.length }) }}
          </p>
          <GalleryImageGrid
            :images="filteredSearchImages"
            :loading="false"
            :show-suggest-button="activeViewMode === 'leaf'"
            @select="selectedImage = $event"
            @suggest-taxonomy="openTaxonomySuggestion"
          />
          <p v-if="filteredSearchImages.length === 0" class="text-sm text-stone-500">
            {{ t('gallery.search.noResults') }}
          </p>
        </template>

        <template v-else-if="activeViewMode === 'leaf'">
          <GalleryImageGrid
            :images="leafImages"
            :loading="false"
            :show-suggest-button="true"
            @select="selectedImage = $event"
            @suggest-taxonomy="openTaxonomySuggestion"
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
            @select="onSelectTreePath"
          />
        </template>
      </section>
    </section>

    <!-- Lightbox -->
    <GalleryImageLightbox
      :image="selectedImage"
      @close="selectedImage = null"
    />

    <GalleryTaxonomySuggestionModal
      :image="suggestionImage"
      @close="closeTaxonomySuggestion"
      @draft-saved="onTaxonomyDraftSaved"
    />
  </div>
</template>
