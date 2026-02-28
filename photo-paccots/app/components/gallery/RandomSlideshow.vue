<script setup lang="ts">
import type { ImageDto } from '~/types/image'

const props = withDefaults(defineProps<{
  images: ImageDto[]
  intervalSeconds: number
  maxPhotos: number
}>(), {
  intervalSeconds: 4,
  maxPhotos: 8,
})

const activeIndex = ref(0)
const randomPool = ref<ImageDto[]>([])
const timerId = ref<ReturnType<typeof setInterval> | null>(null)

const shuffled = (input: ImageDto[]) => {
  const arr = [...input]
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    const tmp = arr[i]
    arr[i] = arr[j]
    arr[j] = tmp
  }
  return arr
}

const setupPool = () => {
  const max = Math.max(1, props.maxPhotos)
  randomPool.value = shuffled(props.images).slice(0, max)
  activeIndex.value = 0
}

const stopTimer = () => {
  if (timerId.value) {
    clearInterval(timerId.value)
    timerId.value = null
  }
}

const startTimer = () => {
  stopTimer()
  if (randomPool.value.length <= 1)
    return
  const intervalMs = Math.max(1, props.intervalSeconds) * 1000
  timerId.value = setInterval(() => {
    activeIndex.value = (activeIndex.value + 1) % randomPool.value.length
  }, intervalMs)
}

const currentImage = computed(() => randomPool.value[activeIndex.value] ?? null)

watch(
  () => [props.images, props.maxPhotos] as const,
  () => {
    setupPool()
    startTimer()
  },
  { immediate: true },
)

watch(
  () => props.intervalSeconds,
  () => {
    startTimer()
  },
)

onUnmounted(() => {
  stopTimer()
})
</script>

<template>
  <section v-if="currentImage" class="overflow-hidden rounded-xl border border-stone-200 bg-white shadow-sm">
    <div class="relative aspect-[16/7] max-h-[380px] min-h-[180px] w-full bg-stone-100">
      <img
        :src="currentImage.thumbnailUrl"
        :alt="currentImage.title || 'Photo'"
        class="h-full w-full object-cover"
      >
      <div class="absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/55 via-black/20 to-transparent p-4 text-white">
        <h2 class="text-lg font-semibold">
          {{ currentImage.title || 'Untitled' }}
        </h2>
        <p v-if="currentImage.description" class="line-clamp-2 text-sm text-white/85">
          {{ currentImage.description }}
        </p>
      </div>
    </div>
  </section>
</template>
