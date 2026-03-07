<script setup lang="ts">
import { nextTick } from 'vue'
import 'leaflet/dist/leaflet.css'
import { useTranslations } from '~/composables/useTranslations'
import type { ImageDto } from '~/types/image'

useHead({
  title: 'The project — Photo Paccots',
})

const api = useApi()
const { t, locale } = useTranslations()

type LocationPoint = {
  id: string
  title: string
  latitude: number
  longitude: number
}

const loading = ref(true)
const error = ref<string | null>(null)
const showProjectMap = ref(false)
const images = ref<ImageDto[]>([])
const points = ref<LocationPoint[]>([])
const selectedYear = ref<number | null>(null)

const mapElement = ref<HTMLElement | null>(null)
const timelineContainer = ref<HTMLElement | null>(null)
const shouldShowMap = computed(() => showProjectMap.value && points.value.length > 0)
const hasTimelineData = computed(() => availableYears.value.length > 0)
const hoveredMonth = ref<number | null>(null)
const pinnedMonth = ref<number | null>(null)
const openMonthPopup = computed<number | null>(() => pinnedMonth.value ?? hoveredMonth.value)

let leafletModule: any = null
let mapInstance: any = null
let markerLayer: any = null

type TimelineImageRecord = {
  image: ImageDto
  date: Date
  year: number
  month: number
}

type SpeciesFirstSeenEntry = {
  imageId: string
  imageTitle: string
  thumbnailUrl: string
  scientificName: string
  commonName: string
  firstSeenAt: Date
}

type TimelineMonthCell = {
  month: number
  monthLabel: string
  count: number
  intensityClass: string
  speciesEntries: SpeciesFirstSeenEntry[]
  tooltipText: string
}

const localeDateFormatter = computed(() => new Intl.DateTimeFormat(locale.value))
const monthLabelFormatter = computed(() => new Intl.DateTimeFormat(locale.value, { month: 'short' }))

const parsedTimelineRecords = computed<TimelineImageRecord[]>(() => {
  return images.value
    .map((image) => {
      const date = new Date(image.uploadedAt)
      if (Number.isNaN(date.getTime())) return null
      return {
        image,
        date,
        year: date.getFullYear(),
        month: date.getMonth() + 1,
      } satisfies TimelineImageRecord
    })
    .filter((entry): entry is TimelineImageRecord => entry !== null)
})

const availableYears = computed<number[]>(() => {
  const years = new Set<number>()
  for (const record of parsedTimelineRecords.value) {
    years.add(record.year)
  }
  return [...years].sort((a, b) => b - a)
})

watch(availableYears, (years) => {
  if (years.length === 0) {
    selectedYear.value = null
    return
  }
  if (!selectedYear.value || !years.includes(selectedYear.value)) {
    selectedYear.value = years[0] ?? null
  }
}, { immediate: true })

const selectedYearRecords = computed(() => {
  if (!selectedYear.value) return []
  return parsedTimelineRecords.value.filter(record => record.year === selectedYear.value)
})

const monthlyCounts = computed(() => {
  const counts = new Map<number, number>()
  for (let month = 1; month <= 12; month++) counts.set(month, 0)
  for (const record of selectedYearRecords.value) {
    counts.set(record.month, (counts.get(record.month) ?? 0) + 1)
  }
  return counts
})

const localizedCommonNameForImage = (image: ImageDto) => {
  const localized = (image.commonNames?.[locale.value] ?? '').trim()
  if (localized) return localized
  return (image.scientificName ?? '').trim()
}

