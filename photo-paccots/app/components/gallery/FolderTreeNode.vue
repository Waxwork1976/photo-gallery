<script setup lang="ts">
import type { FolderTreeNodeDto } from '~/types/image'

defineOptions({
  name: 'GalleryFolderTreeNode',
})

const props = defineProps<{
  node: FolderTreeNodeDto
  selectedPath: string | null
}>()

const emit = defineEmits<{
  select: [path: string]
}>()

const isSelected = computed(() => props.selectedPath === props.node.path)
const hasChildren = computed(() => (props.node.children?.length ?? 0) > 0)
const isExpanded = ref(false)
</script>

<template>
  <li>
    <div class="flex items-center gap-1">
      <button
        v-if="hasChildren"
        type="button"
        class="rounded p-0.5 text-stone-500 hover:bg-stone-100 hover:text-stone-700"
        :aria-label="isExpanded ? 'Collapse folder' : 'Expand folder'"
        @click="isExpanded = !isExpanded"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          class="h-3.5 w-3.5 transition-transform"
          :class="isExpanded ? 'rotate-90' : ''"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          stroke-width="2"
        >
          <path stroke-linecap="round" stroke-linejoin="round" d="M9 5l7 7-7 7" />
        </svg>
      </button>
      <span v-else class="inline-block w-[18px]" />

      <button
        type="button"
        class="rounded px-2 py-0.5 text-left text-sm transition-colors"
        :class="isSelected ? 'bg-emerald-100 text-emerald-800' : 'text-stone-600 hover:bg-stone-100'"
        @click="emit('select', node.path)"
      >
        {{ node.name }}
      </button>
    </div>

    <ul v-if="hasChildren && isExpanded" class="ml-4 mt-1 space-y-1 border-l border-stone-200 pl-2">
      <GalleryFolderTreeNode
        v-for="child in node.children"
        :key="child.path"
        :node="child"
        :selected-path="selectedPath"
        @select="emit('select', $event)"
      />
    </ul>
  </li>
</template>
