// https://nuxt.com/docs/api/configuration/nuxt-config
import tailwindcss from '@tailwindcss/vite'

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',

  ssr: true,

  // Generate static site
  nitro: {
    preset: 'static',
  },

  app: {
    head: {
      title: 'Photo Paccots — Nature & Garden Gallery',
      meta: [
        { charset: 'utf-8' },
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
        {
          name: 'description',
          content: 'Personal nature and garden photography gallery',
        },
      ],
      link: [{ rel: 'icon', type: 'image/x-icon', href: '/favicon.ico' }],
    },
  },

  css: ['~/assets/css/main.css'],

  vite: {
    plugins: [tailwindcss()],
  },

  runtimeConfig: {
    public: {
      azureClientId: process.env.NUXT_PUBLIC_AZURE_CLIENT_ID || '',
      azureTenantId: process.env.NUXT_PUBLIC_AZURE_TENANT_ID || '',
      azureRedirectUri: process.env.NUXT_PUBLIC_AZURE_REDIRECT_URI || '',
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL || '',
      siteUrl: process.env.NUXT_PUBLIC_SITE_URL || '',
    },
  },

  typescript: {
    strict: true,
  },

  devtools: { enabled: true },
})
