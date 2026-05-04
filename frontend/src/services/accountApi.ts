import apiClient from './api'
import type {
  AccountDto,
  CharacterDto,
  CreateAccountRequest,
  SetGmLevelRequest,
  BanAccountRequest,
  SetPasswordRequest,
  DeleteAccountRequest,
  UnbanAccountRequest,
  UnbanIpRequest,
  SendMessageRequest,
  SendItemsRequest,
  SendMoneyRequest,
  SetLevelRequest,
  AccountActionResponse,
  CharacterActionResponse,
} from '@/types/account.types'

// Account API
export const accountApi = {
  // List all accounts
  list: (stackId: string) =>
    apiClient.get<AccountDto[]>(`/stacks/${stackId}/accounts`),

  // Get account's characters
  getCharacters: (stackId: string, accountId: number) =>
    apiClient.get<CharacterDto[]>(`/stacks/${stackId}/accounts/${accountId}/characters`),

  // Create account
  create: (stackId: string, request: CreateAccountRequest) =>
    apiClient.post<AccountActionResponse>(`/stacks/${stackId}/accounts`, request),

  // Set GM level
  setGmLevel: (stackId: string, accountId: number, request: SetGmLevelRequest) =>
    apiClient.post<AccountActionResponse>(
      `/stacks/${stackId}/accounts/${accountId}/set-gm-level`,
      request
    ),

  // Ban account
  ban: (stackId: string, accountId: number, request: BanAccountRequest) =>
    apiClient.post<AccountActionResponse>(
      `/stacks/${stackId}/accounts/${accountId}/ban`,
      request
    ),

  // Delete account
  delete: (stackId: string, accountId: number, request: DeleteAccountRequest) =>
    apiClient.delete<AccountActionResponse>(`/stacks/${stackId}/accounts/${accountId}`, { data: request }),

  // Set password
  setPassword: (stackId: string, accountId: number, request: SetPasswordRequest) =>
    apiClient.post<AccountActionResponse>(
      `/stacks/${stackId}/accounts/${accountId}/set-password`,
      request
    ),

  // Unban account
  unban: (stackId: string, accountId: number, request: UnbanAccountRequest) =>
    apiClient.post<AccountActionResponse>(
      `/stacks/${stackId}/accounts/${accountId}/unban`,
      request
    ),

  // Unban IP
  unbanIp: (stackId: string, request: UnbanIpRequest) =>
    apiClient.post<AccountActionResponse>(`/stacks/${stackId}/accounts/unban-ip`, request),
}

// Character API
export const characterApi = {
  // Send message
  sendMessage: (stackId: string, characterName: string, request: SendMessageRequest) =>
    apiClient.post<CharacterActionResponse>(
      `/stacks/${stackId}/characters/${characterName}/send-message`,
      request
    ),

  // Send items
  sendItems: (stackId: string, characterName: string, request: SendItemsRequest) =>
    apiClient.post<CharacterActionResponse>(
      `/stacks/${stackId}/characters/${characterName}/send-items`,
      request
    ),

  // Send money
  sendMoney: (stackId: string, characterName: string, request: SendMoneyRequest) =>
    apiClient.post<CharacterActionResponse>(
      `/stacks/${stackId}/characters/${characterName}/send-money`,
      request
    ),

  // Kick player
  kick: (stackId: string, characterName: string) =>
    apiClient.post<CharacterActionResponse>(
      `/stacks/${stackId}/characters/${characterName}/kick`
    ),

  // Force rename
  rename: (stackId: string, characterName: string) =>
    apiClient.post<CharacterActionResponse>(
      `/stacks/${stackId}/characters/${characterName}/rename`
    ),

  // Force customize
  customize: (stackId: string, characterName: string) =>
    apiClient.post<CharacterActionResponse>(
      `/stacks/${stackId}/characters/${characterName}/customize`
    ),

  // Set level
  setLevel: (stackId: string, characterName: string, request: SetLevelRequest) =>
    apiClient.post<CharacterActionResponse>(
      `/stacks/${stackId}/characters/${characterName}/set-level`,
      request
    ),
}
