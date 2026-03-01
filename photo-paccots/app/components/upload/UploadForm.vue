<script setup lang="ts">
import { MagnifyingGlassIcon, PhotoIcon, SparklesIcon, XMarkIcon } from '@heroicons/vue/24/outline'
import type { InsectIdentificationResponse } from '~/types/insect'
import type { PlantNetResult } from '~/types/plantnet'
import { useTranslations } from '~/composables/useTranslations'

const emit = defineEmits<{
  uploaded: []
}>()

const { state, uploadImage, reset } = useUpload()
const api = useApi()
const { user } = useAuth()
const { t, locale } = useTranslations()

const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']
const MAX_SIZE_MB = 10
const MAX_SIZE_BYTES = MAX_SIZE_MB * 1024 * 1024
const GEOLOCATION_TIMEOUT_MS = 5000
const MIN_PLANT_CONFIDENCE = 0.5

// Form fields
const file = ref<File | null>(null)
const preview = ref<string | null>(null)
const title = ref('')
const description = ref('')
const tagsInput = ref('')
const validationError = ref<string | null>(null)
const uploadSuccess = ref(false)
const coordinates = ref<{ latitude: number, longitude: number } | null>(null)
const coordinateSource = ref<'device' | 'exif' | null>(null)
const identifiedSpeciesMetadata = ref<{
  speciesType?: 'plant' | 'bird' | 'insect'
  scientificName?: string
  taxonomyOrder?: string
  taxonomyFamily?: string
  taxonomyGenus?: string
  commonNames?: Record<string, string>
}>({})

// Plant identification
const isIdentifying = ref(false)
const identifyError = ref<string | null>(null)
const identifyLowConfidence = ref<string | null>(null)
const identificationDone = ref(false)
const lastIdentificationType = ref<'plant' | 'bird' | 'insect' | null>(null)
const plantMatches = ref<PlantNetResult[]>([])
const showPlantChooser = ref(false)
const selectedPlantResult = ref<PlantNetResult | null>(null)
const isWildlife = ref(true)

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

const formatDateTime = () => {
  const now = new Date()
  const pad = (n: number) => n.toString().padStart(2, '0')
  return `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}`
}

const formatCoordinates = (lat: number, lon: number) => {
  return `${lat.toFixed(6)}, ${lon.toFixed(6)}`
}

const normalizeCommonNames = (input?: Record<string, string>) => {
  const result: Record<string, string> = { en: '', fr: '', de: '', it: '' }
  for (const key of Object.keys(result)) {
    const value = input?.[key]
    result[key] = (value ?? '').trim()
  }
  return result
}

const localizedCommonName = (commonNames?: Record<string, string>, fallback = '') => {
  const names = normalizeCommonNames(commonNames)
  return names[locale.value] || names.en || fallback
}

const getDeviceCoordinates = async (): Promise<{ latitude: number, longitude: number } | null> => {
  if (!import.meta.client || !navigator.geolocation) return null

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        resolve({
          latitude: pos.coords.latitude,
          longitude: pos.coords.longitude,
        })
      },
      () => resolve(null),
      {
        enableHighAccuracy: true,
        timeout: GEOLOCATION_TIMEOUT_MS,
        maximumAge: 0,
      },
    )
  })
}

