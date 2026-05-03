# Account Management UI Implementation

## Overview

This document describes the newly implemented frontend UI for account and character management in AzerothCoreManager.

## Features Implemented

### 1. Tabbed Interface in Stack Details Page

The Stack Details page now has three tabs:
- **Overview** - Stack status, containers, configuration (existing content)
- **Accounts** - Account and character management (new)
- **Logs** - Container logs with direct links

### 2. Accounts Tab

Located at: `frontend/src/components/accounts/AccountsTab.tsx`

Features:
- List all accounts in a table view
- Real-time status indicators (🟢 Online / ⚫ Offline)
- Search/filter accounts by username
- GM level badges (Mod, GM, Admin)
- Character count for each account
- Auto-refresh every 5 seconds
- Click account to view details panel

### 3. Account Details Panel

Located at: `frontend/src/components/accounts/AccountDetailsPanel.tsx`

Features:
- Account information display (ID, IP, last login, character count)
- Ban information display (if banned)
- GM Level control with dropdown (0-3)
- Account actions:
  - Ban/Unban account
  - Reset password
  - Delete account (with confirmation)
- Character list with interactive cards

### 4. Character Card

Located at: `frontend/src/components/accounts/CharacterCard.tsx`

Features:
- Character info: Name, Level, Race, Class
- Online status indicator
- Gold/Silver/Copper display
- Actions menu with:
  - 📨 Send Message
  - 📦 Send Items
  - 💰 Send Gold
  - 🎚️ Set Level
  - 🔄 Force Rename
  - 🎨 Force Customize
  - ❌ Kick Player (only if online)

### 5. Dialog Components

All located at: `frontend/src/components/accounts/dialogs/`

- **CreateAccountDialog** - Create new account with username/password
- **BanAccountDialog** - Ban account with duration (30m to permanent) and reason
- **SetPasswordDialog** - Reset account password
- **DeleteAccountDialog** - Delete account with confirmation
- **SendMessageDialog** - Send in-game mail with subject and body
- **SendItemsDialog** - Send items via mail with item ID and count
- **SendMoneyDialog** - Send gold via mail with gold/silver/copper inputs
- **SetLevelDialog** - Set character level (1-80)

All dialogs include:
- Form validation
- Loading states
- Error handling
- Success feedback

## API Integration

### Account API Client

Located at: `frontend/src/services/accountApi.ts`

Implements all 15 backend endpoints:
- 8 account endpoints (list, create, ban, unban, delete, set GM level, set password, unban IP)
- 7 character endpoints (send message, send items, send money, kick, rename, customize, set level)

### React Query Hooks

Located at: `frontend/src/hooks/useAccounts.ts`

Provides hooks for:
- Query hooks with auto-refresh
- Mutation hooks with cache invalidation
- Optimistic updates
- Error handling

### TypeScript Types

Located at: `frontend/src/types/account.types.ts`

Mirrors backend DTOs:
- `AccountDto`
- `CharacterDto`
- All request/response types

## Usage

### For Users

1. Navigate to a stack's details page
2. Click the "Accounts" tab
3. Click "+ Create Account" to create a new account
4. Search for accounts using the search box
5. Click an account row to view details
6. Use the actions to manage accounts and characters

### For Developers

#### Starting the Application

```bash
# Terminal 1: Backend
cd backend
dotnet watch --project AzerothCoreManager.Api

# Terminal 2: Frontend
cd frontend
npm run dev
```

#### Adding New Features

1. Add API endpoint in backend
2. Add type in `frontend/src/types/account.types.ts`
3. Add API function in `frontend/src/services/accountApi.ts`
4. Add React Query hook in `frontend/src/hooks/useAccounts.ts`
5. Use hook in components

## Technical Notes

### Race and Class Mappings

Located in `CharacterCard.tsx`:
- Races: 1-11 (Human, Orc, Dwarf, etc.)
- Classes: 1-11 (Warrior, Paladin, Hunter, etc.)

### Money Conversion

- 1 Gold = 10,000 Copper
- 1 Silver = 100 Copper
- Backend expects copper amounts
- Frontend converts user input to copper

### GM Levels

- 0: Player
- 1: Moderator
- 2: Game Master
- 3: Administrator
- realmId: -1 (applies to all realms)

### Ban Durations

- "30m" - 30 minutes
- "1h" - 1 hour
- "12h" - 12 hours
- "1d" - 1 day
- "7d" - 7 days
- "30d" - 30 days
- "-1" - Permanent

## Testing

### Manual Testing Checklist

- [ ] Create account
- [ ] View account list
- [ ] Search accounts
- [ ] Select account to view details
- [ ] Set GM level
- [ ] Ban account
- [ ] Unban account
- [ ] Reset password
- [ ] Delete account
- [ ] Send message to character
- [ ] Send items to character
- [ ] Send gold to character
- [ ] Set character level
- [ ] Kick online player
- [ ] Force character rename
- [ ] Force character customize

### Error Scenarios to Test

- Try to create account with existing username
- Try to set invalid level (< 1 or > 80)
- Try character actions when server is offline
- Network errors
- Validation errors

## Future Enhancements

Potential improvements:
- Bulk account operations
- Advanced filtering (by GM level, online status, ban status)
- Account activity log
- Character inventory viewer
- Mail history
- IP ban management UI
- Export account list to CSV
- Account statistics dashboard

## Files Created

1. `frontend/src/types/account.types.ts` - TypeScript types
2. `frontend/src/services/accountApi.ts` - API client
3. `frontend/src/hooks/useAccounts.ts` - React Query hooks
4. `frontend/src/components/accounts/AccountsTab.tsx` - Main tab component
5. `frontend/src/components/accounts/AccountDetailsPanel.tsx` - Details panel
6. `frontend/src/components/accounts/CharacterCard.tsx` - Character card
7. `frontend/src/components/accounts/dialogs/CreateAccountDialog.tsx`
8. `frontend/src/components/accounts/dialogs/BanAccountDialog.tsx`
9. `frontend/src/components/accounts/dialogs/SetPasswordDialog.tsx`
10. `frontend/src/components/accounts/dialogs/DeleteAccountDialog.tsx`
11. `frontend/src/components/accounts/dialogs/SendMessageDialog.tsx`
12. `frontend/src/components/accounts/dialogs/SendItemsDialog.tsx`
13. `frontend/src/components/accounts/dialogs/SendMoneyDialog.tsx`
14. `frontend/src/components/accounts/dialogs/SetLevelDialog.tsx`

## Files Modified

1. `frontend/src/pages/StackDetailsPage.tsx` - Added tabs navigation

## Build Status

✅ Frontend build: Success (no TypeScript errors)
✅ Backend build: Success

## Dependencies

No new dependencies were added. The implementation uses existing libraries:
- React 19
- TailwindCSS v4
- React Query
- React Router
- Axios
- Lucide React (icons)

