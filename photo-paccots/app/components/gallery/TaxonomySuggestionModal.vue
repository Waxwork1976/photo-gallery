<script setup lang="ts">
import type { ImageDto } from '~/types/image'
import type { CreateTaxonomySuggestionRequest } from '~/types/taxonomySuggestion'
import { useTranslations } from '~/composables/useTranslations'

declare global {
  interface Window {
    turnstile?: {
      render: (container: HTMLElement, options: Record<string, unknown>) => string
      remove: (widgetId: string) => void
    }
  }
}

const props = defineProps<{
  image: ImageDto | null
}>()

const emit = defineEmits<{
  close: []
  draftSaved: [payload: { imageId: string, savedAt: string }]
}>()

const { t, locale } = useTranslations()
const { user, isAuthenticated } = useAuth()
const config = useRuntimeConfig()
const api = useApi()

const requesterEmail = ref('')
const suggestedOrder = ref('')
const suggestedFamily = ref('')
const suggestedGenus = ref('')
const suggestedScientificName = ref('')
const suggestedCommonName = ref('')
const note = ref('')
const formError = ref<string | null>(null)
const submitting = ref(false)
const captchaToken = ref('')
const captchaContainer = ref<HTMLElement | null>(null)
const widgetId = ref<string | null>(null)
const turnstileScriptLoading = ref(false)

const visible = computed(() => Boolean(props.image))
const turnstileSiteKey = computed(() => String(config.public.turnstileSiteKey || '').trim())
const isCaptchaConfigured = computed(() => turnstileSiteKey.value.length > 0)

const localizedCurrentCommonName = computed(() => {
  const image = props.image
  if (!image) return ''
  const commonNames = image.commonNames ?? {}
  return commonNames[locale.value] || commonNames.en || ''
})

const resetForm = () => {
  requesterEmail.value = (user.value?.email || '').trim()
  suggestedOrder.value = props.image?.taxonomyOrder ?? ''
  suggestedFamily.value = props.image?.taxonomyFamily ?? ''
  suggestedGenus.value = props.image?.taxonomyGenus ?? ''
  suggestedScientificName.value = props.image?.scientificName ?? ''
  suggestedCommonName.value = localizedCurrentCommonName.value
  note.value = ''
  formError.value = null
  captchaToken.value = ''
}

const removeTurnstileWidget = () => {
  if (!import.meta.client) return
  if (widgetId.value && window.turnstile) {
    window.turnstile.remove(widgetId.value)
  }
  widgetId.value = null
}

const loadTurnstileScript = async () => {
  if (!import.meta.client || window.turnstile || turnstileScriptLoading.value) return
  turnstileScriptLoading.value = true
  try {
    await new Promise<void>((resolve, reject) => {
      const existing = document.querySelector('script[data-turnstile="true"]') as HTMLScriptElement | null
      if (existing) {
        existing.addEventListener('load', () => resolve(), { once: true })
        existing.addEventListener('error', () => reject(new Error('failed')), { once: true })
        return
      }
      const script = document.createElement('script')
      script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit'
      script.async = true
      script.defer = true
      script.dataset.turnstile = 'true'
      script.onload = () => resolve()
      script.onerror = () => reject(new Error('failed'))
      document.head.appendChild(script)
    })
  } finally {
    turnstileScriptLoading.value = false
  }
}

const renderTurnstile = async () => {
  if (!import.meta.client || !visible.value || isAuthenticated.value) return
  if (!turnstileSiteKey.value || !captchaContainer.value) return
  if (widgetId.value) return

  await loadTurnstileScript()
  if (!window.turnstile || !captchaContainer.value) return

  widgetId.value = window.turnstile.render(captchaContainer.value, {
    sitekey: turnstileSiteKey.value,
    theme: 'auto',
    callback: (token: string) => {
      captchaToken.value = token
    },
    'error-callback': () => {
      captchaToken.value = ''
    },
    'expired-callback': () => {
      captchaToken.value = ''
    },
  })
}

const saveDraft = async () => {
  const image = props.image
  if (!image) return

  formError.value = null
  const authenticated = isAuthenticated.value
  const email = requesterEmail.value.trim()

  if (!email) {
    formError.value = t('taxonomySuggest.requiredEmail')
    return
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    formError.value = t('taxonomySuggest.invalidEmail')
    return
  }

  if (!authenticated) {
    if (isCaptchaConfigured.value && !captchaToken.value) {
      formError.value = t('taxonomySuggest.captchaMissing')
      return
    }
  }

  submitting.value = true
  try {
    const payload: CreateTaxonomySuggestionRequest = {
    imageId: image.id,
    imageTitle: image.title || localizedCurrentCommonName.value || image.scientificName || '',
    sourceTaxonomy: {
      order: image.taxonomyOrder || '',
      family: image.taxonomyFamily || '',
      genus: image.taxonomyGenus || '',
      scientificName: image.scientificName || '',
      commonName: localizedCurrentCommonName.value,
    },
    suggestedTaxonomy: {
      order: suggestedOrder.value.trim(),
      family: suggestedFamily.value.trim(),
      genus: suggestedGenus.value.trim(),
      scientificName: suggestedScientificName.value.trim(),
      commonName: suggestedCommonName.value.trim(),
    },
    note: note.value.trim(),
    requester: {
      authenticated,
      name: user.value?.name || '',
      email,
      locale: locale.value,
    },
    captchaToken: authenticated ? undefined : captchaToken.value,
  }

    const created = await api.createTaxonomySuggestion(payload)
    emit('draftSaved', { imageId: image.id, savedAt: created.createdAt })
    emit('close')
  } catch (err: unknown) {
    const detail = (err as { message?: string })?.message?.trim()
    formError.value = detail || t('gallery.loadError')
  } finally {
    submitting.value = false
  }
}