const getExifCoordinates = async (selected: File): Promise<{ latitude: number, longitude: number } | null> => {
  if (!selected.type.includes('jpeg') && !selected.type.includes('jpg')) {
    return null
  }

  const buffer = await selected.arrayBuffer()
  const view = new DataView(buffer)

  if (view.getUint16(0, false) !== 0xFFD8) return null

  let offset = 2
  while (offset + 4 < view.byteLength) {
    if (view.getUint8(offset) !== 0xFF) break
    const marker = view.getUint8(offset + 1)
    const length = view.getUint16(offset + 2, false)

    if (marker === 0xE1 && offset + 10 < view.byteLength) {
      const exifHeader = String.fromCharCode(
        view.getUint8(offset + 4),
        view.getUint8(offset + 5),
        view.getUint8(offset + 6),
        view.getUint8(offset + 7),
      )
      if (exifHeader !== 'Exif') return null

      const tiffOffset = offset + 10
      const littleEndian = view.getUint16(tiffOffset, false) === 0x4949
      const get16 = (pos: number) => view.getUint16(pos, littleEndian)
      const get32 = (pos: number) => view.getUint32(pos, littleEndian)
      const typeSize = (type: number) => {
        if (type === 1 || type === 2 || type === 7) return 1
        if (type === 3) return 2
        if (type === 4 || type === 9) return 4
        if (type === 5 || type === 10) return 8
        return 0
      }

      const ifd0Offset = tiffOffset + get32(tiffOffset + 4)
      if (ifd0Offset >= view.byteLength) return null

      const entryCount = get16(ifd0Offset)
      let gpsIfdOffset = 0
      for (let i = 0; i < entryCount; i++) {
        const entry = ifd0Offset + 2 + i * 12
        const tag = get16(entry)
        if (tag === 0x8825) {
          gpsIfdOffset = tiffOffset + get32(entry + 8)
          break
        }
      }
      if (!gpsIfdOffset || gpsIfdOffset >= view.byteLength) return null

      const gpsEntryCount = get16(gpsIfdOffset)
      let latRef = ''
      let lonRef = ''
      let lat: [number, number, number] | null = null
      let lon: [number, number, number] | null = null

      for (let i = 0; i < gpsEntryCount; i++) {
        const entry = gpsIfdOffset + 2 + i * 12
        const tag = get16(entry)
        const type = get16(entry + 2)
        const count = get32(entry + 4)
        const valueOrOffset = get32(entry + 8)
        const valueByteLength = typeSize(type) * count
        const valuePos = valueByteLength <= 4 ? entry + 8 : tiffOffset + valueOrOffset
        if (valuePos < 0 || valuePos >= view.byteLength) continue

        if (tag === 0x0001 && type === 2) {
          latRef = String.fromCharCode(view.getUint8(valuePos)).trim()
        }
        if (tag === 0x0003 && type === 2) {
          lonRef = String.fromCharCode(view.getUint8(valuePos)).trim()
        }
        if (tag === 0x0002 && type === 5 && count === 3 && valuePos + 24 <= view.byteLength) {
          lat = [0, 1, 2].map((idx) => {
            const numerator = get32(valuePos + idx * 8)
            const denominator = get32(valuePos + idx * 8 + 4)
            return denominator ? numerator / denominator : 0
          }) as [number, number, number]
        }
        if (tag === 0x0004 && type === 5 && count === 3 && valuePos + 24 <= view.byteLength) {
          lon = [0, 1, 2].map((idx) => {
            const numerator = get32(valuePos + idx * 8)
            const denominator = get32(valuePos + idx * 8 + 4)
            return denominator ? numerator / denominator : 0
          }) as [number, number, number]
        }
      }

      if (!lat || !lon || !latRef || !lonRef) return null

      const dmsToDecimal = (parts: [number, number, number], ref: string) => {
        const value = parts[0] + parts[1] / 60 + parts[2] / 3600
        return (ref === 'S' || ref === 'W') ? -value : value
      }

      return {
        latitude: dmsToDecimal(lat, latRef),
        longitude: dmsToDecimal(lon, lonRef),
      }
    }

    offset += 2 + length
  }

  return null
}

