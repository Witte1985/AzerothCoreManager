# Player / Character Management — Implementation Plan

## Goal

Implement a full **Characters tab** in `StackDetailsPage` (next to the existing Accounts tab) that lets admins browse all characters across every account, inspect their inventory/equipment/bank, see how much gold they have, and perform a rich set of management actions — bans, mutes, revives, economy edits, item delivery, and more.

Additionally, the existing **AccountDetailsPanel** must replace its "Character details coming soon" placeholder with the real character list, sharing the same reusable components.

---

## Current State

| Layer | What exists |
|---|---|
| Backend controller | `CharactersController` — GetAll, SendMessage, SendItems, SendMoney, Kick, Rename, Customize, SetLevel |
| Backend service | `AccountManagementService` — MySQL reads for GetCharacters/GetAllCharacters; SOAP for the actions above |
| Frontend hooks | `useCharacters`, character mutation hooks in `useAccounts.ts` |
| Frontend components | `CharacterCard` with an actions dropdown (send message/items/gold, set level, force rename/customize, kick); `AccountDetailsPanel` showing "Character details coming soon" |
| StackDetailsPage tabs | `overview \| accounts \| logs` |
| **Missing** | Inventory/equipment/bank read endpoints; Ban/Mute/Freeze/Revive/RepairGear/MaxSkills/ModifyMoney/AddHonor/AddArenaPoints/AddItem actions; Characters tab; Character detail panel |

---

## AzerothCore Commands Reference

The following SOAP commands drive all new character actions:

| Feature | SOAP Command |
|---|---|
| Ban character | `ban character {name} {duration} {reason}` |
| Unban character | `unban character {name}` |
| Ban account by character | `ban playeraccount {name} {duration} {reason}` |
| Mute player | `mute {name} {minutes} {reason}` |
| Freeze player | `freeze {name}` |
| Revive player | `revive {name}` |
| Kill player | `die` (target-based; use with character name) |
| Repair gear | `gear repair` (targeted — use `.gear repair` via SOAP with character in target) |
| Max all skills | `maxskill` |
| Add/remove money | `modify money {amount}` (negative removes) |
| Add honor points | `honor add {amount}` |
| Add arena points | `modify arenapoints {amount}` |
| Add item to inventory | `additem {name} {itemId} {count}` |
| Set level | `character level {name} {level}` *(already exists)* |
| Force rename | `character rename {name}` *(already exists)* |
| Force customize | `character customize {name}` *(already exists)* |
| Check bag | `character check bag {name} {bagSlot}` |
| Check bank | `character check bank` |
| Show professions | `character check profession {name}` |
| Show reputation | `character reputation {name}` |
| Modify reputation | `modify reputation {factionId} {value}` |
| Add achievement | `achievement add {achievementId}` |
| Remove cooldowns | `cooldown` |
| Send whisper message | `send message {name} {text}` *(via announce/notify)* |

---

## Database Schema (MySQL)

### Inventory read (`acore_characters`)

```sql
SELECT
    ci.bag,
    ci.slot,
    ci.item              AS item_guid,
    ii.itemEntry,
    it.name              AS item_name,
    it.displayid,
    it.Quality,
    it.ItemLevel,
    it.RequiredLevel,
    it.class             AS item_class,
    it.subclass          AS item_subclass,
    ii.stackCount,
    ii.durability
FROM character_inventory ci
JOIN item_instance ii          ON ci.item = ii.guid
JOIN acore_world.item_template it ON ii.itemEntry = it.entry
WHERE ci.guid = @CharacterGuid
ORDER BY ci.bag, ci.slot
```

### Slot ranges (bag = 0 means "on character")

| Range | Meaning |
|---|---|
| slot 0–18 | Equipped gear (head, neck, shoulder, etc.) |
| slot 19–22 | Equipped bag containers |
| slot 23–38 | Backpack (16 slots) |
| slot 39–66 | Bank main storage (28 slots) |
| slot 67–74 | Bank bag containers |
| bag = item_guid of container | Contents of that bag/bank bag |

### Equipment slot mapping

