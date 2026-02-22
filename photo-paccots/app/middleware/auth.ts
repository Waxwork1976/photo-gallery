/**
 * Route middleware that protects pages requiring authentication.
 * If the user is not signed in, they are redirected to the home page.
 */
export default defineNuxtRouteMiddleware(async () => {
  // Only runs on the client — SSG pages have no server session
  if (import.meta.server) return

  const { isAuthenticated, checkAuth } = useAuth()

  // Give MSAL a chance to restore the session from cache
  if (!isAuthenticated.value) {
    await checkAuth()
  }

  if (!isAuthenticated.value) {
    return navigateTo('/')
  }
})