const firstSeenByMonth = computed(() => {
  const firstSeenBySpecies = new Map<string, SpeciesFirstSeenEntry>()
  for (const record of selectedYearRecords.value) {
    const scientificName = (record.image.scientificName ?? '').trim()
    const speciesKey = scientificName.toLowerCase() || `image:${record.image.id}`
    const existing = firstSeenBySpecies.get(speciesKey)
    if (!existing || record.date < existing.firstSeenAt) {
      firstSeenBySpecies.set(speciesKey, {
        imageId: record.image.id,
        imageTitle: record.image.title || record.image.scientificName || record.image.id,
        thumbnailUrl: record.image.thumbnailUrl || '',
        scientificName: scientificName || '—',
        commonName: localizedCommonNameForImage(record.image) || scientificName || '—',
        firstSeenAt: record.date,
      })
    }
  }

  const byMonth = new Map<number, SpeciesFirstSeenEntry[]>()
  for (let month = 1; month <= 12; month++) byMonth.set(month, [])
  for (const entry of firstSeenBySpecies.values()) {
    const month = entry.firstSeenAt.getMonth() + 1
    byMonth.get(month)?.push(entry)
  }
  for (const entries of byMonth.values()) {
    entries.sort((a, b) => a.firstSeenAt.getTime() - b.firstSeenAt.getTime())
  }
  return byMonth
})

const maxMonthCount = computed(() => {
  let max = 0
  for (const count of monthlyCounts.value.values()) {
    if (count > max) max = count
  }
  return max
})

const intensityClassForCount = (count: number, max: number) => {
  if (count <= 0) return 'bg-stone-100 border-stone-200 text-stone-500'
  if (max <= 1) return 'bg-emerald-300/70 border-emerald-400 text-emerald-950'
  const ratio = count / max
  if (ratio < 0.34) return 'bg-emerald-200 border-emerald-300 text-emerald-950'
  if (ratio < 0.67) return 'bg-emerald-300 border-emerald-400 text-emerald-950'
  return 'bg-emerald-400 border-emerald-500 text-emerald-950'
}

const timelineTooltipText = (month: number, count: number, speciesEntries: SpeciesFirstSeenEntry[]) => {
  const lines = [
    `${monthLabelFormatter.value.format(new Date(2000, month - 1, 1))}: ${count} ${t('project.timeline.pictures')}`,
  ]
  if (speciesEntries.length === 0) {
    lines.push(t('project.timeline.tooltip.none'))
    return lines.join('\n')
  }
  lines.push(t('project.timeline.tooltip.speciesFirstSeen'))
  for (const entry of speciesEntries) {
    lines.push(`- ${entry.commonName} | ${entry.scientificName} | ${localeDateFormatter.value.format(entry.firstSeenAt)}`)
  }
  return lines.join('\n')
}

const timelineMonths = computed<TimelineMonthCell[]>(() => {
  const max = maxMonthCount.value
  const cells: TimelineMonthCell[] = []
  for (let month = 1; month <= 12; month++) {
    const count = monthlyCounts.value.get(month) ?? 0
    const speciesEntries = firstSeenByMonth.value.get(month) ?? []
    cells.push({
      month,
      monthLabel: monthLabelFormatter.value.format(new Date(2000, month - 1, 1)),
      count,
      intensityClass: intensityClassForCount(count, max),
      speciesEntries,
      tooltipText: timelineTooltipText(month, count, speciesEntries),
    })
  }
  return cells
})

const closeTimelinePopup = () => {
  hoveredMonth.value = null
  pinnedMonth.value = null
}

const onMonthMouseEnter = (month: number) => {
  hoveredMonth.value = month
}

const onMonthMouseLeave = (month: number) => {
  if (hoveredMonth.value === month)
    hoveredMonth.value = null
}

const onMonthFocusIn = (month: number) => {
  hoveredMonth.value = month
}

const onMonthFocusOut = (event: FocusEvent, month: number) => {
  const currentTarget = event.currentTarget as HTMLElement | null
  const nextTarget = event.relatedTarget as Node | null
  if (currentTarget && nextTarget && currentTarget.contains(nextTarget))
    return
  if (hoveredMonth.value === month)
    hoveredMonth.value = null
}

const toggleMonthPopup = (month: number) => {
  pinnedMonth.value = pinnedMonth.value === month ? null : month
  hoveredMonth.value = month
}

const handleDocumentPointerDown = (event: PointerEvent) => {
  const target = event.target as Node | null
  if (!target || !timelineContainer.value)
    return
  if (!timelineContainer.value.contains(target))
    closeTimelinePopup()
}

const handleDocumentKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape')
    closeTimelinePopup()
}

const destroyMap = () => {
  markerLayer?.remove()
  markerLayer = null
  mapInstance?.remove()
  mapInstance = null
}

