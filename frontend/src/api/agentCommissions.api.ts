import { apiClient } from './client'
import type {
  AgentCommission,
  CreateAgentCommissionRequest,
  DisableAgentCommissionRequest,
} from '@/types/agentCommission.types'

const base = (agentId: string) => `/agents/${agentId}/commissions`

export const getAgentCommissions = (agentId: string): Promise<AgentCommission[]> =>
  apiClient.get<AgentCommission[]>(base(agentId)).then((r) => r.data)

export const createAgentCommission = (
  agentId: string,
  req: CreateAgentCommissionRequest,
): Promise<AgentCommission> =>
  apiClient.post<AgentCommission>(base(agentId), req).then((r) => r.data)

export const disableAgentCommission = (
  agentId: string,
  id: number,
  req: DisableAgentCommissionRequest,
): Promise<AgentCommission> =>
  apiClient.post<AgentCommission>(`${base(agentId)}/${id}/disable`, req).then((r) => r.data)
