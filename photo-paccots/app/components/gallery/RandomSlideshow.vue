<script setup lang="ts">
import type { ImageDto } from '~/types/image'

const props = withDefaults(defineProps<{
  images: ImageDto[]
  intervalSeconds: number
  maxPhotos: number
  transitionSeconds: number
}>(), {
  intervalSeconds: 4,
  maxPhotos: 8,
  transitionSeconds: 0.8,
})

const activeIndex = ref(0)
const slideshowPool = ref<ImageDto[]>([])
const timerId = ref<ReturnType<typeof setInterval> | null>(null)

const setupPool = () => {
  const max = Math.max(1, props.maxPhotos)
  slideshowPool.value = props.images.slice(0, max)
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
  if (slideshowPool.value.length <= 1)
    return
  const intervalMs = Math.max(1, props.intervalSeconds) * 1000
  timerId.value = setInterval(() => {
    activeIndex.value = (activeIndex.value + 1) % slideshowPool.value.length
  }, intervalMs)
}

const currentImage = computed(() => slideshowPool.value[activeIndex.value] ?? null)
const fadeDurationMs = computed(() => Math.max(0, props.transitionSeconds) * 1000)

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
  <section v-if="currentImage" class="min-w-0 overflow-hidden rounded-xl border border-stone-200 bg-white shadow-sm">
    <div class="relative aspect-[16/7] max-h-[380px] min-h-[180px] w-full min-w-0 bg-stone-100">
      <Transition name="fade-slide" mode="out-in" :duration="fadeDurationMs">
        <img
          :key="currentImage.id"
          :src="currentImage.thumbnailUrl"
          :alt="currentImage.title || 'Photo'"
          class="h-full w-full object-cover"
          :style="{ transitionDuration: `${fadeDurationMs}ms` }"
        >
      </Transition>
      <div class="absolute inset-x-0 bottom-0 min-w-0 bg-gradient-to-t from-black/55 via-black/20 to-transparent p-4 text-white">
        <h2 class="break-words text-lg font-semibold">
          {{ currentImage.title || 'Untitled' }}
        </h2>
        <p v-if="currentImage.description" class="line-clamp-2 break-words text-sm text-white/85">
          {{ currentImage.description }}
        </p>
      </div>
    </div>
  </section>
</template>

<style scoped>
.fade-slide-enter-active,
.fade-slide-leave-active {
  transition-property: opacity;
  transition-timing-function: ease-in-out;
  @apply motion-reduce:transition-none;
}

.fade-slide-enter-from,
.fade-slide-leave-to {
  opacity: 0;
}

.fade-slide-enter-to,
.fade-slide-leave-from {
  opacity: 1;
}
</style>