| Slot | Name | Slot | Name |
|---|---|---|---|
| 0 | Head | 10 | Finger 1 |
| 1 | Neck | 11 | Finger 2 |
| 2 | Shoulders | 12 | Trinket 1 |
| 3 | Shirt | 13 | Trinket 2 |
| 4 | Chest | 14 | Back |
| 5 | Waist | 15 | Main Hand |
| 6 | Legs | 16 | Off Hand |
| 7 | Feet | 17 | Ranged / Relic |
| 8 | Wrists | 18 | Tabard |
| 9 | Hands | — | — |

### Gold

The `characters.money` column holds copper (1 gold = 10 000 copper). Already in `CharacterDto` via the frontend type but **not yet returned by the backend query** — needs to be added to the MySQL SELECT.

---

## Implementation Phases

---

### Phase 1 — Backend: New DTOs

**File:** `backend/AzerothCoreManager.Core/Contracts/CharacterInventoryDto.cs`

```
CharacterInventoryDto
  EquippedItems   : List<ItemSlotDto>   // slots 0–18
  BackpackItems   : List<ItemSlotDto>   // slots 23–38 (bag=0)
  BagItems        : List<BagDto>        // 4 equipped bags with their contents
  BankItems       : List<ItemSlotDto>   // slots 39–66 (bag=0)
  BankBagItems    : List<BagDto>        // 7 bank bags with their contents

ItemSlotDto
  Slot            : int
  Bag             : int
  ItemGuid        : int
  ItemEntry       : int
  ItemName        : string
  DisplayId       : int
  Quality         : int     // 0=Poor … 6=Artifact
  ItemLevel       : int
  RequiredLevel   : int
  StackCount      : int
  Durability      : int

BagDto
  Slot            : int     // slot of the container (19–22 or 67–74)
  ContainerGuid   : int
  Items           : List<ItemSlotDto>
```

**Extend `CharacterDto`** with:
- `Money` (long, copper) — already in the MySQL `characters` table
- `Zone` (int) — zone ID from `characters.zone`
- `Guild` (string?) — optional, from `guild_member` + `guild` join

**New request/response DTOs in `CharacterDto.cs`:**
- `BanCharacterRequest` — `Duration`, `Reason`
- `MuteCharacterRequest` — `Minutes`, `Reason`
- `ModifyMoneyRequest` — `CopperAmount` (positive = add, negative = remove)
- `AddHonorRequest` — `Amount`
- `AddArenaPointsRequest` — `Amount`
- `AddItemRequest` — `ItemId`, `Count`
- `CharacterBanInfoDto` — `IsBanned`, `BanExpiry`, `BanReason`, `BannedBy`

---

### Phase 2 — Backend: Service Interface & Implementation

**File:** `IAccountManagementService.cs` — add:

```csharp
// Data queries
Task<CharacterInventoryDto> GetCharacterInventoryAsync(string stackId, int characterGuid, CancellationToken ct = default);

// Character moderation
Task<bool> BanCharacterAsync(string stackId, string name, string duration, string reason, CancellationToken ct = default);
Task<bool> UnbanCharacterAsync(string stackId, string name, CancellationToken ct = default);
Task<bool> MuteCharacterAsync(string stackId, string name, int minutes, string reason, CancellationToken ct = default);
Task<bool> FreezeCharacterAsync(string stackId, string name, CancellationToken ct = default);
Task<bool> ReviveCharacterAsync(string stackId, string name, CancellationToken ct = default);

// Utility
Task<bool> RepairGearAsync(string stackId, string name, CancellationToken ct = default);
Task<bool> MaxSkillsAsync(string stackId, string name, CancellationToken ct = default);

// Economy
Task<bool> ModifyMoneyAsync(string stackId, string name, long copperAmount, CancellationToken ct = default);
Task<bool> AddHonorAsync(string stackId, string name, int amount, CancellationToken ct = default);
Task<bool> AddArenaPointsAsync(string stackId, string name, int amount, CancellationToken ct = default);

// Items
Task<bool> AddItemAsync(string stackId, string name, int itemId, int count, CancellationToken ct = default);
```

**File:** `AccountManagementService.cs` — implement all above:
- Inventory: MySQL query across `character_inventory`, `item_instance`, `acore_world.item_template`; group results into `CharacterInventoryDto` by slot ranges
- SOAP actions: follow existing patterns (build command string → `_soapProxy.ExecuteCommandAsync` → check response keywords)

