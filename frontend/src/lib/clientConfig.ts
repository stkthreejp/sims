declare global {
  interface Window {
    __SIMS_CONFIG__?: {
      GOOGLE_MAPS_API_KEY?: string
    }
  }
}

export function getGoogleMapsApiKey(): string | undefined {
  const runtimeKey = window.__SIMS_CONFIG__?.GOOGLE_MAPS_API_KEY
  if (runtimeKey && !runtimeKey.includes('${')) return runtimeKey

  return import.meta.env.VITE_GOOGLE_MAPS_API_KEY
}