const applyPlantSelection = (result: PlantNetResult, options: { updateText?: boolean } = {}) => {
  const { updateText = true } = options
  const species = result.species
  const commonNames = normalizeCommonNames(species.commonNamesByLocale)
  if (!Object.values(commonNames).some(Boolean)) {
    commonNames[locale.value] = (species.commonNames?.[0] ?? '').trim()
    if (!commonNames.en)
      commonNames.en = (species.commonNames?.[0] ?? '').trim()
  }

  if (updateText) {
    const commonName = localizedCommonName(commonNames, species.scientificNameWithoutAuthor)
    const userName = user.value?.name ?? 'unknown'
    title.value = `${formatDateTime()}-${commonName}-${userName}`

    const descParts = [
      species.scientificName,
      `Genus: ${species.genus.scientificNameWithoutAuthor}`,
      `Family: ${species.family.scientificNameWithoutAuthor}`,
    ]
    if (result.score !== undefined) {
      descParts.push(`Confidence: ${Math.round(result.score * 100)}%`)
    }
    description.value = descParts.join(' | ')
  }

  const orderedTags = [
    'plant',
    isWildlife.value ? 'wildlife' : 'cultivated',
    species.family.scientificNameWithoutAuthor,
    species.genus.scientificNameWithoutAuthor,
    species.scientificName,
  ]
  const nextTags = new Set<string>()
  for (const tag of orderedTags) {
    const value = tag?.trim()
    if (value) nextTags.add(value)
  }
  tagsInput.value = Array.from(nextTags).join(', ')
  identifiedSpeciesMetadata.value = {
    speciesType: 'plant',
    scientificName: species.scientificName,
    taxonomyOrder: '',
    taxonomyFamily: species.family.scientificNameWithoutAuthor,
    taxonomyGenus: species.genus.scientificNameWithoutAuthor,
    commonNames,
  }

  selectedPlantResult.value = result
  identificationDone.value = true
  lastIdentificationType.value = 'plant'
}

const selectLowConfidencePlantMatch = (result: PlantNetResult) => {
  applyPlantSelection(result)
  showPlantChooser.value = false
  identifyLowConfidence.value = null
}

const getPlantCandidateImage = (result: PlantNetResult): string | null => {
  const media = [...(result.images ?? []), ...(result.similarImages ?? [])]
  for (const item of media) {
    if (!item) continue

    if (typeof item.image === 'string' && item.image.trim()) {
      return item.image
    }

    const urlValue = item.url
    if (typeof urlValue === 'string' && urlValue.trim()) {
      return urlValue
    }

    if (urlValue && typeof urlValue === 'object') {
      const best = urlValue.m ?? urlValue.o ?? urlValue.s
      if (typeof best === 'string' && best.trim()) {
        return best
      }
    }
  }
  return null
}

const identifyPlant = async () => {
  if (!file.value) return

  isIdentifying.value = true
  identifyError.value = null
  identifyLowConfidence.value = null
  plantMatches.value = []
  showPlantChooser.value = false
  selectedPlantResult.value = null
  isWildlife.value = true

  try {
    const response = await api.identifyPlant([file.value])

    const topResult = response.results?.[0]
    if (topResult) {
      const score = topResult.score ?? 0
      if (score < MIN_PLANT_CONFIDENCE) {
        identifyLowConfidence.value = t('upload.identification.lowConfidencePlant', {
          score: Math.round(score * 100),
          threshold: Math.round(MIN_PLANT_CONFIDENCE * 100),
        })
        plantMatches.value = (response.results ?? []).slice(0, 5)
        showPlantChooser.value = plantMatches.value.length > 0
        identificationDone.value = false
        lastIdentificationType.value = null
        return
      }

      applyPlantSelection(topResult)
    } else {
      identifyError.value = t('upload.identification.noPlant')
    }
  } catch (err: unknown) {
    const message = err instanceof Error
      ? err.message
      : (err as { message?: string })?.message ?? 'Identification failed'
    identifyError.value = message
  } finally {
    isIdentifying.value = false
  }
}