const ensureMap = async () => {
  if (!import.meta.client || !shouldShowMap.value || !mapElement.value)
    return

  if (!leafletModule)
    leafletModule = await import('leaflet')

  if (!mapInstance) {
    mapInstance = leafletModule.map(mapElement.value, {
      zoomControl: true,
      minZoom: 2,
      maxZoom: 18,
    })

    leafletModule.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
    }).addTo(mapInstance)
  }

  if (markerLayer) {
    markerLayer.remove()
    markerLayer = null
  }

  markerLayer = leafletModule.layerGroup()
  for (const point of points.value) {
    leafletModule
      .circleMarker([point.latitude, point.longitude], {
        radius: 6,
        color: '#0f766e',
        weight: 2,
        fillColor: '#14b8a6',
        fillOpacity: 0.75,
      })
      .bindPopup(point.title || t('project.map.photoFallbackTitle'))
      .addTo(markerLayer)
  }
  markerLayer.addTo(mapInstance)

  const latLngs = points.value.map(p => leafletModule.latLng(p.latitude, p.longitude))
  if (latLngs.length === 1) {
    mapInstance.setView(latLngs[0], 11)
  } else {
    const bounds = leafletModule.latLngBounds(latLngs)
    mapInstance.fitBounds(bounds, { padding: [20, 20] })
  }
}

const load = async () => {
  loading.value = true
  error.value = null
  try {
    const [settings, imagesResponse] = await Promise.all([
      api.getSlideshowSettings(),
      api.listImages(),
    ])

    showProjectMap.value = settings.showProjectMap
    images.value = imagesResponse.images
    points.value = imagesResponse.images
      .filter(img => typeof img.latitude === 'number' && typeof img.longitude === 'number')
      .map(img => ({
        id: img.id,
        title: img.title || img.scientificName || t('project.map.photoFallbackTitle'),
        latitude: img.latitude as number,
        longitude: img.longitude as number,
      }))
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message ?? ''
    error.value = `${t('project.loadError')} ${detail}`.trim()
  } finally {
    loading.value = false
  }
}

watch(shouldShowMap, async (next) => {
  if (!next) {
    destroyMap()
    return
  }
  await nextTick()
  await ensureMap()
})

watch(points, async () => {
  if (!shouldShowMap.value)
    return
  await nextTick()
  await ensureMap()
})

onMounted(async () => {
  if (import.meta.client) {
    document.addEventListener('pointerdown', handleDocumentPointerDown)
    document.addEventListener('keydown', handleDocumentKeydown)
  }
  await load()
  if (shouldShowMap.value) {
    await nextTick()
    await ensureMap()
  }
})

