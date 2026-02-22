import { PublicClientApplication } from '@azure/msal-browser'

/**
 * Client-only plugin that creates a single MSAL instance shared
 * across the entire app.
 *
 * Uses the redirect flow: the browser navigates to Microsoft login
 * and back. On return, handleRedirectPromise() processes the response
 * and caches the account/tokens in localStorage.
 */
export default defineNuxtPlugin(async () => {
  const config = useRuntimeConfig()

  const msalInstance = new PublicClientApplication({
    auth: {
      clientId: config.public.azureClientId,
      authority: `https://login.microsoftonline.com/${config.public.azureTenantId}`,
      redirectUri: config.public.azureRedirectUri,
    },
    cache: {
      cacheLocation: 'localStorage',
    },
  })

  await msalInstance.initialize()

  // Process the auth response when Microsoft redirects back.
  // Returns null if this is a normal page load (no redirect response).
  await msalInstance.handleRedirectPromise()

  return {
    provide: {
      msalInstance,
    },
  }
})
