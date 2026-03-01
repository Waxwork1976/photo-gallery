<script setup lang="ts">
import type { FolderTreeNodeDto } from '~/types/image'
import { useTranslations } from '~/composables/useTranslations'

defineProps<{
  tree: FolderTreeNodeDto[]
  rootLabels: Record<string, string>
  selectedPath: string | null
}>()

const emit = defineEmits<{
  select: [path: string]
  clear: []
}>()

const { t } = useTranslations()
</script>

<template>
  <aside class="overflow-x-hidden rounded-xl border border-stone-200 bg-white p-4 shadow-sm">
    <div class="mb-2 flex items-center justify-between">
      <h2 class="text-sm font-semibold text-stone-900">{{ t('folders.title') }}</h2>
      <button
        type="button"
        class="text-xs text-stone-500 underline hover:text-stone-700"
        @click="emit('clear')"
      >
        {{ t('folders.clear') }}
      </button>
    </div>
    <ul class="space-y-1">
      <GalleryFolderTreeNode
        v-for="node in tree"
        :key="node.path"
        :node="node"
        :root-labels="rootLabels"
        :selected-path="selectedPath"
        @select="emit('select', $event)"
      />
    </ul>
  </aside>
</template>