watch(
  () => props.image,
  async () => {
    removeTurnstileWidget()
    resetForm()
    await nextTick()
    await renderTurnstile()
  },
)

watch(
  () => [isAuthenticated.value, user.value?.email],
  () => {
    if (!requesterEmail.value.trim()) {
      requesterEmail.value = (user.value?.email || '').trim()
    }
  },
)

onUnmounted(() => {
  removeTurnstileWidget()
})
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
        v-if="visible && image"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/45 p-4"
        @click.self="emit('close')"
      >
        <section class="w-full max-w-2xl rounded-xl bg-white p-5 shadow-xl sm:p-6">
          <div class="mb-4 flex items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold text-stone-900">
                {{ t('taxonomySuggest.title') }}
              </h2>
              <p class="text-sm text-stone-500">
                {{ image.title || localizedCurrentCommonName || image.scientificName || image.id }}
              </p>
            </div>
            <button class="btn-secondary px-3 py-1.5 text-xs" @click="emit('close')">
              {{ t('taxonomySuggest.cancel') }}
            </button>
          </div>

          <div class="mb-4 rounded-md border border-stone-200 bg-stone-50 p-3 text-xs text-stone-700">
            <p class="font-medium text-stone-800">
              {{ t('taxonomySuggest.currentTaxonomy') }}
            </p>
            <p class="mt-1">Order: {{ image.taxonomyOrder || '—' }}</p>
            <p>Family: {{ image.taxonomyFamily || '—' }}</p>
            <p>Genus: {{ image.taxonomyGenus || '—' }}</p>
            <p>Scientific: {{ image.scientificName || '—' }}</p>
            <p>Common: {{ localizedCurrentCommonName || '—' }}</p>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <label class="block">
              <span class="block text-xs font-medium text-stone-600">{{ t('taxonomySuggest.suggestedOrder') }}</span>
              <input v-model="suggestedOrder" type="text" class="ui-input-compact">
            </label>
            <label class="block">
              <span class="block text-xs font-medium text-stone-600">{{ t('taxonomySuggest.suggestedFamily') }}</span>
              <input v-model="suggestedFamily" type="text" class="ui-input-compact">
            </label>
            <label class="block">
              <span class="block text-xs font-medium text-stone-600">{{ t('taxonomySuggest.suggestedGenus') }}</span>
              <input v-model="suggestedGenus" type="text" class="ui-input-compact">
            </label>
            <label class="block">
              <span class="block text-xs font-medium text-stone-600">{{ t('taxonomySuggest.suggestedCommonName') }}</span>
              <input v-model="suggestedCommonName" type="text" class="ui-input-compact">
            </label>
          </div>

          <label class="mt-3 block">
            <span class="block text-xs font-medium text-stone-600">{{ t('taxonomySuggest.suggestedScientificName') }}</span>
            <input v-model="suggestedScientificName" type="text" class="ui-input-compact">
          </label>

          <label class="mt-3 block">
            <span class="block text-xs font-medium text-stone-600">{{ t('taxonomySuggest.note') }}</span>
            <textarea
              v-model="note"
              rows="3"
              class="ui-input-compact min-h-[88px]"
              :placeholder="t('taxonomySuggest.notePlaceholder')"
            />
          </label>

          <div class="mt-3 rounded-md border border-stone-200 bg-stone-50 p-3 text-xs text-stone-700">
            <p class="font-medium text-stone-800">
              {{ t('taxonomySuggest.identity') }}
            </p>

            <template v-if="isAuthenticated">
              <p class="mt-1">
                {{ t('taxonomySuggest.authenticatedAs') }} {{ user?.name || user?.email || '—' }}
              </p>
            </template>

            <label class="mt-2 block">
              <span class="block text-xs font-medium text-stone-600">{{ t('taxonomySuggest.email') }}</span>
              <input
                v-model="requesterEmail"
                type="email"
                class="ui-input-compact"
                :placeholder="t('taxonomySuggest.emailPlaceholder')"
                required
              >
            </label>

            <template v-if="!isAuthenticated">
              <div v-if="isCaptchaConfigured" class="mt-2">
                <p class="mb-1 block text-xs font-medium text-stone-600">
                  {{ t('taxonomySuggest.captchaLabel') }}
                </p>
                <div ref="captchaContainer" />
              </div>
              <p v-else class="mt-2 text-xs text-stone-500">
                {{ t('taxonomySuggest.captchaUnavailable') }}
              </p>
            </template>
          </div>

          <p class="mt-3 text-xs text-stone-500">
            {{ t('taxonomySuggest.localOnlyNotice') }}
          </p>
          <p v-if="formError" class="mt-2 text-xs text-red-600">
            {{ formError }}
          </p>

          <div class="mt-4 flex justify-end gap-2">
            <button class="btn-secondary" @click="emit('close')">
              {{ t('taxonomySuggest.cancel') }}
            </button>
            <button class="btn-primary" :disabled="submitting" @click="saveDraft">
              {{ t('taxonomySuggest.saveDraft') }}
            </button>
          </div>
        </section>
      </div>
    </Transition>
  </Teleport>
</template>
