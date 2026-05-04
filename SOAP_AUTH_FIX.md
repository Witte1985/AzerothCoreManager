# AzerothCore SOAP Authentication Fix

## Problem Summary

**401 Unauthorized when making SOAP requests to AzerothCore worldserver**

### Root Cause

AzerothCore's SOAP interface requires HTTP Basic Authentication credentials to be configured in `worldserver.conf`. The authentication system has two completely separate mechanisms:

1. **Game Client Authentication (SRP6)**
   - Uses `salt` and `verifier` columns in `account` table
   - For WoW game client login only
   - **Not used by SOAP**

2. **SOAP HTTP Basic Auth**
   - Uses `SOAP.User` and `SOAP.Password` settings in `worldserver.conf`
   - **This is what you're missing**
   - Completely independent of database accounts

Your `worldserver.conf` only has:
- `SOAP.Enabled = 1`
- `SOAP.IP = "127.0.0.1"`
- `SOAP.Port = 7878`

But it's **missing**:
- `SOAP.User = "admin"`
- `SOAP.Password = "admin"`

## Solution

### Option 1: Manual Configuration (Quick Fix)

1. **Connect to your worldserver container:**
   ```bash
   docker exec -it acore-63db3c3414434a9c9f91536123998592-worldserver bash
   ```

2. **Edit worldserver.conf:**
   ```bash
   nano /azerothcore/env/dist/etc/worldserver.conf
   ```

3. **Add these lines after the SOAP.Port setting (around line 470):**
   ```ini
   #
   #    SOAP.User
   #        Description: Username for SOAP HTTP Basic Authentication
   #        Default:     "" - (No authentication)
   
   SOAP.User = "admin"
   
   #
   #    SOAP.Password
   #        Description: Password for SOAP HTTP Basic Authentication
   #        Default:     "" - (No authentication)
   
   SOAP.Password = "admin"
   ```

4. **Restart the worldserver container:**
   ```bash
   docker restart acore-63db3c3414434a9c9f91536123998592-worldserver
   ```

5. **Test the SOAP endpoint:**
   ```bash
   curl -v --basic --user admin:admin \
     -H "Content-Type: text/xml" \
     -H "SOAPAction: \"urn:AC#executeCommand\"" \
     -d '<?xml version="1.0" encoding="utf-8"?>
   <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ns1="urn:AC">
     <SOAP-ENV:Body>
       <ns1:executeCommand>
         <command>server info</command>
         <username>admin</username>
         <password>admin</password>
       </ns1:executeCommand>
     </SOAP-ENV:Body>
   </SOAP-ENV:Envelope>' \
     http://localhost:7878/
   ```

### Option 2: Automated Configuration (Permanent Fix)

Modify the AzerothCoreManager to automatically inject SOAP credentials during stack creation/startup.

#### Implementation Plan:

1. **Add worldserver.conf customization support to StackService**
2. **Inject SOAP.User and SOAP.Password during stack initialization**
3. **Ensure credentials persist across container restarts**

---

## Understanding AzerothCore Authentication

### 1. SOAP Authentication Flow

```
┌─────────────┐                    ┌──────────────┐
│  SOAP       │  HTTP Basic Auth   │  worldserver │
│  Client     │ ─────────────────> │  SOAP API    │
│  (Manager)  │  user: admin       │              │
│             │  pass: admin       │              │
└─────────────┘                    └──────────────┘
                                          │
                                          ▼
                                   Checks SOAP.User
                                   and SOAP.Password
                                   from worldserver.conf
                                          │
                                          ▼
                                   ✓ Authorized
                                   (executes command)
```

**Important:** The SOAP username/password are:
- **Configured in `worldserver.conf`**
- **Not stored in the database**
- **Not related to game accounts**
- **Used only for SOAP API authentication**

### 2. Game Account Authentication (Separate System)

```
┌─────────────┐                    ┌──────────────┐
│  WoW        │  SRP6 Challenge    │  authserver  │
│  Client     │ ─────────────────> │              │
│             │                    │              │
└─────────────┘                    └──────────────┘
                                          │
                                          ▼
                                   Queries account table
                                   Uses salt + verifier
                                   for SRP6 verification
                                          │
                                          ▼
                                   ✓ Authenticated
                                   (allows login)
```

**This uses:**
- `account.salt` (32 bytes)
- `account.verifier` (32 bytes)
- **Not used by SOAP**

### 3. Why Your salt/verifier Are Zeros

The admin account in your database has zero salt/verifier because:

