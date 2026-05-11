import { useEffect, useRef, useState } from 'react'

export interface AddressComponents {
  addressLine1: string
  city: string
  state: string
  zipCode: string
  latitude?: number
  longitude?: number
  geocodePrecision?: string
  geocodeProvider?: string
  googlePlaceId?: string
}

interface Props {
  value: string
  onChange: (raw: string) => void
  onSelect: (components: AddressComponents) => void
  placeholder?: string
  className?: string
  hasError?: boolean
}

const MAPS_SCRIPT_ID = 'google-maps-places'

function loadMapsScript(): Promise<void> {
  return new Promise((resolve, reject) => {
    if (typeof window === 'undefined') return
    const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY
    if (!apiKey) {
      reject(new Error('Google Maps API key is not configured'))
      return
    }

    // Already loaded
    if ((window as any).google?.maps?.places) {
      resolve()
      return
    }

    // Script tag already injected — wait for it
    const existing = document.getElementById(MAPS_SCRIPT_ID)
    if (existing) {
      existing.addEventListener('load', () => resolve())
      existing.addEventListener('error', reject)
      return
    }

    const script = document.createElement('script')
    script.id = MAPS_SCRIPT_ID
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&libraries=places`
    script.async = true
    script.defer = true
    script.onload = () => resolve()
    script.onerror = reject
    document.head.appendChild(script)
  })
}

export function AddressAutocomplete({
  value,
  onChange,
  onSelect,
  placeholder = 'Street address',
  className = '',
  hasError = false,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [loadError, setLoadError] = useState(false)
  const [ready, setReady] = useState(
    () => !!(window as any).google?.maps?.places
  )

  useEffect(() => {
    if (ready) return
    loadMapsScript()
      .then(() => {
        setLoadError(false)
        setReady(true)
      })
      .catch(() => {
        setLoadError(true)
        console.warn('Google Maps failed to load')
      })
  }, [ready])

  useEffect(() => {
    if (!ready || !inputRef.current) return

    const g = (window as any).google
    const autocomplete = new g.maps.places.Autocomplete(inputRef.current, {
      types: ['address'],
      componentRestrictions: { country: 'us' },
      fields: ['address_components', 'geometry', 'place_id', 'types'],
    })

    const listener = autocomplete.addListener('place_changed', () => {
      const place = autocomplete.getPlace()
      if (!place?.address_components) return

      const get = (type: string) =>
        place.address_components.find((c: any) => c.types.includes(type))

      const streetNumber = get('street_number')?.long_name ?? ''
      const route = get('route')?.long_name ?? ''
      const city =
        get('locality')?.long_name ??
        get('sublocality_level_1')?.long_name ??
        get('administrative_area_level_3')?.long_name ??
        ''
      const state = get('administrative_area_level_1')?.short_name ?? ''
      const zipCode = get('postal_code')?.long_name ?? ''
      const addressLine1 = [streetNumber, route].filter(Boolean).join(' ')
      const location = place.geometry?.location
      const latitude = location ? location.lat() : undefined
      const longitude = location ? location.lng() : undefined
      const precision = Array.isArray(place.types) && place.types.length > 0 ? place.types[0] : undefined

      onChange(addressLine1)
      onSelect({
        addressLine1,
        city,
        state,
        zipCode,
        latitude,
        longitude,
        geocodePrecision: precision,
        geocodeProvider: latitude != null && longitude != null ? 'GooglePlaces' : undefined,
        googlePlaceId: place.place_id,
      })
    })

    return () => {
      (window as any).google?.maps?.event?.removeListener(listener)
      document.querySelectorAll('.pac-container').forEach((el) => el.remove())
    }
  }, [ready])

  return (
    <div className={className}>
      <input
        ref={inputRef}
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        autoComplete="off"
        className={`w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${hasError ? 'border-red-400' : 'border-slate-300'}`}
      />
      {loadError && (
        <p className="mt-1 text-xs text-amber-700">
          Address lookup is unavailable. Enter the address manually and save to geocode from the backend.
        </p>
      )}
    </div>
  )
}
