// Account DTOs
export interface AccountDto {
  id: number
  username: string
  gmLevel: number
  lastLogin: string | null
  characterCount: number
  isOnline: boolean
}

export interface CharacterDto {
  guid: number
  name: string
  race: number
  class: number
  level: number
  gender: number
  online: boolean
  totaltime: number
  leveltime: number
  money: number
  account: number
  map: number
  zone: number
}

// Request DTOs
export interface CreateAccountRequest {
  username: string
  password: string
  expansion?: number
}

export interface SetGmLevelRequest {
  gmLevel: number
  realmId: number
}

export interface BanAccountRequest {
  duration: string // "30m" | "1h" | "12h" | "1d" | "7d" | "30d" | "-1" (permanent)
  reason: string
}

export interface SetPasswordRequest {
  newPassword: string
}

export interface UnbanIpRequest {
  ipAddress: string
}

export interface SendMessageRequest {
  subject: string
  body: string
}

export interface SendItemsRequest {
  itemId: number
  count: number
  subject: string
  body: string
}

export interface SendMoneyRequest {
  copperAmount: number
  subject: string
  body: string
}

export interface SetLevelRequest {
  level: number
}

// Response DTOs
export interface AccountActionResponse {
  success: boolean
  message: string
}

export interface CharacterActionResponse {
  success: boolean
  message: string
}