const identifyBird = async () => {
  if (!file.value) return

  isIdentifying.value = true
  identifyError.value = null
  identifyLowConfidence.value = null
  plantMatches.value = []
  showPlantChooser.value = false
  selectedPlantResult.value = null

  try {
    const response = await api.identifyBird(file.value)
    const topResult = response.topResult ?? response.results?.[0] ?? null

    if (!topResult) {
      identifyError.value = 'No bird identified in this image.'
      return
    }

    const score = topResult.probability
    const minScore = response.minProbability ?? 0.5
    if (!response.accepted || score < minScore) {
      identifyLowConfidence.value = `Bird result confidence (${Math.round(score * 100)}%) is below threshold (${Math.round(minScore * 100)}%).`
      identificationDone.value = false
      lastIdentificationType.value = null
      return
    }

    const userName = user.value?.name ?? 'unknown'
    const commonName = localizedCommonName(topResult.commonNames, topResult.scientificName)
    title.value = `${formatDateTime()}-${commonName}-${userName}`
    description.value = `${topResult.scientificName} | Confidence: ${Math.round(score * 100)}%`

    const orderedTags = [
      'bird',
      topResult.scientificName,
    ]
    const nextTags = new Set<string>()
    for (const t of orderedTags) {
      const v = t?.trim()
      if (v)
        nextTags.add(v)
    }
    tagsInput.value = Array.from(nextTags).join(', ')
    identifiedSpeciesMetadata.value = {
      speciesType: 'bird',
      scientificName: topResult.scientificName,
      taxonomyOrder: topResult.taxonomyOrder ?? '',
      taxonomyFamily: topResult.taxonomyFamily ?? '',
      taxonomyGenus: topResult.taxonomyGenus ?? '',
      commonNames: normalizeCommonNames(topResult.commonNames),
    }

    identificationDone.value = true
    lastIdentificationType.value = 'bird'
  } catch (err: unknown) {
    const message = err instanceof Error
      ? err.message
      : (err as { message?: string })?.message ?? 'Bird identification failed'
    identifyError.value = message
  } finally {
    isIdentifying.value = false
  }
}

const mergeUniqueTags = (current: string, nextOrderedTags: string[]) => {
  const existing = current
    .split(',')
    .map((value) => value.trim())
    .filter(Boolean)

  const merged = new Set<string>(existing)
  for (const value of nextOrderedTags) {
    const normalized = value.trim()
    if (normalized) {
      merged.add(normalized)
    }
  }

  return Array.from(merged).join(', ')
}

const applyInsectIdentification = (result: InsectIdentificationResponse) => {
  const userName = user.value?.name ?? 'unknown'
  const displayName = localizedCommonName(result.commonNames, result.scientificName || 'insect')
  title.value = `${formatDateTime()}-${displayName}-${userName}`

  description.value = `${result.scientificName} | Order: ${result.taxonomyOrder} | Family: ${result.taxonomyFamily} | Genus: ${result.taxonomyGenus} | Confidence: ${Math.round((result.confidence ?? 0) * 100)}%`

  const tagCandidates = [
    'insect',
    result.taxonomyFamily,
    result.taxonomyGenus,
    result.scientificName,
  ]

  tagsInput.value = mergeUniqueTags(tagsInput.value, tagCandidates)
  identifiedSpeciesMetadata.value = {
    speciesType: 'insect',
    scientificName: result.scientificName,
    taxonomyOrder: result.taxonomyOrder,
    taxonomyFamily: result.taxonomyFamily,
    taxonomyGenus: result.taxonomyGenus,
    commonNames: normalizeCommonNames(result.commonNames),
  }
  identificationDone.value = true
  lastIdentificationType.value = 'insect'
}

const identifyInsect = async () => {
  if (!file.value) return

  isIdentifying.value = true
  identifyError.value = null
  identifyLowConfidence.value = null
  plantMatches.value = []
  showPlantChooser.value = false
  selectedPlantResult.value = null

  try {
    const response = await api.identifyInsect(file.value, locale.value)
    if (!response.accepted) {
      identifyLowConfidence.value = t('upload.identification.lowConfidenceInsect', {
        score: Math.round((response.confidence ?? 0) * 100),
      })
      identificationDone.value = false
      lastIdentificationType.value = null
      return
    }

    applyInsectIdentification(response)
  } catch (err: unknown) {
    const message = err instanceof Error
      ? err.message
      : (err as { message?: string })?.message ?? 'Insect identification failed'
    identifyError.value = message
  } finally {
    isIdentifying.value = false
  }
}

