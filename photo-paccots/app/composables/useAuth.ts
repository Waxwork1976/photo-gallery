import type { AccountInfo } from '@azure/msal-browser'
import type { AuthUser } from '~/types/auth'

/**
 * Composable that manages Microsoft Entra ID authentication via MSAL.
 * Uses the redirect flow — the browser navigates to Microsoft login and back.
 */
export const useAuth = () => {
  const user = useState<AuthUser | null>('auth-user', () => null)
  const isAuthenticated = computed(() => user.value !== null)

  const getMsal = () => {
    const { $msalInstance } = useNuxtApp()
    return $msalInstance as import('@azure/msal-browser').PublicClientApplication
  }

  const setUserFromAccount = (account: AccountInfo, idToken: string) => {
    user.value = {
      name: account.name || '',
      email: account.username || '',
      oid: account.localAccountId,
      accessToken: idToken,
    }
  }

  /** Redirect to Microsoft login page. */
  const login = async () => {
    const msal = getMsal()
    await msal.loginRedirect({
      scopes: ['openid', 'profile', 'email'],
      prompt: 'select_account',
    })
  }

  /** Sign out via redirect. */
  const logout = async () => {
    const msal = getMsal()
    user.value = null
    await msal.logoutRedirect()
  }

  /** Try to silently restore a session from the MSAL cache. */
  const checkAuth = async () => {
    const msal = getMsal()
    const accounts = msal.getAllAccounts()

    if (accounts.length > 0) {
      try {
        const response = await msal.acquireTokenSilent({
          scopes: ['openid', 'profile', 'email'],
          account: accounts[0],
        })
        setUserFromAccount(accounts[0], response.idToken)
      } catch {
        console.warn('Silent token acquisition failed — user must log in again.')
        user.value = null
      }
    }
  }

  /** Get a fresh ID token for API calls. Returns null if no session. */
  const getAccessToken = async (): Promise<string | null> => {
    const msal = getMsal()
    const accounts = msal.getAllAccounts()

    if (accounts.length === 0) return null

    try {
      const response = await msal.acquireTokenSilent({
        scopes: ['openid', 'profile', 'email'],
        account: accounts[0],
      })
      return response.idToken
    } catch {
      console.error('Failed to acquire token silently')
      return null
    }
  }

  return {
    user: readonly(user),
    isAuthenticated,
    login,
    logout,
    checkAuth,
    getAccessToken,
  }
}