Also fix the existing `GetCharactersAsync` / `GetAllCharactersAsync` queries to include `money`, `zone`, and an optional `guild_name` column.

---

### Phase 3 — Backend: New Controller Endpoints

**File:** `CharactersController.cs` — add:

```
GET    /api/stacks/{stackId}/characters/{guid}/inventory
POST   /api/stacks/{stackId}/characters/{name}/ban
DELETE /api/stacks/{stackId}/characters/{name}/ban        (unban)
POST   /api/stacks/{stackId}/characters/{name}/mute
POST   /api/stacks/{stackId}/characters/{name}/freeze
POST   /api/stacks/{stackId}/characters/{name}/revive
POST   /api/stacks/{stackId}/characters/{name}/repair-gear
POST   /api/stacks/{stackId}/characters/{name}/max-skills
POST   /api/stacks/{stackId}/characters/{name}/modify-money
POST   /api/stacks/{stackId}/characters/{name}/add-honor
POST   /api/stacks/{stackId}/characters/{name}/add-arena-points
POST   /api/stacks/{stackId}/characters/{name}/add-item
```

Follow the existing pattern: delegate to `IAccountManagementService`, return `200 OK` with `{ success, message }` or `400 Bad Request`.

---

### Phase 4 — Frontend: Types & API Layer

**New file:** `frontend/src/types/character.types.ts`

```typescript
export interface ItemSlotDto { slot, bag, itemGuid, itemEntry, itemName, displayId, quality, itemLevel, requiredLevel, stackCount, durability }
export interface BagDto { slot, containerGuid, items: ItemSlotDto[] }
export interface CharacterInventoryDto { equippedItems, backpackItems, bagItems, bankItems, bankBagItems }
export interface BanCharacterRequest { duration, reason }
export interface MuteCharacterRequest { minutes, reason }
export interface ModifyMoneyRequest { copperAmount }
export interface AddHonorRequest { amount }
export interface AddArenaPointsRequest { amount }
export interface AddItemRequest { itemId, count }
```

Also extend `CharacterDto` in `account.types.ts`:
```typescript
money: number        // copper
zone: number
guild?: string
```

**Update `accountApi.ts`** — add to `characterApi`:

```typescript
getInventory:     (stackId, guid)              → GET  /{guid}/inventory
ban:              (stackId, name, request)     → POST /{name}/ban
unban:            (stackId, name)              → DELETE /{name}/ban
mute:             (stackId, name, request)     → POST /{name}/mute
freeze:           (stackId, name)              → POST /{name}/freeze
revive:           (stackId, name)              → POST /{name}/revive
repairGear:       (stackId, name)              → POST /{name}/repair-gear
maxSkills:        (stackId, name)              → POST /{name}/max-skills
modifyMoney:      (stackId, name, request)     → POST /{name}/modify-money
addHonor:         (stackId, name, request)     → POST /{name}/add-honor
addArenaPoints:   (stackId, name, request)     → POST /{name}/add-arena-points
addItem:          (stackId, name, request)     → POST /{name}/add-item
```

**New/updated hook file:** `hooks/useCharacters.ts`

```typescript
export function useAllCharacters(stackId)            // polls every 5s
export function useCharacterInventory(stackId, guid) // fetches on demand
export function useBanCharacter(stackId)
export function useUnbanCharacter(stackId)
export function useMuteCharacter(stackId)
export function useFreezeCharacter(stackId)
export function useReviveCharacter(stackId)
export function useRepairGear(stackId)
export function useMaxSkills(stackId)
export function useModifyMoney(stackId)
export function useAddHonor(stackId)
export function useAddArenaPoints(stackId)
export function useAddItem(stackId)
```

---

### Phase 5 — Frontend: New Dialog Components

**Directory:** `frontend/src/components/characters/dialogs/`

| File | Purpose |
|---|---|
| `BanCharacterDialog.tsx` | Duration dropdown (30m/1h/12h/1d/7d/30d/Permanent) + reason input |
| `MuteCharacterDialog.tsx` | Minutes input + reason input |
| `ModifyMoneyDialog.tsx` | Gold/silver/copper input fields; computes copper total |
| `AddHonorDialog.tsx` | Amount input |
| `AddArenaPointsDialog.tsx` | Amount input |
| `AddItemDialog.tsx` | Item ID + count inputs |