const onFileChange = async (e: Event) => {
  const input = e.target as HTMLInputElement
  const selected = input.files?.[0]
  if (!selected) return

  validationError.value = null
  identificationDone.value = false
  identifyError.value = null
  identifyLowConfidence.value = null
  lastIdentificationType.value = null
  plantMatches.value = []
  showPlantChooser.value = false
  selectedPlantResult.value = null
  isWildlife.value = true
  coordinates.value = null
  coordinateSource.value = null
  identifiedSpeciesMetadata.value = {}

  if (!ALLOWED_TYPES.includes(selected.type)) {
    validationError.value = `Invalid file type. Allowed: JPEG, PNG, WebP, GIF.`
    return
  }

  if (selected.size > MAX_SIZE_BYTES) {
    validationError.value = `File too large. Maximum size is ${MAX_SIZE_MB} MB.`
    return
  }

  file.value = selected
  title.value = selected.name.replace(/\.[^/.]+$/, '')

  if (preview.value) URL.revokeObjectURL(preview.value)
  preview.value = URL.createObjectURL(selected)

  const deviceLocation = await getDeviceCoordinates()
  if (deviceLocation) {
    coordinates.value = deviceLocation
    coordinateSource.value = 'device'
    return
  }

  const exifLocation = await getExifCoordinates(selected)
  if (exifLocation) {
    coordinates.value = exifLocation
    coordinateSource.value = 'exif'
  }
}

const onDrop = (e: DragEvent) => {
  e.preventDefault()
  const dropped = e.dataTransfer?.files?.[0]
  if (!dropped) return

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
  identificationDone.value = false
  identifyError.value = null
  identifyLowConfidence.value = null
  lastIdentificationType.value = null
  plantMatches.value = []
  showPlantChooser.value = false
  selectedPlantResult.value = null
  isWildlife.value = true
  coordinates.value = null
  coordinateSource.value = null
  identifiedSpeciesMetadata.value = {}
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
      speciesType: identifiedSpeciesMetadata.value.speciesType,
      scientificName: identifiedSpeciesMetadata.value.scientificName,
      taxonomyOrder: identifiedSpeciesMetadata.value.taxonomyOrder,
      taxonomyFamily: identifiedSpeciesMetadata.value.taxonomyFamily,
      taxonomyGenus: identifiedSpeciesMetadata.value.taxonomyGenus,
      commonNames: identifiedSpeciesMetadata.value.commonNames,
      latitude: coordinates.value?.latitude,
      longitude: coordinates.value?.longitude,
    })

    uploadSuccess.value = true

    setTimeout(() => {
      removeFile()
      title.value = ''
      description.value = ''
      tagsInput.value = ''
      uploadSuccess.value = false
      reset()
      identifiedSpeciesMetadata.value = {}
      emit('uploaded')
    }, 2000)
  } catch {
    // Error is already captured in state.error
  }
}

onUnmounted(() => {
  if (preview.value) URL.revokeObjectURL(preview.value)
})

