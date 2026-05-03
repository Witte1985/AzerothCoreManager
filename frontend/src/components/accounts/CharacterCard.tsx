import { useState } from 'react'
import { MoreVertical, MessageSquare, Package, Coins, UserX, Edit, Palette, TrendingUp } from 'lucide-react'
import type { CharacterDto } from '@/types/account.types'
import SendMessageDialog from './dialogs/SendMessageDialog'
import SendItemsDialog from './dialogs/SendItemsDialog'
import SendMoneyDialog from './dialogs/SendMoneyDialog'
import SetLevelDialog from './dialogs/SetLevelDialog'
import {
  useSendMessage,
  useSendItems,
  useSendMoney,
  useKickPlayer,
  useRenameCharacter,
  useCustomizeCharacter,
  useSetLevel,
} from '@/hooks/useAccounts'

// Race and class names for display
const RACES: Record<number, string> = {
  1: 'Human', 2: 'Orc', 3: 'Dwarf', 4: 'Night Elf', 5: 'Undead',
  6: 'Tauren', 7: 'Gnome', 8: 'Troll', 10: 'Blood Elf', 11: 'Draenei',
}

const CLASSES: Record<number, string> = {
  1: 'Warrior', 2: 'Paladin', 3: 'Hunter', 4: 'Rogue', 5: 'Priest',
  6: 'Death Knight', 7: 'Shaman', 8: 'Mage', 9: 'Warlock', 11: 'Druid',
}

interface CharacterCardProps {
  character: CharacterDto
  stackId: string
}

export default function CharacterCard({ character, stackId }: CharacterCardProps) {
  const [showActionsMenu, setShowActionsMenu] = useState(false)
  const [activeDialog, setActiveDialog] = useState<string | null>(null)

  const sendMessageMutation = useSendMessage(stackId)
  const sendItemsMutation = useSendItems(stackId)
  const sendMoneyMutation = useSendMoney(stackId)
  const kickPlayerMutation = useKickPlayer(stackId)
  const renameCharacterMutation = useRenameCharacter(stackId)
  const customizeCharacterMutation = useCustomizeCharacter(stackId)
  const setLevelMutation = useSetLevel(stackId)

  const handleSendMessage = async (subject: string, body: string) => {
    await sendMessageMutation.mutateAsync({ characterName: character.name, request: { subject, body } })
  }

  const handleSendItems = async (itemId: number, count: number, subject: string, body: string) => {
    await sendItemsMutation.mutateAsync({ characterName: character.name, request: { itemId, count, subject, body } })
  }

  const handleSendMoney = async (copperAmount: number, subject: string, body: string) => {
    await sendMoneyMutation.mutateAsync({ characterName: character.name, request: { copperAmount, subject, body } })
  }

  const handleSetLevel = async (level: number) => {
    await setLevelMutation.mutateAsync({ characterName: character.name, request: { level } })
  }

  const handleKick = () => {
    if (confirm(`Kick ${character.name} from the server?`)) {
      kickPlayerMutation.mutate(character.name)
    }
    setShowActionsMenu(false)
  }

  const handleRename = () => {
    if (confirm(`Force ${character.name} to rename on next login?`)) {
      renameCharacterMutation.mutate(character.name)
    }
    setShowActionsMenu(false)
  }

  const handleCustomize = () => {
    if (confirm(`Force ${character.name} to customize appearance on next login?`)) {
      customizeCharacterMutation.mutate(character.name)
    }
    setShowActionsMenu(false)
  }

  return (
    <>
      <div className="border border-gray-200 rounded-lg p-4 hover:border-blue-300 transition-colors">
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <div className="flex items-center gap-2 mb-2">
              <h4 className="font-semibold text-lg">{character.name}</h4>
              {character.online && (
                <span className="px-2 py-0.5 bg-green-100 text-green-700 text-xs rounded-full">
                  Online
                </span>
              )}
            </div>
            <div className="text-sm text-gray-600 space-y-1">
              <p>Level {character.level} {RACES[character.race] || 'Unknown'} {CLASSES[character.class] || 'Unknown'}</p>
              <p className="text-xs text-gray-500">
                Gold: {Math.floor(character.money / 10000)}g {Math.floor((character.money % 10000) / 100)}s {character.money % 100}c
              </p>
            </div>
          </div>
          <div className="relative">
            <button
              onClick={() => setShowActionsMenu(!showActionsMenu)}
              className="p-1 hover:bg-gray-100 rounded"
            >
              <MoreVertical className="w-5 h-5 text-gray-500" />
            </button>
            {showActionsMenu && (
              <div className="absolute right-0 mt-1 w-56 bg-white border border-gray-200 rounded-lg shadow-lg z-10">
                <div className="py-1">
                  <button
                    onClick={() => { setActiveDialog('message'); setShowActionsMenu(false) }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-50 flex items-center gap-2"
                  >
                    <MessageSquare className="w-4 h-4" />
                    Send Message
                  </button>
                  <button
                    onClick={() => { setActiveDialog('items'); setShowActionsMenu(false) }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-50 flex items-center gap-2"
                  >
                    <Package className="w-4 h-4" />
                    Send Items
                  </button>
                  <button
                    onClick={() => { setActiveDialog('money'); setShowActionsMenu(false) }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-50 flex items-center gap-2"
                  >
                    <Coins className="w-4 h-4" />
                    Send Gold
                  </button>
                  <button
                    onClick={() => { setActiveDialog('level'); setShowActionsMenu(false) }}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-50 flex items-center gap-2"
                  >
                    <TrendingUp className="w-4 h-4" />
                    Set Level
                  </button>
                  <div className="border-t border-gray-200 my-1"></div>
                  <button
                    onClick={handleRename}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-50 flex items-center gap-2"
                  >
                    <Edit className="w-4 h-4" />
                    Force Rename
                  </button>
                  <button
                    onClick={handleCustomize}
                    className="w-full text-left px-4 py-2 text-sm hover:bg-gray-50 flex items-center gap-2"
                  >
                    <Palette className="w-4 h-4" />
                    Force Customize
                  </button>
                  {character.online && (
                    <>
                      <div className="border-t border-gray-200 my-1"></div>
                      <button
                        onClick={handleKick}
                        className="w-full text-left px-4 py-2 text-sm hover:bg-red-50 text-red-600 flex items-center gap-2"
                      >
                        <UserX className="w-4 h-4" />
                        Kick Player
                      </button>
                    </>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      </div>

      {activeDialog === 'message' && (
        <SendMessageDialog
          characterName={character.name}
          onClose={() => setActiveDialog(null)}
          onSubmit={handleSendMessage}
        />
      )}
      {activeDialog === 'items' && (
        <SendItemsDialog
          characterName={character.name}
          onClose={() => setActiveDialog(null)}
          onSubmit={handleSendItems}
        />
      )}
      {activeDialog === 'money' && (
        <SendMoneyDialog
          characterName={character.name}
          onClose={() => setActiveDialog(null)}
          onSubmit={handleSendMoney}
        />
      )}
      {activeDialog === 'level' && (
        <SetLevelDialog
          characterName={character.name}
          currentLevel={character.level}
          onClose={() => setActiveDialog(null)}
          onSubmit={handleSetLevel}
        />
      )}
    </>
  )
}
