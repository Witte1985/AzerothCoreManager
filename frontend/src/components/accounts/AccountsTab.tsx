import { useState, useMemo } from 'react'
import { Plus, Search, Loader2, Users, Shield } from 'lucide-react'
import { useAccounts, useCreateAccount } from '@/hooks/useAccounts'
import type { AccountDto } from '@/types/account.types'
import AccountDetailsPanel from './AccountDetailsPanel'
import CreateAccountDialog from './dialogs/CreateAccountDialog'

interface AccountsTabProps {
  stackId: string
}

export default function AccountsTab({ stackId }: AccountsTabProps) {
  const { data: accounts, isLoading, error } = useAccounts(stackId)
  const createAccountMutation = useCreateAccount(stackId)
  const [selectedAccount, setSelectedAccount] = useState<AccountDto | null>(null)
  const [showCreateDialog, setShowCreateDialog] = useState(false)
  const [searchQuery, setSearchQuery] = useState('')

  // Filter accounts by search query
  const filteredAccounts = useMemo(() => {
    if (!accounts) return []
    if (!searchQuery.trim()) return accounts
    const query = searchQuery.toLowerCase()
    return accounts.filter((account) =>
      account.username.toLowerCase().includes(query)
    )
  }, [accounts, searchQuery])

  const handleCreateAccount = async (username: string, password: string) => {
    await createAccountMutation.mutateAsync({
      username,
      password,
      expansion: 2, // WotLK
    })
  }

  const getGmLevelBadge = (level: number) => {
    switch (level) {
      case 0:
        return null
      case 1:
        return <span className="text-xs text-blue-600">Mod</span>
      case 2:
        return <span className="text-xs text-purple-600">GM</span>
      case 3:
        return <span className="text-xs text-red-600">Admin</span>
      default:
        return null
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="w-8 h-8 text-blue-600 animate-spin" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-4">
        <p className="text-red-800">
          Failed to load accounts. Make sure the server is running.
        </p>
      </div>
    )
  }

  return (
    <>
      <div className="space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Users className="w-5 h-5 text-gray-600" />
            <h2 className="text-xl font-semibold">
              Accounts ({accounts?.length || 0})
            </h2>
          </div>
          <button
            onClick={() => setShowCreateDialog(true)}
            className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 flex items-center gap-2"
          >
            <Plus className="w-4 h-4" />
            Create Account
          </button>
        </div>

        {/* Search */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search accounts by username..."
            className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>

        {/* Layout: Table + Details Panel */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          {/* Accounts Table */}
          <div className="border border-gray-200 rounded-lg overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full">
                <thead className="bg-gray-50 border-b border-gray-200">
                  <tr>
                    <th className="px-4 py-3 text-left text-xs font-semibold text-gray-700 uppercase">
                      Username
                    </th>
                    <th className="px-4 py-3 text-center text-xs font-semibold text-gray-700 uppercase">
                      Status
                    </th>
                    <th className="px-4 py-3 text-center text-xs font-semibold text-gray-700 uppercase">
                      GM
                    </th>
                    <th className="px-4 py-3 text-center text-xs font-semibold text-gray-700 uppercase">
                      Chars
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {filteredAccounts.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-4 py-8 text-center text-gray-500">
                        {searchQuery ? 'No accounts found' : 'No accounts yet. Create one to get started.'}
                      </td>
                    </tr>
                  ) : (
                    filteredAccounts.map((account) => (
                      <tr
                        key={account.id}
                        onClick={() => setSelectedAccount(account)}
                        className={`cursor-pointer hover:bg-gray-50 transition-colors ${
                          selectedAccount?.id === account.id ? 'bg-blue-50' : ''
                        }`}
                      >
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <span className="font-medium">{account.username}</span>
                            {account.banDate && (
                              <span className="text-xs text-red-600">🚫</span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-center">
                          {account.isOnline ? (
                            <span className="inline-block w-2 h-2 bg-green-500 rounded-full"></span>
                          ) : (
                            <span className="inline-block w-2 h-2 bg-gray-300 rounded-full"></span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-center">
                          <div className="flex items-center justify-center gap-1">
                            {account.gmLevel > 0 && <Shield className="w-3 h-3" />}
                            {getGmLevelBadge(account.gmLevel)}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-center">
                          <span className="text-sm text-gray-600">
                            {account.characters.length}
                          </span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Details Panel */}
          <div>
            {selectedAccount ? (
              <AccountDetailsPanel
                account={selectedAccount}
                stackId={stackId}
                onClose={() => setSelectedAccount(null)}
              />
            ) : (
              <div className="border border-gray-200 rounded-lg bg-gray-50 flex items-center justify-center h-64">
                <p className="text-gray-500">Select an account to view details</p>
              </div>
            )}
          </div>
        </div>
      </div>

      {showCreateDialog && (
        <CreateAccountDialog
          onClose={() => setShowCreateDialog(false)}
          onSubmit={handleCreateAccount}
        />
      )}
    </>
  )
}