watch(isWildlife, () => {
  if (
    selectedPlantResult.value
    && identificationDone.value
    && lastIdentificationType.value === 'plant'
  ) {
    applyPlantSelection(selectedPlantResult.value, { updateText: false })
  }
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
            class="ui-focus-ring ui-transition-color absolute right-2 top-2 rounded-full bg-black/50 p-1 text-white hover:bg-black/70"
            @click="removeFile"
          >
            <XMarkIcon class="h-4 w-4" />
          </button>
        </div>
        <p class="mt-2 text-center text-xs text-stone-500">
          {{ file.name }} &mdash; {{ (file.size / (1024 * 1024)).toFixed(1) }} MB
        </p>
        <p v-if="coordinates" class="mt-1 text-center text-xs text-stone-500">
          Location ({{ coordinateSource === 'device' ? 'device' : 'exif' }}): {{ formatCoordinates(coordinates.latitude, coordinates.longitude) }}
        </p>
      </div>

      <!-- Placeholder -->
      <div v-else class="flex flex-col items-center justify-center px-6 py-16 text-center">
        <PhotoIcon class="mb-3 h-12 w-12 text-stone-300" />
        <p class="text-sm font-medium text-stone-600">
          Drag & drop an image here, or
          <button type="button" class="ui-focus-ring ui-transition-color rounded-sm text-emerald-700 underline hover:text-emerald-800" @click="openFilePicker">
            browse
          </button>
        </p>
        <p class="mt-1 text-xs text-stone-400">
          JPEG, PNG, WebP or GIF &mdash; up to {{ MAX_SIZE_MB }} MB
        </p>
      </div>
    </div>

    <!-- Validation error -->
    <p v-if="validationError" class="alert-error">
      {{ validationError }}
    </p>

    <!-- Identification actions (shown when a file is selected) -->
    <div v-if="file && !state.isUploading && !uploadSuccess" class="space-y-3">
      <button
        type="button"
        class="ui-focus-ring ui-transition-color flex w-full items-center justify-center gap-2 rounded-lg border-2 px-4 py-3 text-sm font-semibold"
        :class="identificationDone && lastIdentificationType === 'plant'
          ? 'border-emerald-500 bg-emerald-50 text-emerald-700'
          : 'border-stone-200 bg-white text-stone-600 hover:border-emerald-300 hover:bg-emerald-50/50'"
        :disabled="isIdentifying"
        @click="identifyPlant"
      >
        <template v-if="isIdentifying">
          <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
          </svg>
          Identifying plant...
        </template>
        <template v-else>
          <MagnifyingGlassIcon class="h-5 w-5" />
          Identify plant
        </template>
      </button>

      <button
        type="button"
        class="ui-focus-ring ui-transition-color flex w-full items-center justify-center gap-2 rounded-lg border-2 px-4 py-3 text-sm font-semibold"
        :class="identificationDone && lastIdentificationType === 'bird'
          ? 'border-sky-500 bg-sky-50 text-sky-700'
          : 'border-stone-200 bg-white text-stone-600 hover:border-sky-300 hover:bg-sky-50/50'"
        :disabled="isIdentifying"
        @click="identifyBird"
      >
        <template v-if="isIdentifying">
          <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
          </svg>
          {{ t('upload.identification.identifyingBird') }}
        </template>
        <template v-else>
          <SparklesIcon class="h-5 w-5" />
          {{ t('upload.identification.identifyBird') }}
        </template>
      </button>

      <button
        type="button"
        class="ui-focus-ring ui-transition-color flex w-full items-center justify-center gap-2 rounded-lg border-2 px-4 py-3 text-sm font-semibold"
        :class="identificationDone && lastIdentificationType === 'insect'
          ? 'border-violet-500 bg-violet-50 text-violet-700'
          : 'border-stone-200 bg-white text-stone-600 hover:border-violet-300 hover:bg-violet-50/50'"
        :disabled="isIdentifying"
        @click="identifyInsect"
      >
        <template v-if="isIdentifying">
          <svg class="h-4 w-4 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
          </svg>
          {{ t('upload.identification.identifyingInsect') }}
        </template>
        <template v-else>
          <SparklesIcon class="h-5 w-5" />
          {{ t('upload.identification.identifyInsect') }}
        </template>
      </button>

      <!-- Identification result -->
      <div v-if="identificationDone" class="alert-success">
        {{ lastIdentificationType === 'bird' ? t('upload.identification.resultBird') : (lastIdentificationType === 'insect' ? t('upload.identification.resultInsect') : t('upload.identification.resultPlant')) }} {{ t('upload.identification.resultFilled') }}
      </div>

      <!-- Low-confidence message -->
      <p v-if="identifyLowConfidence" class="alert-warn">
        {{ identifyLowConfidence }}
      </p>

      <div v-if="showPlantChooser && plantMatches.length" class="space-y-2 rounded-lg border border-amber-200 bg-amber-50 p-3">
        <p class="text-sm font-medium text-amber-800">
          {{ t('upload.identification.topMatches') }}
        </p>
        <ul class="space-y-2">
          <li
            v-for="match in plantMatches"
            :key="`${match.species.scientificName}-${match.score}`"
            class="flex items-center justify-between gap-3 rounded-md border border-amber-200 bg-white px-3 py-2"
          >
            <div class="flex min-w-0 items-center gap-3">
              <div class="h-14 w-14 shrink-0 overflow-hidden rounded-md border border-stone-200 bg-stone-100">
                <img
                  v-if="getPlantCandidateImage(match)"
                  :src="getPlantCandidateImage(match) || ''"
                  :alt="match.species.scientificNameWithoutAuthor"
                  class="h-full w-full object-cover"
                >
                <div v-else class="flex h-full w-full items-center justify-center text-[10px] text-stone-500">
                  {{ t('upload.identification.noReferenceImage') }}
                </div>
              </div>

              <div class="min-w-0">
              <p class="truncate text-sm font-medium text-stone-800">
                {{ match.species.scientificNameWithoutAuthor }}
              </p>
              <p class="text-xs text-stone-500">
                {{ t('upload.identification.confidence', { score: Math.round((match.score ?? 0) * 100) }) }}
              </p>
            </div>
            </div>
            <button
              type="button"
              class="btn-secondary shrink-0 rounded-md px-2 py-1 text-xs"
              @click="selectLowConfidencePlantMatch(match)"
            >
              {{ t('upload.identification.useMatch') }}
            </button>
          </li>
        </ul>
      </div>

      <!-- Identification error -->
      <p v-if="identifyError" class="alert-warn">
        {{ identifyError }}
      </p>
    </div>

    <!-- Metadata fields (shown when a file is selected) -->
    <template v-if="file">
      <div>
        <label for="title" class="block text-sm font-medium text-stone-700">Title</label>
        <input
          id="title"
          v-model="title"
          type="text"
          class="ui-input"
          placeholder="Give your photo a title"
        >
      </div>

      <div>
        <label for="description" class="block text-sm font-medium text-stone-700">Description</label>
        <textarea
          id="description"
          v-model="description"
          rows="3"
          class="ui-input"
          placeholder="Optional description"
        />
      </div>

      <div>
        <label for="tags" class="block text-sm font-medium text-stone-700">Tags</label>
        <input
          id="tags"
          v-model="tagsInput"
          type="text"
          class="ui-input"
          placeholder="nature, garden, flowers (comma-separated)"
        >
        <div v-if="tags.length" class="mt-2 flex flex-wrap gap-1.5">
          <span
            v-for="tag in tags"
            :key="tag"
            class="chip-accent"
          >
            {{ tag }}
          </span>
        </div>
      </div>

      <div v-if="identificationDone && lastIdentificationType === 'plant' && selectedPlantResult" class="rounded-lg border border-stone-200 bg-white p-3">
        <label class="flex items-start gap-2 text-sm text-stone-700">
          <input
            v-model="isWildlife"
            type="checkbox"
            class="mt-0.5 h-4 w-4 rounded border-stone-300 text-emerald-600 focus:ring-emerald-500"
          >
          <span>
            <span class="font-medium">{{ t('upload.identification.wildlife') }}</span>
            <span class="block text-xs text-stone-500">{{ t('upload.identification.wildlifeHelp') }}</span>
          </span>
        </label>
      </div>

      <!-- Upload progress -->
      <UploadProgress
        v-if="state.isUploading"
        :progress="state.progress"
      />

      <!-- Error message -->
      <p v-if="state.error" class="alert-error">
        {{ state.error }}
      </p>

      <!-- Success message -->
      <p v-if="uploadSuccess" class="alert-success font-medium">
        Photo uploaded successfully!
      </p>

      <!-- Submit button -->
      <button
        type="submit"
        :disabled="state.isUploading || uploadSuccess"
        class="btn-primary w-full py-2.5 text-sm font-semibold"
      >
        <span v-if="state.isUploading">Uploading...</span>
        <span v-else-if="uploadSuccess">Done!</span>
        <span v-else>Upload Photo</span>
      </button>
    </template>
  </form>
</template>
