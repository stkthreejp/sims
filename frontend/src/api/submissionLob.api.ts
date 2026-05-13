import { apiClient } from './client'
import type {
  SubmissionLocation,
  SubmissionLocationCreate,
  SubmissionDriver,
  SubmissionDriverCreate,
  SubmissionVehicle,
  SubmissionVehicleCreate,
  SubmissionPriorCarrier,
  SubmissionPriorCarrierCreate,
  SubmissionAdditionalInterest,
  SubmissionAdditionalInterestCreate,
  SubmissionAdditionalInterestBlanket,
  SubmissionAdditionalInterestBlanketUpsert,
  SubmissionSupplemental,
  SubmissionSupplementalUpsert,
  SubmissionGLCoverages,
  SubmissionGLCoveragesUpsert,
  SubmissionGLClassification,
  SubmissionGLClassificationCreate,
  SubmissionIMCoverages,
  SubmissionIMCoveragesUpsert,
  SubmissionEquipment,
  SubmissionEquipmentCreate,
  IMEquipmentType,
  IMTerritory,
} from '@/types/submissionLob.types'

const base = (submissionId: string) => `/submissions/${submissionId}`

export const submissionDriversApi = {
  getAll: (submissionId: string) =>
    apiClient.get<SubmissionDriver[]>(`${base(submissionId)}/drivers`).then((r) => r.data),
  create: (submissionId: string, dto: SubmissionDriverCreate) =>
    apiClient.post<SubmissionDriver>(`${base(submissionId)}/drivers`, dto).then((r) => r.data),
  update: (submissionId: string, id: string, dto: SubmissionDriverCreate) =>
    apiClient.put<SubmissionDriver>(`${base(submissionId)}/drivers/${id}`, dto).then((r) => r.data),
  delete: (submissionId: string, id: string) =>
    apiClient.delete(`${base(submissionId)}/drivers/${id}`),
}

export const submissionVehiclesApi = {
  getAll: (submissionId: string) =>
    apiClient.get<SubmissionVehicle[]>(`${base(submissionId)}/vehicles`).then((r) => r.data),
  create: (submissionId: string, dto: SubmissionVehicleCreate) =>
    apiClient.post<SubmissionVehicle>(`${base(submissionId)}/vehicles`, dto).then((r) => r.data),
  update: (submissionId: string, id: string, dto: SubmissionVehicleCreate) =>
    apiClient.put<SubmissionVehicle>(`${base(submissionId)}/vehicles/${id}`, dto).then((r) => r.data),
  delete: (submissionId: string, id: string) =>
    apiClient.delete(`${base(submissionId)}/vehicles/${id}`),
}

export const submissionPriorCarriersApi = {
  getAll: (submissionId: string) =>
    apiClient.get<SubmissionPriorCarrier[]>(`${base(submissionId)}/prior-carriers`).then((r) => r.data),
  create: (submissionId: string, dto: SubmissionPriorCarrierCreate) =>
    apiClient.post<SubmissionPriorCarrier>(`${base(submissionId)}/prior-carriers`, dto).then((r) => r.data),
  update: (submissionId: string, id: string, dto: SubmissionPriorCarrierCreate) =>
    apiClient.put<SubmissionPriorCarrier>(`${base(submissionId)}/prior-carriers/${id}`, dto).then((r) => r.data),
  delete: (submissionId: string, id: string) =>
    apiClient.delete(`${base(submissionId)}/prior-carriers/${id}`),
}

export const submissionAdditionalInterestsApi = {
  getAll: (submissionId: string) =>
    apiClient.get<SubmissionAdditionalInterest[]>(`${base(submissionId)}/additional-interests`).then((r) => r.data),
  getBlankets: (submissionId: string) =>
    apiClient.get<SubmissionAdditionalInterestBlanket[]>(`${base(submissionId)}/additional-interests/blankets`).then((r) => r.data),
  upsertBlanket: (submissionId: string, lineOfBusiness: string, dto: SubmissionAdditionalInterestBlanketUpsert) =>
    apiClient.put<SubmissionAdditionalInterestBlanket>(`${base(submissionId)}/additional-interests/blankets/${lineOfBusiness}`, dto).then((r) => r.data),
  create: (submissionId: string, dto: SubmissionAdditionalInterestCreate) =>
    apiClient.post<SubmissionAdditionalInterest>(`${base(submissionId)}/additional-interests`, dto).then((r) => r.data),
  update: (submissionId: string, id: string, dto: SubmissionAdditionalInterestCreate) =>
    apiClient.put<SubmissionAdditionalInterest>(`${base(submissionId)}/additional-interests/${id}`, dto).then((r) => r.data),
  delete: (submissionId: string, id: string) =>
    apiClient.delete(`${base(submissionId)}/additional-interests/${id}`),
}