All dialogs follow the existing pattern (modal overlay, form with React Hook Form + Zod, submit → mutation → close on success).

---

### Phase 6 — Frontend: Character Detail Panel

**New file:** `frontend/src/components/characters/CharacterDetailPanel.tsx`

A reusable panel component with the following sub-tabs:

#### Info tab
- Character name, online badge
- Level, Race (display name), Class (display name), Gender
- Guild (if any)
- Zone / Map (display name or ID)
- Total playtime (formatted as Xd Xh Xm)
- Gold — displayed as `{gold}g {silver}s {copper}c` in yellow/gold color

#### Equipment tab
- Render a 3-column WoW-style paper doll layout:
  - Left column (top-to-bottom): Head, Neck, Shoulders, Back, Chest, Shirt, Tabard, Wrists
  - Center column: Static character silhouette placeholder image
  - Right column (top-to-bottom): Hands, Finger 1, Finger 2, Trinket 1, Trinket 2, Waist, Legs, Feet
  - Below center: Main Hand, Off Hand, Ranged
- Each slot is a rounded box showing item name (colored by quality) or "Empty"
- Quality colors: Poor=gray, Common=white, Uncommon=green, Rare=blue, Epic=purple, Legendary=orange

#### Inventory tab
- Backpack section (16 slots in 4-column grid)
- Bag 1–4 sections (each shows bag name + its item grid)
- Items shown as: name (quality color), stack count if > 1

#### Bank tab
- Bank main (28 slots)
- Bank bags 1–7
- Same style as Inventory tab

#### Actions tab
Organized into groups with clear section headers:

**Moderation**
- 🔨 Ban Character → `BanCharacterDialog`
- ✅ Unban Character (shown if banned)
- 🔇 Mute Player → `MuteCharacterDialog`
- 🧊 Freeze Player (one-click)
- 👟 Kick Player → existing `KickPlayerRequest`

**Restoration / Utility**
- 💊 Revive Player (one-click)
- 🔧 Repair All Gear (one-click)
- ⬆️ Max All Skills (one-click)

**Economy**
- 💰 Modify Gold → `ModifyMoneyDialog`
- ⚔️ Add Honor → `AddHonorDialog`
- 🏆 Add Arena Points → `AddArenaPointsDialog`

**Items & Mail**
- 📦 Add Item Directly → `AddItemDialog`
- 📮 Send Items (mail) → existing `SendItemsDialog`
- 💰 Send Money (mail) → existing `SendMoneyDialog`
- ✉️ Send Message → existing `SendMessageDialog`

**Character**
- 📊 Set Level → existing `SetLevelDialog`
- ✏️ Force Rename (one-click confirm)
- 🎨 Force Customize (one-click confirm)

**Props:**
```typescript
interface CharacterDetailPanelProps {
  character: CharacterDto
  stackId: string
  onClose: () => void
}
```

---

### Phase 7 — Frontend: Characters Tab

**New file:** `frontend/src/components/characters/CharactersTab.tsx`

Layout mirrors `AccountsTab`:
- Left half: filterable, searchable character list
  - Search by name
  - Toggle: Online only
  - Filter: Race dropdown, Class dropdown
  - Each row: online indicator • name • level • race • class • guild • account username
- Right half: `CharacterDetailPanel` when a character is selected, else placeholder

**Props:** `{ stackId: string }`

Uses `useAllCharacters(stackId)` (with 5s auto-refresh).

---

### Phase 8 — Frontend: Wire Up Characters Tab in StackDetailsPage

**File:** `frontend/src/pages/StackDetailsPage.tsx`

1. Add `'characters'` to the active tab union type:
   ```typescript
   const [activeTab, setActiveTab] = useState<'overview' | 'accounts' | 'characters' | 'logs'>('overview')
   ```

2. Add tab button between Accounts and Logs:
   ```tsx
   <button onClick={() => setActiveTab('characters')} className={tabClass('characters')}>
     <Users className="w-4 h-4" />
     Characters
   </button>
   ```