1. **It was created directly in the database** (INSERT statement)
2. **Not created via worldserver console command** (`account create`)

To set a proper password for game login:
```bash
# Connect to worldserver console
docker attach acore-63db3c3414434a9c9f91536123998592-worldserver

# Run command (requires worldserver to be running)
account set password admin newpassword newpassword
```

But again: **This is completely separate from SOAP authentication!**

---

## Bootstrapping Pattern

### The Chicken-and-Egg Problem

You asked: "Do I need SOAP to work to create the first GM account, but SOAP needs a GM account to authenticate?"

**Answer: No!** Here's why:

1. **SOAP credentials are in worldserver.conf** (not database)
   - Set at server configuration time
   - No database dependency

2. **SOAP commands don't require a logged-in GM**
   - SOAP authentication = HTTP Basic Auth only
   - Once authenticated via SOAP.User/SOAP.Password, you can execute any command
   - No "GM account in database" needed for SOAP to work

3. **Proper Bootstrap Sequence:**
   ```
   1. Configure SOAP credentials in worldserver.conf
      ↓
   2. Start worldserver
      ↓
   3. Use SOAP to create first GM account:
      Command: "account create admin password"
      ↓
   4. Use SOAP to grant GM permissions:
      Command: "account set gmlevel admin 3 -1"
      ↓
   5. (Optional) Set game password for login:
      Command: "account set password admin newpass newpass"
   ```

### Your Current State

```
✗ SOAP.User and SOAP.Password not configured → 401 Unauthorized
✓ Database has admin account (id=1)
✗ salt/verifier are zeros → Can't log into game
✓ account_access shows gmlevel=3 → Proper GM permissions (for game, not SOAP)
```

**Fix:** Just add SOAP.User and SOAP.Password to worldserver.conf!

---

## Default Credentials in AzerothCore

AzerothCore **does not** have default SOAP credentials. You must explicitly set:
- `SOAP.User`
- `SOAP.Password`

If these are missing or empty, the worldserver will reject all SOAP requests with 401.

Common practice:
- Development: `SOAP.User = "admin"`, `SOAP.Password = "admin"`
- Production: Use strong credentials and restrict `SOAP.IP` to localhost/LAN

---

## Testing Checklist

After applying the fix:

- [ ] SOAP credentials added to worldserver.conf
- [ ] Worldserver restarted
- [ ] Test with curl: `curl -v --basic --user admin:admin http://localhost:7878/`
- [ ] Expected: HTTP 200 (not 401)
- [ ] Test SOAP command via your API
- [ ] Create account via SOAP: `account create testuser testpass`
- [ ] Verify account created in database

---

## Implementation Notes for AzerothCoreManager

### Current Behavior

Your `SoapProxyService` correctly sends username/password in the SOAP envelope:
```xml
<username>{escapedUsername}</username>
<password>{escapedPassword}</password>
```

And you store these in the database:
```csharp
public string SoapUsername { get; set; } = "admin";
public string SoapPassword { get; set; } = "admin";
```

**But:** The worldserver doesn't know about these credentials because they're not in `worldserver.conf`.

### Required Changes

You need to modify `StackService.cs` or `BuildService.cs` to:

1. **Generate a worldserver.conf.local or use environment variables** to inject SOAP credentials
2. **Or mount a custom worldserver.conf** with SOAP.User and SOAP.Password set

Suggested approach:
- Use Docker environment variables to inject configuration
- Or create a post-build step to modify worldserver.conf
- Or use a volume mount with a custom config snippet

---

## Security Considerations

1. **Never expose SOAP to the internet directly**
   - Keep `SOAP.IP = "127.0.0.1"` for localhost-only
   - Use VPN or SSH tunnel for remote access

2. **Use strong passwords in production**
   - Don't use "admin/admin" in production stacks
   - Store credentials securely (environment variables, secrets management)

3. **SOAP has no encryption**
   - HTTP Basic Auth + SOAP = credentials in plaintext
   - Only use over trusted networks (localhost, internal LAN)
   - Consider adding TLS proxy if needed over network

---

## References

- [AzerothCore SOAP Documentation](https://www.azerothcore.org/wiki/soap-server)
- AzerothCore PR #3421 (introduced SOAP.User and SOAP.Password)
- Your code: `backend/AzerothCoreManager.Infrastructure/Services/SoapProxyService.cs`
- Your code: `backend/AzerothCoreManager.Core/Contracts/AdvancedConfigDto.cs`
