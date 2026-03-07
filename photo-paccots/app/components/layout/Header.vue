<script setup lang="ts">
import { Bars3Icon, CameraIcon, XMarkIcon } from '@heroicons/vue/24/outline'
import { useTranslations } from '~/composables/useTranslations'

const { user, isAuthenticated, login, logout } = useAuth()
const mobileMenuOpen = ref(false)
const { t, locale, setLocale, supportedLocales } = useTranslations()
const api = useApi()
const route = useRoute()
const pendingTaxonomySuggestions = ref(0)

const refreshPendingTaxonomySuggestions = async () => {
  if (!isAuthenticated.value) {
    pendingTaxonomySuggestions.value = 0
    return
  }
  try {
    const result = await api.getManageTaxonomySuggestionCount()
    pendingTaxonomySuggestions.value = Math.max(0, result.pendingCount || 0)
  } catch {
    pendingTaxonomySuggestions.value = 0
  }
}

watch(isAuthenticated, () => {
  refreshPendingTaxonomySuggestions()
}, { immediate: true })

watch(() => route.fullPath, () => {
  if (isAuthenticated.value) refreshPendingTaxonomySuggestions()
})
</script>

<template>
  <header class="sticky top-0 z-40 border-b border-stone-200 bg-white/90 backdrop-blur-md">
    <nav class="mx-auto flex max-w-[88rem] items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
      <!-- Logo / Brand -->
      <NuxtLink to="/" class="ui-focus-ring ui-transition-color flex items-center gap-2 rounded-md text-lg font-semibold text-stone-900 hover:text-emerald-700">
        <CameraIcon class="h-6 w-6 text-emerald-600" />
        <span class="hidden sm:inline">{{ t('app.brand') }}</span>
      </NuxtLink>

      <!-- Desktop nav -->
      <div class="hidden items-center gap-4 lg:gap-5 sm:flex">
        <NuxtLink to="/" class="ui-focus-ring ui-transition-color whitespace-nowrap rounded-md text-sm font-medium text-stone-600 hover:text-emerald-700">
          {{ t('nav.gallery') }}
        </NuxtLink>
        <NuxtLink to="/project" class="ui-focus-ring ui-transition-color whitespace-nowrap rounded-md text-sm font-medium text-stone-600 hover:text-emerald-700">
          {{ t('nav.project') }}
        </NuxtLink>

        <NuxtLink
          v-if="isAuthenticated"
          to="/upload"
          class="ui-focus-ring ui-transition-color whitespace-nowrap rounded-md text-sm font-medium text-stone-600 hover:text-emerald-700"
        >
          {{ t('nav.upload') }}
        </NuxtLink>

        <NuxtLink
          v-if="isAuthenticated"
          to="/manage"
          class="ui-focus-ring ui-transition-color inline-flex items-center gap-1 whitespace-nowrap rounded-md text-sm font-medium text-stone-600 hover:text-emerald-700"
        >
          {{ t('nav.manage') }}
          <span
            v-if="pendingTaxonomySuggestions > 0"
            class="inline-flex min-w-5 items-center justify-center rounded-full bg-emerald-600 px-1.5 text-[10px] font-semibold leading-4 text-white"
          >
            {{ pendingTaxonomySuggestions }}
          </span>
        </NuxtLink>

        <select
          :value="locale"
          class="ui-input-compact mt-0 py-1 text-xs text-stone-700"
          @change="setLocale(($event.target as HTMLSelectElement).value as 'en' | 'fr' | 'de' | 'it')"
        >
          <option v-for="lang in supportedLocales" :key="lang" :value="lang">
            {{ lang.toUpperCase() }}
          </option>
        </select>

        <!-- Auth button -->
        <template v-if="isAuthenticated">
          <span class="text-xs text-stone-500">{{ user?.name }}</span>
          <button
            class="btn-secondary py-1.5"
            @click="logout"
          >
            {{ t('auth.signOut') }}
          </button>
        </template>
        <button
          v-else
          class="btn-primary px-4 py-1.5"
          @click="login"
        >
          {{ t('auth.signIn') }}
        </button>
      </div>

      <!-- Mobile hamburger -->
      <button
        class="ui-focus-ring ui-transition-color rounded-md p-1.5 text-stone-500 hover:bg-stone-100 sm:hidden"
        :aria-label="mobileMenuOpen ? 'Close menu' : 'Open menu'"
        @click="mobileMenuOpen = !mobileMenuOpen"
      >
        <Bars3Icon v-if="!mobileMenuOpen" class="h-6 w-6" />
        <XMarkIcon v-else class="h-6 w-6" />
      </button>
    </nav>

    <!-- Mobile menu -->
    <Transition
      enter-active-class="transition duration-200 ease-out motion-reduce:transition-none"
      enter-from-class="opacity-0 -translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition duration-150 ease-in motion-reduce:transition-none"
      leave-from-class="opacity-100 translate-y-0"
      leave-to-class="opacity-0 -translate-y-1"
    >
      <div v-if="mobileMenuOpen" class="border-t border-stone-200 bg-white px-4 pb-4 pt-2 sm:hidden">
        <NuxtLink to="/" class="ui-focus-ring ui-transition-color block rounded-md px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-100" @click="mobileMenuOpen = false">
          {{ t('nav.gallery') }}
        </NuxtLink>
        <NuxtLink to="/project" class="ui-focus-ring ui-transition-color block rounded-md px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-100" @click="mobileMenuOpen = false">
          {{ t('nav.project') }}
        </NuxtLink>
        <NuxtLink v-if="isAuthenticated" to="/upload" class="ui-focus-ring ui-transition-color block rounded-md px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-100" @click="mobileMenuOpen = false">
          {{ t('nav.upload') }}
        </NuxtLink>
        <NuxtLink v-if="isAuthenticated" to="/manage" class="ui-focus-ring ui-transition-color flex items-center justify-between rounded-md px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-100" @click="mobileMenuOpen = false">
          <span>{{ t('nav.manage') }}</span>
          <span
            v-if="pendingTaxonomySuggestions > 0"
            class="inline-flex min-w-5 items-center justify-center rounded-full bg-emerald-600 px-1.5 text-[10px] font-semibold leading-4 text-white"
          >
            {{ pendingTaxonomySuggestions }}
          </span>
        </NuxtLink>

        <div class="mt-2 border-t border-stone-100 pt-2">
          <template v-if="isAuthenticated">
            <p class="px-3 text-xs text-stone-500">{{ user?.name }}</p>
            <button class="ui-focus-ring ui-transition-color mt-1 block w-full rounded-md px-3 py-2 text-left text-sm font-medium text-stone-700 hover:bg-stone-100" @click="logout(); mobileMenuOpen = false">
              {{ t('auth.signOut') }}
            </button>
          </template>
          <button
            v-else
            class="btn-primary w-full"
            @click="login(); mobileMenuOpen = false"
          >
            {{ t('auth.signIn') }}
          </button>
        </div>
        <div class="mt-2 px-3">
          <label class="mb-1 block text-xs text-stone-500">Lang</label>
          <select
            :value="locale"
            class="ui-input-compact mt-0 w-full py-1 text-xs text-stone-700"
            @change="setLocale(($event.target as HTMLSelectElement).value as 'en' | 'fr' | 'de' | 'it')"
          >
            <option v-for="lang in supportedLocales" :key="lang" :value="lang">
              {{ lang.toUpperCase() }}
            </option>
          </select>
        </div>
      </div>
    </Transition>
  </header>
</template>
