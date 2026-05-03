# Account Management Backend Implementation

## Overview

The account and character management backend has been successfully implemented with full SOAP integration and MySQL queries according to the plan in `/home/witte/Projects/AzerothCoreManager/plan/ACCOUNT_MANAGEMENT_PLAN.md`.

## Implemented Components

### Phase 1: SOAP Proxy Service ✓

**Files Created:**
- `Core/Services/Interfaces/ISoapProxyService.cs`
- `Core/Contracts/SoapCommandDto.cs`
- `Infrastructure/Services/SoapProxyService.cs`

**Features:**
- SOAP XML envelope construction with XML escaping
- HTTP client integration via `IHttpClientFactory`
- SOAP response parsing
- Stack-specific SOAP credentials (username/password)
- Error handling and logging

### Phase 2: Account Management Service ✓

**Files Created:**
- `Core/Services/Interfaces/IAccountManagementService.cs`
- `Core/Contracts/AccountDto.cs`
- `Core/Contracts/CharacterDto.cs`
- `Infrastructure/Services/AccountManagementService.cs`

**Features:**
- **MySQL Queries:**
  - List all accounts with online status, GM level, character count
  - List characters for an account with full details
  
- **SOAP Commands:**
  - Create account
  - Set GM level
  - Ban account
  - Ban IP
  - Send message to character
  - Send items via mail
  - Send money via mail
  - Kick player

### Phase 3: API Controllers ✓

**Files Created:**
- `Api/Controllers/AccountsController.cs`
- `Api/Controllers/CharactersController.cs`

**Endpoints:**

#### AccountsController
- `GET /api/stacks/{stackId}/accounts` - List all accounts
- `GET /api/stacks/{stackId}/accounts/{accountId}/characters` - List characters for account
- `POST /api/stacks/{stackId}/accounts` - Create new account
- `POST /api/stacks/{stackId}/accounts/{accountId}/set-gm-level` - Set GM level
- `POST /api/stacks/{stackId}/accounts/{accountId}/ban` - Ban account

#### CharactersController
- `POST /api/stacks/{stackId}/characters/{characterName}/send-message` - Send in-game message
- `POST /api/stacks/{stackId}/characters/{characterName}/send-items` - Send items via mail
- `POST /api/stacks/{stackId}/characters/{characterName}/send-money` - Send gold via mail
- `POST /api/stacks/{stackId}/characters/{characterName}/kick` - Kick player from server

### Phase 4: MySQL Connection Factory ✓

**Files Created:**
- `Core/Services/Interfaces/IMySqlConnectionFactory.cs`
- `Infrastructure/Data/MySqlConnectionFactory.cs`

**Features:**
- Create connections to `acore_auth`, `acore_world`, or `acore_characters` databases
- Stack-specific MySQL connection (container name, port, password)
- Connection pooling via `MySqlConnection`

## Infrastructure Changes

### NuGet Packages Added
- `MySql.Data` (v9.2.0) - MySQL connector
- `Dapper` (v2.1.44) - Micro-ORM for queries

### Configuration Updates

**AdvancedConfigDto.cs:**
```csharp
public string SoapUsername { get; set; } = "admin";
public string SoapPassword { get; set; } = "admin";
```

**ManagedStackEntity.cs:**
```csharp
public string SoapUsername { get; set; } = "admin";
public string SoapPassword { get; set; } = "admin";
```

**Database Migration:**
- Migration: `AddSoapCredentials`
- Adds `SoapUsername` and `SoapPassword` columns to ManagedStacks table

### Service Registration

**DependencyInjection.cs:**
```csharp
services.AddScoped<IMySqlConnectionFactory, MySqlConnectionFactory>();
services.AddScoped<ISoapProxyService, SoapProxyService>();
services.AddScoped<IAccountManagementService, AccountManagementService>();
```

## Technical Details

### SOAP Integration

**URL Format:** `http://ac-worldserver-{stackId}:{soapPort}/`

**Authentication:** Stack-specific credentials from configuration

**XML Escaping:** Uses `SecurityElement.Escape()` to prevent injection

