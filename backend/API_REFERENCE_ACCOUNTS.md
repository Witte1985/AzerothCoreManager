# Account Management API Reference

## Base URL
All endpoints are relative to: `http://localhost:5000/api/stacks/{stackId}`

## Account Endpoints

### List Accounts
**GET** `/accounts`

Returns all accounts for the specified stack with online status and character counts.

**Response:**
```json
[
  {
    "id": 1,
    "username": "admin",
    "gmLevel": 3,
    "lastLogin": "2026-05-03T12:00:00Z",
    "characterCount": 2,
    "isOnline": true
  }
]
```

### Get Characters for Account
**GET** `/accounts/{accountId}/characters`

Returns all characters belonging to the specified account.

**Response:**
```json
[
  {
    "guid": 1,
    "name": "Paladin",
    "account": 1,
    "level": 80,
    "race": 1,
    "class": 2,
    "gender": 0,
    "online": true,
    "totalTime": 3600,
    "map": 0,
    "positionX": 100.0,
    "positionY": 200.0,
    "positionZ": 50.0
  }
]
```

### Create Account
**POST** `/accounts`

Creates a new game account.

**Request Body:**
```json
{
  "username": "newplayer",
  "password": "securePassword123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Account 'newplayer' created successfully"
}
```

### Set GM Level
**POST** `/accounts/{accountId}/set-gm-level`

Sets the GM level for an account (0 = player, 1-3 = GM levels).

**Request Body:**
```json
{
  "username": "admin",
  "level": 3,
  "realmId": -1
}
```

**Response:**
```json
{
  "success": true,
  "message": "GM level set to 3 for account 'admin'"
}
```

### Ban Account
**POST** `/accounts/{accountId}/ban`

Bans an account for a specified duration.

**Request Body:**
```json
{
  "username": "badplayer",
  "duration": "30m",
  "reason": "Spamming chat"
}
```

**Duration formats:**
- `30s` - 30 seconds
- `30m` - 30 minutes
- `1h` - 1 hour
- `1d` - 1 day
- `permanent` - Permanent ban

**Response:**
```json
{
  "success": true,
  "message": "Account 'badplayer' banned for 30m"
}
```

## Character Endpoints

### Send Message
**POST** `/characters/{characterName}/send-message`

Sends an in-game message to a character (must be online).

**Request Body:**
```json
{
  "message": "Welcome to the server!"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Message sent to 'Paladin'"
}
```

### Send Items
**POST** `/characters/{characterName}/send-items`

Sends items to a character via in-game mail.

**Request Body:**
```json
{
  "itemId": 25,
  "count": 10
}
```

**Response:**
```json
{
  "success": true,
  "message": "Items sent to 'Paladin' via mail"
}
```

### Send Money
**POST** `/characters/{characterName}/send-money`

Sends gold to a character via in-game mail.

**Request Body:**
```json
{
  "copperAmount": 100000
}
```

**Copper conversion:**
- 1 gold = 10,000 copper
- 1 silver = 100 copper

**Response:**
```json
{
  "success": true,
  "message": "Money sent to 'Paladin' via mail: 10g 0s 0c"
}
```

### Kick Player
**POST** `/characters/{characterName}/kick`

Kicks a player from the server (must be online).

**Request Body:**
```json
{
  "reason": "Server maintenance"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Player 'Paladin' has been kicked"
}
```

## Error Responses

All endpoints return standard HTTP status codes:

**400 Bad Request** - Invalid input
```json
{
  "error": "Username is required"
}
```

**404 Not Found** - Stack not found
```json
{
  "error": "Stack 'abc123' not found"
}
```

**500 Internal Server Error** - Server error
```json
{
  "error": "Failed to connect to worldserver SOAP interface: Connection refused"
}
```

## Notes

1. **Stack must be running** - Most operations require the worldserver to be running
2. **SOAP credentials** - Configured per-stack in Advanced settings (default: admin/admin)
3. **MySQL access** - Read operations query the MySQL database directly
4. **Write operations** - Use SOAP commands for modifications
5. **Character names** - Case-sensitive in most operations
6. **Online status** - Updated in real-time from database queries

## Testing with cURL

**List accounts:**
```bash
curl http://localhost:5000/api/stacks/my-stack/accounts
```

**Create account:**
```bash
curl -X POST http://localhost:5000/api/stacks/my-stack/accounts \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpass"}'
```

**Send message:**
```bash
curl -X POST http://localhost:5000/api/stacks/my-stack/characters/Paladin/send-message \
  -H "Content-Type: application/json" \
  -d '{"message":"Hello from API!"}'
```

## Common Item IDs

For testing `send-items`:
- 2589 - Linen Cloth
- 25 - Worn Shortbow
- 6948 - Hearthstone
- 40582 - Scepter of the Nathrezim (legendary)

## Common Use Cases

### Create Admin Account
```bash
# 1. Create account
curl -X POST http://localhost:5000/api/stacks/my-stack/accounts \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# 2. Set GM level to 3
curl -X POST http://localhost:5000/api/stacks/my-stack/accounts/2/set-gm-level \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","level":3,"realmId":-1}'
```

### Welcome New Player
```bash
# Send welcome message
curl -X POST http://localhost:5000/api/stacks/my-stack/characters/NewPlayer/send-message \
  -H "Content-Type: application/json" \
  -d '{"message":"Welcome to our server! Type .help for commands"}'

# Send starter gold (10 gold)
curl -X POST http://localhost:5000/api/stacks/my-stack/characters/NewPlayer/send-money \
  -H "Content-Type: application/json" \
  -d '{"copperAmount":100000}'
```
