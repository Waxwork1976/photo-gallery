<script setup lang="ts">
const { user, isAuthenticated, login, logout } = useAuth()
const mobileMenuOpen = ref(false)
</script>

<template>
  <header class="sticky top-0 z-40 border-b border-stone-200 bg-white/90 backdrop-blur-md">
    <nav class="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6 lg:px-8">
      <!-- Logo / Brand -->
      <NuxtLink to="/" class="flex items-center gap-2 text-lg font-semibold text-stone-900 hover:text-emerald-700 transition-colors">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6 text-emerald-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" />
          <path stroke-linecap="round" stroke-linejoin="round" d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" />
        </svg>
        <span class="hidden sm:inline">Photo Paccots</span>
      </NuxtLink>

      <!-- Desktop nav -->
      <div class="hidden items-center gap-6 sm:flex">
        <NuxtLink to="/" class="text-sm font-medium text-stone-600 hover:text-emerald-700 transition-colors">
          Gallery
        </NuxtLink>

        <NuxtLink
          v-if="isAuthenticated"
          to="/upload"
          class="text-sm font-medium text-stone-600 hover:text-emerald-700 transition-colors"
        >
          Upload
        </NuxtLink>

        <NuxtLink
          v-if="isAuthenticated"
          to="/manage"
          class="text-sm font-medium text-stone-600 hover:text-emerald-700 transition-colors"
        >
          Manage
        </NuxtLink>

        <!-- Auth button -->
        <template v-if="isAuthenticated">
          <span class="text-xs text-stone-400">{{ user?.name }}</span>
          <button
            class="rounded-lg bg-stone-100 px-3 py-1.5 text-sm font-medium text-stone-700 hover:bg-stone-200 transition-colors"
            @click="logout"
          >
            Sign out
          </button>
        </template>
        <button
          v-else
          class="rounded-lg bg-emerald-600 px-4 py-1.5 text-sm font-medium text-white shadow-sm hover:bg-emerald-700 transition-colors"
          @click="login"
        >
          Sign in
        </button>
      </div>

      <!-- Mobile hamburger -->
      <button
        class="rounded-md p-1.5 text-stone-500 hover:bg-stone-100 sm:hidden"
        @click="mobileMenuOpen = !mobileMenuOpen"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path v-if="!mobileMenuOpen" stroke-linecap="round" stroke-linejoin="round" d="M4 6h16M4 12h16M4 18h16" />
          <path v-else stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </nav>

    <!-- Mobile menu -->
    <Transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="opacity-100 translate-y-0"
      leave-to-class="opacity-0 -translate-y-1"
    >
      <div v-if="mobileMenuOpen" class="border-t border-stone-200 bg-white px-4 pb-4 pt-2 sm:hidden">
        <NuxtLink to="/" class="block rounded-md px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-100" @click="mobileMenuOpen = false">
          Gallery
        </NuxtLink>
        <NuxtLink v-if="isAuthenticated" to="/upload" class="block rounded-md px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-100" @click="mobileMenuOpen = false">
          Upload
        </NuxtLink>
        <NuxtLink v-if="isAuthenticated" to="/manage" class="block rounded-md px-3 py-2 text-sm font-medium text-stone-700 hover:bg-stone-100" @click="mobileMenuOpen = false">
          Manage
        </NuxtLink>

        <div class="mt-2 border-t border-stone-100 pt-2">
          <template v-if="isAuthenticated">
            <p class="px-3 text-xs text-stone-400">{{ user?.name }}</p>
            <button class="mt-1 block w-full rounded-md px-3 py-2 text-left text-sm font-medium text-stone-700 hover:bg-stone-100" @click="logout(); mobileMenuOpen = false">
              Sign out
            </button>
          </template>
          <button
            v-else
            class="block w-full rounded-md bg-emerald-600 px-3 py-2 text-center text-sm font-medium text-white hover:bg-emerald-700"
            @click="login(); mobileMenuOpen = false"
          >
            Sign in
          </button>
        </div>
      </div>
    </Transition>
  </header>
</template>
