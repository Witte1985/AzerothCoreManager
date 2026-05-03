import { useState } from 'react'
import { Shield, Ban, Trash2, Key, X } from 'lucide-react'
import type { AccountDto } from '@/types/account.types'
import CharacterCard from './CharacterCard'
import BanAccountDialog from './dialogs/BanAccountDialog'
import SetPasswordDialog from './dialogs/SetPasswordDialog'
import DeleteAccountDialog from './dialogs/DeleteAccountDialog'
import {
  useSetGmLevel,
  useBanAccount,
  useUnbanAccount,
  useDeleteAccount,
  useSetPassword,
} from '@/hooks/useAccounts'

interface AccountDetailsPanelProps {
  account: AccountDto
  stackId: string
  onClose: () => void
}

export default function AccountDetailsPanel({ account, stackId, onClose }: AccountDetailsPanelProps) {
  const [selectedGmLevel, setSelectedGmLevel] = useState(account.gmLevel.toString())
  const [activeDialog, setActiveDialog] = useState<string | null>(null)

  const setGmLevelMutation = useSetGmLevel(stackId)
  const banAccountMutation = useBanAccount(stackId)
  const unbanAccountMutation = useUnbanAccount(stackId)
  const deleteAccountMutation = useDeleteAccount(stackId)
  const setPasswordMutation = useSetPassword(stackId)

  const handleSetGmLevel = async () => {
    const gmLevel = parseInt(selectedGmLevel)
    if (gmLevel === account.gmLevel) return

    try {
      await setGmLevelMutation.mutateAsync({
        accountId: account.id,
        request: { gmLevel, realmId: -1 }
      })
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to set GM level')
    }
  }

  const handleBan = async (duration: string, reason: string) => {
    await banAccountMutation.mutateAsync({
      accountId: account.id,
      request: { duration, reason }
    })
  }

  const handleUnban = async () => {
    if (confirm(`Unban account ${account.username}?`)) {
      try {
        await unbanAccountMutation.mutateAsync(account.id)
      } catch (err: any) {
        alert(err.response?.data?.message || 'Failed to unban account')
      }
    }
  }

  const handleSetPassword = async (newPassword: string) => {
    await setPasswordMutation.mutateAsync({
      accountId: account.id,
      request: { newPassword }
    })
  }

  const handleDelete = async () => {
    await deleteAccountMutation.mutateAsync(account.id)
    onClose()
  }

  const isBanned = account.banDate !== null

  return (
    <>
      <div className="border border-gray-200 rounded-lg bg-white shadow-sm">
        <div className="flex items-center justify-between p-4 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <h3 className="text-xl font-semibold">{account.username}</h3>
            {account.isOnline && (
              <span className="px-3 py-1 bg-green-100 text-green-700 text-sm rounded-full">
                🟢 Online
              </span>
            )}
            {isBanned && (
              <span className="px-3 py-1 bg-red-100 text-red-700 text-sm rounded-full">
                ⛔ Banned
              </span>
            )}
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-4 space-y-6">
          {/* Account Info */}
          <div>
            <h4 className="font-semibold text-sm text-gray-700 mb-2">Account Information</h4>
            <div className="grid grid-cols-2 gap-2 text-sm">
              <div className="text-gray-600">Account ID:</div>
              <div className="font-medium">{account.id}</div>
              <div className="text-gray-600">Last IP:</div>
              <div className="font-medium">{account.lastIp || 'Never logged in'}</div>
              <div className="text-gray-600">Last Login:</div>
              <div className="font-medium">
                {account.lastLogin ? new Date(account.lastLogin).toLocaleString() : 'Never'}
              </div>
              <div className="text-gray-600">Characters:</div>
              <div className="font-medium">{account.characters.length}</div>
            </div>
          </div>

          {/* Ban Info */}
          {isBanned && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3">
              <h4 className="font-semibold text-sm text-red-800 mb-2">Ban Information</h4>
              <div className="text-sm space-y-1">
                <p className="text-red-700">
                  <span className="font-medium">Banned by:</span> {account.bannedBy || 'Unknown'}
                </p>
                <p className="text-red-700">
                  <span className="font-medium">Reason:</span> {account.banReason || 'No reason given'}
                </p>
                <p className="text-red-700">
                  <span className="font-medium">Date:</span>{' '}
                  {account.banDate ? new Date(account.banDate).toLocaleString() : 'Unknown'}
                </p>
              </div>
            </div>
          )}

          {/* GM Level Control */}
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-2">
              <Shield className="w-4 h-4 inline mr-1" />
              GM Level
            </label>
            <div className="flex gap-2">
              <select
                value={selectedGmLevel}
                onChange={(e) => setSelectedGmLevel(e.target.value)}
                className="flex-1 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
              >
                <option value="0">0 - Player</option>
                <option value="1">1 - Moderator</option>
                <option value="2">2 - Game Master</option>
                <option value="3">3 - Administrator</option>
              </select>
              <button
                onClick={handleSetGmLevel}
                disabled={parseInt(selectedGmLevel) === account.gmLevel || setGmLevelMutation.isPending}
                className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed"
              >
                Set Level
              </button>
            </div>
          </div>

          {/* Actions */}
          <div>
            <h4 className="font-semibold text-sm text-gray-700 mb-2">Account Actions</h4>
            <div className="grid grid-cols-2 gap-2">
              {isBanned ? (
                <button
                  onClick={handleUnban}
                  className="px-4 py-2 bg-green-600 text-white rounded-md hover:bg-green-700 flex items-center justify-center gap-2"
                >
                  <Ban className="w-4 h-4" />
                  Unban Account
                </button>
              ) : (
                <button
                  onClick={() => setActiveDialog('ban')}
                  className="px-4 py-2 bg-orange-600 text-white rounded-md hover:bg-orange-700 flex items-center justify-center gap-2"
                >
                  <Ban className="w-4 h-4" />
                  Ban Account
                </button>
              )}
              <button
                onClick={() => setActiveDialog('password')}
                className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 flex items-center justify-center gap-2"
              >
                <Key className="w-4 h-4" />
                Reset Password
              </button>
              <button
                onClick={() => setActiveDialog('delete')}
                className="px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 flex items-center justify-center gap-2 col-span-2"
              >
                <Trash2 className="w-4 h-4" />
                Delete Account
              </button>
            </div>
          </div>

          {/* Characters */}
          <div>
            <h4 className="font-semibold text-sm text-gray-700 mb-3">
              Characters ({account.characters.length})
            </h4>
            {account.characters.length === 0 ? (
              <p className="text-gray-500 text-sm text-center py-4">No characters found</p>
            ) : (
              <div className="space-y-3">
                {account.characters.map((character) => (
                  <CharacterCard key={character.guid} character={character} stackId={stackId} />
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {activeDialog === 'ban' && (
        <BanAccountDialog
          accountUsername={account.username}
          onClose={() => setActiveDialog(null)}
          onSubmit={handleBan}
        />
      )}
      {activeDialog === 'password' && (
        <SetPasswordDialog
          accountUsername={account.username}
          onClose={() => setActiveDialog(null)}
          onSubmit={handleSetPassword}
        />
      )}
      {activeDialog === 'delete' && (
        <DeleteAccountDialog
          accountUsername={account.username}
          onClose={() => setActiveDialog(null)}
          onSubmit={handleDelete}
        />
      )}
    </>
  )
}