3. Render `<CharactersTab stackId={stackId!} />` when `activeTab === 'characters'`.

---

### Phase 9 — Frontend: Enhance AccountDetailsPanel

**File:** `frontend/src/components/accounts/AccountDetailsPanel.tsx`

Replace the "Character details coming soon" block with:
- `useCharacters(stackId, account.id)` query to load that account's characters
- A compact character list (using `CharacterCard` or a simpler inline version)
- Clicking a character expands `CharacterDetailPanel` inline (slide-down / accordion) or opens it in a right-side panel

This makes account → character navigation seamless without leaving the Accounts tab.

---

## Component Reuse Map

```
StackDetailsPage
├── AccountsTab
│   └── AccountDetailsPanel
│       └── CharacterCard  (existing)
│       └── CharacterDetailPanel  ← NEW (reused here)
│           ├── ItemSlot display
│           └── dialogs/...
└── CharactersTab  ← NEW
    └── CharacterDetailPanel  ← SAME component
```

---

## File Inventory

### New backend files
| File | Description |
|---|---|
| `Core/Contracts/CharacterInventoryDto.cs` | `CharacterInventoryDto`, `ItemSlotDto`, `BagDto` + new request DTOs |

### Modified backend files
| File | Change |
|---|---|
| `Core/Contracts/CharacterDto.cs` | Add `Money`, `Zone`, `Guild` fields + new request records |
| `Core/Services/Interfaces/IAccountManagementService.cs` | Add 12 new method signatures |
| `Infrastructure/Services/AccountManagementService.cs` | Implement all new methods; fix MySQL queries to return money/zone/guild |
| `Api/Controllers/CharactersController.cs` | Add 12 new endpoints |

### New frontend files
| File | Description |
|---|---|
| `src/types/character.types.ts` | All inventory/action DTOs |
| `src/hooks/useCharacters.ts` | All character-specific query/mutation hooks |
| `src/components/characters/CharactersTab.tsx` | Characters tab with list + detail panel |
| `src/components/characters/CharacterDetailPanel.tsx` | Full character detail with sub-tabs |
| `src/components/characters/dialogs/BanCharacterDialog.tsx` | Ban dialog |
| `src/components/characters/dialogs/MuteCharacterDialog.tsx` | Mute dialog |
| `src/components/characters/dialogs/ModifyMoneyDialog.tsx` | Modify money dialog |
| `src/components/characters/dialogs/AddHonorDialog.tsx` | Add honor dialog |
| `src/components/characters/dialogs/AddArenaPointsDialog.tsx` | Add arena points dialog |
| `src/components/characters/dialogs/AddItemDialog.tsx` | Add item dialog |

### Modified frontend files
| File | Change |
|---|---|
| `src/types/account.types.ts` | Extend `CharacterDto` with `money`, `zone`, `guild` |
| `src/services/accountApi.ts` | Add 12 new `characterApi` methods |
| `src/pages/StackDetailsPage.tsx` | Add Characters tab |
| `src/components/accounts/AccountDetailsPanel.tsx` | Replace placeholder with character list + `CharacterDetailPanel` |

---

## Notes & Decisions

- **Inventory data is read-only** from MySQL — no write path needed. Actions that modify inventory use SOAP.
- **Item icons**: WoW client item icons are not bundled. The quality-colored item name is sufficient for an admin tool. Icon support can be added later via a WoW icon CDN.
- **SOAP targeting**: Some SOAP commands require the player to be online (e.g., freeze, revive, repair). The UI should display a warning or disable those buttons when the character is offline.
- **Character ban vs account ban**: Both are supported. Character ban uses `ban character {name}`; account ban (by character) uses `ban playeraccount {name}`. The UI in `CharacterDetailPanel` will offer "Ban Character" and the account-level ban remains in `AccountDetailsPanel`.
- **Zone names**: Zone IDs can be looked up from a static map in the frontend (a record of common zone IDs → display names). Full DBC lookup is out of scope.
- **Existing dialogs reuse**: `SendMessageDialog`, `SendItemsDialog`, `SendMoneyDialog`, `SetLevelDialog` (in `components/accounts/dialogs/`) are already fully functional and will be imported directly into `CharacterDetailPanel`.
