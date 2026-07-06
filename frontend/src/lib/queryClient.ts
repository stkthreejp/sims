import { QueryClient, MutationCache } from '@tanstack/react-query'
import { toast } from 'sonner'
import { getApiErrorMessage } from './apiError'

export const queryClient = new QueryClient({
  // Safety net: any mutation that fails WITHOUT its own onError handler still
  // surfaces the reason instead of failing silently (audit X5). Mutations that
  // define their own onError keep full control — this only fires as a fallback.
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => {
      if (mutation.options.onError) return
      toast.error(getApiErrorMessage(error))
    },
  }),
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, // 5 minutes
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})
