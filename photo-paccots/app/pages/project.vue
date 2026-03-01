<script setup lang="ts">
import { nextTick } from 'vue'
import 'leaflet/dist/leaflet.css'
import { useTranslations } from '~/composables/useTranslations'

useHead({
  title: 'The project — Photo Paccots',
})

const api = useApi()
const { t } = useTranslations()

type LocationPoint = {
  id: string
  title: string
  latitude: number
  longitude: number
}

const loading = ref(true)
const error = ref<string | null>(null)
const showProjectMap = ref(false)
const points = ref<LocationPoint[]>([])

const mapElement = ref<HTMLElement | null>(null)
const shouldShowMap = computed(() => showProjectMap.value && points.value.length > 0)

let leafletModule: any = null
let mapInstance: any = null
let markerLayer: any = null

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
    const [settings, images] = await Promise.all([
      api.getSlideshowSettings(),
      api.listImages(),
    ])

    showProjectMap.value = settings.showProjectMap
    points.value = images.images
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
  await load()
  if (shouldShowMap.value) {
    await nextTick()
    await ensureMap()
  }
})

onUnmounted(() => {
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