onUnmounted(() => {
  if (import.meta.client) {
    document.removeEventListener('pointerdown', handleDocumentPointerDown)
    document.removeEventListener('keydown', handleDocumentKeydown)
  }
  destroyMap()
})
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6">
    <section>
      <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
        {{ t('project.title') }}
      </h1>
      <p class="mt-1 text-sm text-stone-500">
        {{ t('project.subtitle') }}
      </p>
    </section>

    <section class="ui-card space-y-4 p-5 sm:p-6">
      <p class="text-sm leading-7 text-stone-700">
        {{ t('project.body.p1') }}
      </p>
      <p class="text-sm leading-7 text-stone-700">
        {{ t('project.body.p2') }}
      </p>
      <p class="text-sm leading-7 text-stone-700">
        {{ t('project.body.p3') }}
      </p>
    </section>

    <p v-if="loading" class="text-sm text-stone-500">
      {{ t('project.loading') }}
    </p>
    <p v-else-if="error" class="alert-error">
      {{ error }}
    </p>

    <section v-if="!loading && !error" class="ui-card space-y-4 p-5 sm:p-6">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 class="text-lg font-semibold text-stone-900">
            {{ t('project.timeline.title') }}
          </h2>
          <p class="text-sm text-stone-600">
            {{ t('project.timeline.subtitle') }}
          </p>
        </div>

        <label v-if="hasTimelineData" class="inline-flex items-center gap-2 text-sm text-stone-700">
          <span>{{ t('project.timeline.yearLabel') }}</span>
          <select v-model="selectedYear" class="ui-input-compact w-auto min-w-[7rem]">
            <option v-for="year in availableYears" :key="year" :value="year">
              {{ year }}
            </option>
          </select>
        </label>
      </div>

      <p v-if="!hasTimelineData" class="text-sm text-stone-500">
        {{ t('project.timeline.noData') }}
      </p>

      <div v-else ref="timelineContainer" class="space-y-2">
        <div class="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
          <article
            v-for="cell in timelineMonths"
            :key="cell.month"
            class="relative rounded-md border p-2.5 outline-none"
            :class="[cell.intensityClass, 'cursor-pointer', openMonthPopup === cell.month ? 'z-[1100]' : '']"
            :title="cell.tooltipText"
            tabindex="0"
            role="button"
            :aria-expanded="openMonthPopup === cell.month"
            @mouseenter="onMonthMouseEnter(cell.month)"
            @mouseleave="onMonthMouseLeave(cell.month)"
            @focusin="onMonthFocusIn(cell.month)"
            @focusout="onMonthFocusOut($event, cell.month)"
            @click="toggleMonthPopup(cell.month)"
            @keydown.enter.prevent="toggleMonthPopup(cell.month)"
            @keydown.space.prevent="toggleMonthPopup(cell.month)"
          >
            <p class="text-xs font-semibold uppercase tracking-wide opacity-85">
              {{ cell.monthLabel }}
            </p>
            <p class="mt-1 text-lg font-semibold leading-none">
              {{ cell.count }}
            </p>
            <p class="mt-1 text-[11px] opacity-85">
              {{ t('project.timeline.pictures') }}
            </p>

            <div
              v-if="openMonthPopup === cell.month"
              class="absolute left-0 z-[1100] mt-2 w-[min(24rem,88vw)] rounded-lg border border-stone-200 bg-white p-3 text-stone-800 shadow-xl"
              @click.stop
            >
              <p class="text-xs font-semibold text-stone-900">
                {{ cell.monthLabel }}: {{ cell.count }} {{ t('project.timeline.pictures') }}
              </p>

              <p v-if="cell.speciesEntries.length === 0" class="mt-2 text-xs text-stone-600">
                {{ t('project.timeline.tooltip.none') }}
              </p>

              <template v-else>
                <p class="mt-2 text-xs font-medium text-stone-700">
                  {{ t('project.timeline.tooltip.speciesFirstSeen') }}
                </p>
                <ul class="mt-2 max-h-64 space-y-2 overflow-y-auto pr-1">
                  <li
                    v-for="entry in cell.speciesEntries"
                    :key="`${cell.month}:${entry.imageId}:${entry.scientificName}:${entry.firstSeenAt.getTime()}`"
                    class="flex items-start gap-2 rounded-md border border-stone-200 bg-stone-50 p-2"
                  >
                    <img
                      v-if="entry.thumbnailUrl"
                      :src="entry.thumbnailUrl"
                      :alt="t('project.timeline.popup.imageAlt', { name: entry.commonName || entry.scientificName })"
                      class="h-[50px] w-auto max-w-[84px] flex-shrink-0 rounded border border-stone-200 object-cover"
                    >
                    <div class="min-w-0 text-xs text-stone-700">
                      <p class="truncate font-medium text-stone-900">{{ entry.commonName }}</p>
                      <p class="truncate italic">{{ entry.scientificName }}</p>
                      <p>{{ localeDateFormatter.format(entry.firstSeenAt) }}</p>
                    </div>
                  </li>
                </ul>
              </template>
            </div>
          </article>
        </div>
        <p class="text-xs text-stone-500">
          {{ t('project.timeline.popupHint') }}
        </p>
      </div>
    </section>

    <section v-if="!loading && !error && shouldShowMap" class="ui-card space-y-3 p-5 sm:p-6">
      <h2 class="text-lg font-semibold text-stone-900">
        {{ t('project.map.title') }}
      </h2>
      <ClientOnly>
        <div ref="mapElement" class="h-[420px] w-full rounded-md border border-stone-200" />
      </ClientOnly>
    </section>
  </div>
</template>