**Example SOAP Envelope:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns1="urn:AC">
  <SOAP-ENV:Body>
    <ns1:executeCommand>
      <command>account create username password</command>
      <username>admin</username>
      <password>admin</password>
    </ns1:executeCommand>
  </SOAP-ENV:Body>
</SOAP-ENV:Envelope>
```

### MySQL Connections

**Container Name:** `ac-database-{stackId}`

**Connection String Format:**
```
Server=ac-database-{stackId};Port={port};Database={dbName};Uid=root;Pwd={password};AllowPublicKeyRetrieval=True;
```

**Database Names:**
- `acore_auth` - Account authentication
- `acore_world` - World data
- `acore_characters` - Character data

### Query Examples

**Get Accounts:**
```sql
SELECT 
  a.id AS Id,
  a.username AS Username,
  COALESCE(aa.gmlevel, 0) AS GmLevel,
  a.last_login AS LastLogin,
  COUNT(DISTINCT c.guid) AS CharacterCount,
  MAX(c.online) AS IsOnline
FROM account a
LEFT JOIN account_access aa ON a.id = aa.AccountID AND aa.RealmID = -1
LEFT JOIN acore_characters.characters c ON c.account = a.id
GROUP BY a.id, a.username, aa.gmlevel, a.last_login
```

**Get Characters:**
```sql
SELECT 
  guid, name, account, level, race, class, gender, 
  online, totaltime, map, position_x, position_y, position_z
FROM characters 
WHERE account = @AccountId
ORDER BY level DESC, totaltime DESC
```

## Testing Checklist

To test the implementation:

1. **Start API:**
   ```bash
   cd backend
   dotnet run --project AzerothCoreManager.Api
   ```

2. **Access Swagger UI:**
   - Open http://localhost:5000/swagger
   - Verify all account/character endpoints are listed

3. **Test Endpoints (requires running stack):**
   - GET `/api/stacks/{stackId}/accounts` - Should return account list
   - POST `/api/stacks/{stackId}/accounts` - Create test account
   - GET `/api/stacks/{stackId}/accounts/{accountId}/characters` - List characters
   - POST `/api/stacks/{stackId}/accounts/{accountId}/set-gm-level` - Set GM level
   - POST `/api/stacks/{stackId}/characters/{name}/send-message` - Send message

## Next Steps (Frontend)

The backend is complete and ready for frontend integration:

1. Create `AccountsTab` component in Stack Details page
2. Add tab navigation (Overview, Accounts, Logs, Configuration)
3. Implement account list with search/filter
4. Add account details panel with GM level controls
5. Create character action dropdowns
6. Add forms for creating accounts, banning, sending items/money

See the plan document for frontend implementation details.

## Notes

- No authentication/rate limiting implemented (private tool assumption)
- All operations are logged with Serilog
- Errors return meaningful HTTP status codes and messages
- SOAP commands return success/failure based on response text parsing
- MySQL queries use Dapper for clean, parameterized queries
- All services use async/await with CancellationToken support

## Files Modified

### Configuration
- `Core/Contracts/AdvancedConfigDto.cs` - Added SOAP credentials
- `Infrastructure/Data/Entities/ManagedStackEntity.cs` - Added SOAP credential fields
- `Infrastructure/Services/StackService.cs` - Persist SOAP credentials on create/update

### Service Registration
- `Infrastructure/DependencyInjection.cs` - Registered new services

### Database
- New migration: `AddSoapCredentials`

## Files Created

### Core (Contracts & Interfaces)
- `Core/Contracts/SoapCommandDto.cs`
- `Core/Contracts/AccountDto.cs`
- `Core/Contracts/CharacterDto.cs`
- `Core/Services/Interfaces/ISoapProxyService.cs`
- `Core/Services/Interfaces/IAccountManagementService.cs`
- `Core/Services/Interfaces/IMySqlConnectionFactory.cs`

### Infrastructure (Services)
- `Infrastructure/Services/SoapProxyService.cs`
- `Infrastructure/Services/AccountManagementService.cs`
- `Infrastructure/Data/MySqlConnectionFactory.cs`

### API (Controllers)
- `Api/Controllers/AccountsController.cs`
- `Api/Controllers/CharactersController.cs`

Total: 14 files created, 4 files modified
