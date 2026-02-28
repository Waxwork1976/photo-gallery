<script setup lang="ts">
import { useTranslations } from '~/composables/useTranslations'

definePageMeta({
  middleware: 'auth',
})

useHead({
  title: 'Upload — Photo Paccots',
})

const router = useRouter()
const { t } = useTranslations()

const onUploaded = () => {
  // Navigate back to gallery after a successful upload
  router.push('/')
}
</script>

<template>
  <div class="mx-auto max-w-2xl">
    <section class="mb-8 text-center">
      <h1 class="text-2xl font-bold tracking-tight text-stone-900 sm:text-3xl">
        {{ t('upload.page.title') }}
      </h1>
      <p class="mt-2 text-sm text-stone-500">
        {{ t('upload.page.subtitle') }}
      </p>
    </section>

    <UploadForm @uploaded="onUploaded" />
  </div>
</template>
