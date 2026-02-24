<script setup lang="ts">
import type { ImageDto } from '~/types/image'

const props = defineProps<{
  image: ImageDto | null
}>()

const emit = defineEmits<{
  close: []
}>()

/** Close on Escape key. */
const onKeyDown = (e: KeyboardEvent) => {
  if (e.key === 'Escape') emit('close')
}

onMounted(() => {
  window.addEventListener('keydown', onKeyDown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeyDown)
})

const imageLoaded = ref(false)

watch(
  () => props.image,
  () => {
    imageLoaded.value = false
  },
)
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="image"
        class="lightbox-backdrop"
        @click.self="emit('close')"
      >
        <!-- Close button -->
        <button
          class="absolute right-4 top-4 rounded-full bg-white/10 p-2 text-white backdrop-blur-sm hover:bg-white/20 transition-colors"
          @click="emit('close')"
        >
          <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>

        <!-- Content -->
        <div class="flex max-h-[90vh] max-w-[90vw] flex-col items-center gap-4">
          <!-- Spinner -->
          <div v-if="!imageLoaded" class="flex h-64 items-center justify-center">
            <div class="h-10 w-10 animate-spin rounded-full border-4 border-white/30 border-t-white" />
          </div>

          <img
            :src="image.fullUrl"
            :alt="image.title || 'Photo'"
            class="max-h-[80vh] rounded-lg object-contain shadow-2xl transition-opacity duration-300"
            :class="imageLoaded ? 'opacity-100' : 'opacity-0 absolute'"
            @load="imageLoaded = true"
          >

          <!-- Caption -->
          <div v-if="imageLoaded" class="text-center">
            <h2 class="text-lg font-semibold text-white">
              {{ image.title || 'Untitled' }}
            </h2>
            <p v-if="image.description" class="mt-1 max-w-xl text-sm text-white/70">
              {{ image.description }}
            </p>
            <div v-if="image.tags.length" class="mt-2 flex flex-wrap justify-center gap-1.5">
              <span
                v-for="tag in image.tags"
                :key="tag"
                class="rounded-full bg-white/15 px-2.5 py-0.5 text-xs font-medium text-white/80"
              >
                {{ tag }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
