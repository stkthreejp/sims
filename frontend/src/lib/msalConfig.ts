import { PublicClientApplication, type Configuration } from '@azure/msal-browser'

const msalConfig: Configuration = {
  auth: {
    clientId: 'a3c3a9ab-09a7-4b9c-8b1e-0d97fe97ff6f',
    authority: 'https://login.microsoftonline.com/49037468-6b8e-4c49-a58e-2588bd7b2706',
    // Main app redirect (used for redirect flow and logout)
    redirectUri: window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage',
  },
}

export const msalInstance = new PublicClientApplication(msalConfig)

// Initialize eagerly — call this once at app startup
let _initPromise: Promise<void> | null = null
export function ensureMsalInitialized(): Promise<void> {
  if (!_initPromise) {
    _initPromise = msalInstance.initialize()
  }
  return _initPromise
}

// loginPopup request — redirect the popup to our lightweight redirect page
// so it doesn't re-load the full React SPA and freeze
export const loginRequest = {
  scopes: ['openid', 'profile', 'email'],
  redirectUri: window.location.origin + '/auth-redirect.html',
}
