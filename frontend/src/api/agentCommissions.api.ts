import type {
  AgentCommission,
  CreateAgentCommissionRequest,
  DisableAgentCommissionRequest,
} from '@/types/agentCommission.types'

async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const token = localStorage.getItem('token')
  const res = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options?.headers,
    },
  })
  if (!res.ok) {
    const err = await res.json().catch(() => ({}))
    throw new Error(err.errorMessage ?? `Request failed: ${res.status}`)
  }
  return res.json()
}

const base = (agentId: string) => `/api/v1/agents/${agentId}/commissions`

export const getAgentCommissions = (agentId: string): Promise<AgentCommission[]> =>
  apiFetch(base(agentId))

export const createAgentCommission = (
  agentId: string,
  req: CreateAgentCommissionRequest,
): Promise<AgentCommission> =>
  apiFetch(base(agentId), { method: 'POST', body: JSON.stringify(req) })

export const disableAgentCommission = (
  agentId: string,
  id: number,
  req: DisableAgentCommissionRequest,
): Promise<AgentCommission> =>
  apiFetch(`${base(agentId)}/${id}/disable`, { method: 'POST', body: JSON.stringify(req) })
