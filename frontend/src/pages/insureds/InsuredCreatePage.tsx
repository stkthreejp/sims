import { useNavigate } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { insuredsApi } from '@/api/insureds.api'
import { queryClient } from '@/lib/queryClient'
import { PageHeader } from '@/components/common/PageHeader'
import { AddressAutocomplete } from '@/components/common/AddressAutocomplete'
import { rhfValidators, formatPhoneInput } from '@/lib/validators'
import { getApiErrorMessage } from '@/lib/apiError'
import { US_STATES } from '@/constants/usStates'
import type { InsuredCreate, InsuredType } from '@/types/insured.types'

export function InsuredCreatePage() {
  const navigate = useNavigate()
  const { register, handleSubmit, watch, setValue, getValues, formState: { errors } } = useForm<InsuredCreate>({
    defaultValues: { insuredType: 'Commercial', state: 'TX' }
  })
  const insuredType = watch('insuredType') as InsuredType
  const addressLine1Value = watch('addressLine1') ?? ''

  const mutation = useMutation({
    mutationFn: insuredsApi.create,
    onSuccess: (insured) => {
      toast.success('Insured created')
      queryClient.invalidateQueries({ queryKey: ['insureds'] })
      navigate(`/insureds/${insured.id}`)
    },
    onError: (err: any) => toast.error(getApiErrorMessage(err, 'Failed to create insured')),
  })

  return (
    <div className="max-w-2xl">
      <PageHeader title="New Insured" />
      <form onSubmit={handleSubmit((d) => mutation.mutate(d))} className="bg-white rounded-lg border border-slate-200 p-6 space-y-5">
        <div>
          <label className="block text-sm font-medium text-slate-700 mb-1">Type</label>
          <select {...register('insuredType')} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
            <option value="Commercial">Commercial</option>
            <option value="Individual">Individual</option>
          </select>
        </div>

        {insuredType === 'Individual' ? (
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">First Name</label>
              <input {...register('firstName', { required: 'Required' })} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              {errors.firstName && <p className="text-xs text-red-600 mt-1">{errors.firstName.message}</p>}
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Last Name</label>
              <input {...register('lastName', { required: 'Required' })} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              {errors.lastName && <p className="text-xs text-red-600 mt-1">{errors.lastName.message}</p>}
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-slate-700 mb-1">Company Name</label>
              <input {...register('companyName', { required: 'Required' })} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
              {errors.companyName && <p className="text-xs text-red-600 mt-1">{errors.companyName.message}</p>}
            </div>
            <div className="w-1/2">
              <label className="block text-sm font-medium text-slate-700 mb-1">USDOT #</label>
              <input {...register('usDotNumber')} inputMode="numeric" placeholder="Optional" className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
            </div>
          </div>
        )}

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
          {/* Hidden field keeps RHF aware of the value; AddressAutocomplete drives the display */}
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
            <select {...register('state', { required: 'Required' })} className="w-full px-3 py-2 border border-slate-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500">
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
            {mutation.isPending ? 'Saving…' : 'Create Insured'}
          </button>
          <button type="button" onClick={() => navigate('/insureds')} className="px-4 py-2 border border-slate-300 text-sm rounded-md hover:bg-slate-50">
            Cancel
          </button>
        </div>
      </form>
    </div>
  )
}