export const submissionLocationsApi = {
  getAll: (submissionId: string) =>
    apiClient.get<SubmissionLocation[]>(`${base(submissionId)}/locations`).then((r) => r.data),
  create: (submissionId: string, dto: SubmissionLocationCreate) =>
    apiClient.post<SubmissionLocation>(`${base(submissionId)}/locations`, dto).then((r) => r.data),
  update: (submissionId: string, id: string, dto: SubmissionLocationCreate) =>
    apiClient.put<SubmissionLocation>(`${base(submissionId)}/locations/${id}`, dto).then((r) => r.data),
  delete: (submissionId: string, id: string) =>
    apiClient.delete(`${base(submissionId)}/locations/${id}`),
}

export const submissionGLApi = {
  getCoverages: (submissionId: string) =>
    apiClient.get<SubmissionGLCoverages | null>(`${base(submissionId)}/gl/coverages`).then((r) => r.data),
  upsertCoverages: (submissionId: string, dto: SubmissionGLCoveragesUpsert) =>
    apiClient.put<SubmissionGLCoverages>(`${base(submissionId)}/gl/coverages`, dto).then((r) => r.data),
  getClassifications: (submissionId: string) =>
    apiClient.get<SubmissionGLClassification[]>(`${base(submissionId)}/gl/classifications`).then((r) => r.data),
  createClassification: (submissionId: string, dto: SubmissionGLClassificationCreate) =>
    apiClient.post<SubmissionGLClassification>(`${base(submissionId)}/gl/classifications`, dto).then((r) => r.data),
  updateClassification: (submissionId: string, id: string, dto: SubmissionGLClassificationCreate) =>
    apiClient.put<SubmissionGLClassification>(`${base(submissionId)}/gl/classifications/${id}`, dto).then((r) => r.data),
  deleteClassification: (submissionId: string, id: string) =>
    apiClient.delete(`${base(submissionId)}/gl/classifications/${id}`),
}

export const submissionIMApi = {
  getCoverages: (submissionId: string) =>
    apiClient.get<SubmissionIMCoverages | null>(`${base(submissionId)}/im/coverages`).then((r) => r.data),
  upsertCoverages: (submissionId: string, dto: SubmissionIMCoveragesUpsert) =>
    apiClient.put<SubmissionIMCoverages>(`${base(submissionId)}/im/coverages`, dto).then((r) => r.data),
  getEquipment: (submissionId: string) =>
    apiClient.get<SubmissionEquipment[]>(`${base(submissionId)}/im/equipment`).then((r) => r.data),
  createEquipment: (submissionId: string, dto: SubmissionEquipmentCreate) =>
    apiClient.post<SubmissionEquipment>(`${base(submissionId)}/im/equipment`, dto).then((r) => r.data),
  updateEquipment: (submissionId: string, id: string, dto: SubmissionEquipmentCreate) =>
    apiClient.put<SubmissionEquipment>(`${base(submissionId)}/im/equipment/${id}`, dto).then((r) => r.data),
  deleteEquipment: (submissionId: string, id: string) =>
    apiClient.delete(`${base(submissionId)}/im/equipment/${id}`),
}

export const imLookupsApi = {
  getEquipmentTypes: () =>
    apiClient.get<IMEquipmentType[]>('/im/equipment-types').then((r) => r.data),
  getTerritories: () =>
    apiClient.get<IMTerritory[]>('/im/territories').then((r) => r.data),
}

export const submissionSupplementalApi = {
  get: (submissionId: string) =>
    apiClient.get<SubmissionSupplemental | null>(`${base(submissionId)}/supplemental`).then((r) => r.data),
  upsert: (submissionId: string, dto: SubmissionSupplementalUpsert) =>
    apiClient.put<SubmissionSupplemental>(`${base(submissionId)}/supplemental`, dto).then((r) => r.data),
}
