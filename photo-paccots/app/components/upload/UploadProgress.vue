<script setup lang="ts">
defineProps<{
  progress: number
}>()

const steps = [
  { min: 0, label: 'Requesting upload URL...' },
  { min: 15, label: 'Uploading image...' },
  { min: 82, label: 'Saving metadata...' },
  { min: 98, label: 'Done!' },
]

const currentStep = (progress: number) => {
  for (let i = steps.length - 1; i >= 0; i--) {
    if (progress >= steps[i].min) return steps[i].label
  }
  return steps[0].label
}
</script>

<template>
  <div class="space-y-2">
    <!-- Label -->
    <div class="flex items-center justify-between text-sm">
      <span class="font-medium text-stone-600">{{ currentStep(progress) }}</span>
      <span class="tabular-nums text-stone-400">{{ Math.round(progress) }}%</span>
    </div>

    <!-- Progress bar -->
    <div class="h-2 overflow-hidden rounded-full bg-stone-200">
      <div
        class="h-full rounded-full bg-emerald-500 transition-all duration-300 ease-out"
        :style="{ width: `${progress}%` }"
      />
    </div>
  </div>
</template>
