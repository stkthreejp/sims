import { useNavigate, useParams } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { useEffect } from 'react'
import { toast } from 'sonner'
import { insuredsApi } from '@/api/insureds.api'
import { queryClient } from '@/lib/queryClient'
import { PageHeader } from '@/components/common/PageHeader'
import { LoadingSpinner } from '@/components/common/LoadingSpinner'
import { AddressAutocomplete } from '@/components/common/AddressAutocomplete'
import { rhfValidators, formatPhoneInput } from '@/lib/validators'
import { BUSINESS_ENTITY_TYPE_LABELS, type InsuredUpdate } from '@/types/insured.types'

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT','VA','WA','WV','WI','WY']

function getSaveErrorMessage(err: any, fallback: string) {
  const data = err?.response?.data
  const errors = data?.errors
  if (errors && typeof errors === 'object') {
    const first = Object.entries(errors).flatMap(([field, messages]) =>
      Array.isArray(messages) ? messages.map((m) => `${field}: ${m}`) : [`${field}: ${messages}`]
    )[0]
    if (first) return first
  }
  return data?.errorMessage ?? data?.detail ?? data?.title ?? fallback
}

export function InsuredEditPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: insured, isLoading } = useQuery({ queryKey: ['insureds', id], queryFn: () => insuredsApi.getById(id!) })
  const { register, handleSubmit, reset, watch, setValue, formState: { errors } } = useForm<InsuredUpdate>()

  useEffect(() => { if (insured) reset(insured as unknown as InsuredUpdate) }, [insured, reset])

  const addressLine1Value = watch('addressLine1') ?? ''

  const mutation = useMutation({
    mutationFn: (data: InsuredUpdate) => insuredsApi.update(id!, data),
    onSuccess: () => {
      toast.success('Insured updated')
      queryClient.invalidateQueries({ queryKey: ['insureds'] })
      navigate(`/insureds/${id}`)
    },
    onError: (err: any) => toast.error(getSaveErrorMessage(err, 'Failed to update insured')),
  })

  if (isLoading) return <LoadingSpinner />

  return (
    <div className="max-w-2xl">
      <PageHeader title="Edit Insured" />
      <form onSubmit={handleSubmit((d) => mutation.mutate(d))} className="bg-white rounded-lg border border-slate-200 p-6 space-y-5">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">First Name</label>
            <input {...register('firstName')} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Last Name</label>
            <input {...register('lastName')} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        </div>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Company Name</label>
            <input {...register('companyName')} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div className="w-1/2">
            <label className="block text-sm font-medium text-slate-700 mb-1">USDOT #</label>
            <input {...register('usDotNumber')} inputMode="numeric" placeholder="Optional" className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">DBA</label>
            <input {...register('dba')} placeholder="Doing business as (optional)" className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Entity Type</label>
            <select {...register('entityType')} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              <option value="">— Select —</option>
              {(Object.keys(BUSINESS_ENTITY_TYPE_LABELS) as (keyof typeof BUSINESS_ENTITY_TYPE_LABELS)[]).map((k) => (
                <option key={k} value={k}>{BUSINESS_ENTITY_TYPE_LABELS[k]}</option>
              ))}
            </select>
          </div>
        </div>
        <div className="w-1/3">
          <label className="block text-sm font-medium text-slate-700 mb-1">Years in Business</label>
          <input {...register('yearsInBusiness', { valueAsNumber: true })} type="number" min="0" max="200" placeholder="e.g. 12" className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Email *</label>
            <input
              {...register('email', { required: 'Required', validate: rhfValidators.email })}
              type="text"
              placeholder="name@example.com"
              className={`w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.email ? 'border-red-400' : 'border-slate-300'}`}
            />
            {errors.email && <p className="text-xs text-red-600 mt-1">{errors.email.message}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">Phone *</label>
            <input
              {...register('phone', { required: 'Required', validate: rhfValidators.phone })}
              type="text"
              placeholder="(555) 123-4567"
              onChange={(e) => setValue('phone', formatPhoneInput(e.target.value), { shouldValidate: true })}
              className={`w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.phone ? 'border-red-400' : 'border-slate-300'}`}
            />
            {errors.phone && <p className="text-xs text-red-600 mt-1">{errors.phone.message}</p>}
          </div>
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">Address</label>
          <input type="hidden" {...register('addressLine1', { required: 'Required' })} />
          <AddressAutocomplete
            value={addressLine1Value}
            onChange={(val) => {
              setValue('addressLine1', val, { shouldValidate: true })
              setValue('latitude', undefined)
              setValue('longitude', undefined)
              setValue('geocodePrecision', undefined)
              setValue('geocodeProvider', undefined)
              setValue('googlePlaceId', undefined)
            }}
            onSelect={({ addressLine1, city, state, zipCode, latitude, longitude, geocodePrecision, geocodeProvider, googlePlaceId }) => {
              setValue('addressLine1', addressLine1, { shouldValidate: true })
              setValue('city', city, { shouldValidate: true })
              if (state) setValue('state', state)
              setValue('zipCode', zipCode, { shouldValidate: true })
              setValue('latitude', latitude)
              setValue('longitude', longitude)
              setValue('geocodePrecision', geocodePrecision)
              setValue('geocodeProvider', geocodeProvider)
              setValue('googlePlaceId', googlePlaceId)
            }}
            hasError={!!errors.addressLine1}
            className="mb-2"
          />
          {errors.addressLine1 && <p className="text-xs text-red-600 mt-1">{errors.addressLine1.message}</p>}
          <input {...register('addressLine2')} placeholder="Suite, unit, etc. (optional)" className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <div className="grid grid-cols-3 gap-4">
          <div className="col-span-1">
            <label className="block text-sm font-medium text-slate-700 mb-1">City</label>
            <input
              {...register('city', { required: 'Required' })}
              className={`w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.city ? 'border-red-400' : 'border-slate-300'}`}
            />
            {errors.city && <p className="text-xs text-red-600 mt-1">{errors.city.message}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">State</label>
            <select {...register('state')} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
              {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">ZIP</label>
            <input
              {...register('zipCode', { required: 'Required', validate: rhfValidators.zip })}
              placeholder="78701"
              className={`w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${errors.zipCode ? 'border-red-400' : 'border-slate-300'}`}
            />
            {errors.zipCode && <p className="text-xs text-red-600 mt-1">{errors.zipCode.message}</p>}
          </div>
        </div>
        <div className="flex gap-3 pt-2">
          <button type="submit" disabled={mutation.isPending} className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white text-sm font-medium rounded-md">
            {mutation.isPending ? 'Saving…' : 'Save Changes'}
          </button>
          <button type="button" onClick={() => navigate(`/insureds/${id}`)} className="px-4 py-2 border border-slate-300 text-sm rounded-md hover:bg-slate-50">Cancel</button>
        </div>
      </form>
    </div>
  )
}
