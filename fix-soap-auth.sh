#!/bin/bash
# Fix AzerothCore SOAP Authentication
# Adds SOAP.User and SOAP.Password to worldserver.conf

set -e

STACK_ID="${1:-63db3c3414434a9c9f91536123998592}"
CONTAINER_NAME="acore-${STACK_ID}-worldserver"
SOAP_USER="${2:-admin}"
SOAP_PASSWORD="${3:-admin}"

echo "=================================================="
echo "AzerothCore SOAP Authentication Fix"
echo "=================================================="
echo "Stack ID: $STACK_ID"
echo "Container: $CONTAINER_NAME"
echo "SOAP User: $SOAP_USER"
echo "SOAP Password: [hidden]"
echo "=================================================="
echo

# Check if container exists
if ! docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "❌ ERROR: Container '$CONTAINER_NAME' not found"
    echo
    echo "Available containers:"
    docker ps -a --filter "name=acore-" --format "table {{.Names}}\t{{.Status}}"
    exit 1
fi

# Check if container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "⚠️  WARNING: Container '$CONTAINER_NAME' is not running"
    echo "Starting container..."
    docker start "$CONTAINER_NAME" || {
        echo "❌ Failed to start container"
        exit 1
    }
    echo "Waiting for container to be ready..."
    sleep 5
fi

echo "📝 Checking current SOAP configuration..."
CONF_PATH="/azerothcore/env/dist/etc/worldserver.conf"

# Check if SOAP.User already exists
if docker exec "$CONTAINER_NAME" grep -q "^SOAP\.User" "$CONF_PATH" 2>/dev/null; then
    echo "✓ SOAP.User already configured"
    CURRENT_USER=$(docker exec "$CONTAINER_NAME" grep "^SOAP\.User" "$CONF_PATH" | cut -d= -f2 | tr -d ' "')
    echo "  Current value: $CURRENT_USER"
else
    echo "✗ SOAP.User not found - will add it"
fi

if docker exec "$CONTAINER_NAME" grep -q "^SOAP\.Password" "$CONF_PATH" 2>/dev/null; then
    echo "✓ SOAP.Password already configured"
else
    echo "✗ SOAP.Password not found - will add it"
fi

echo
read -p "Continue with adding/updating SOAP credentials? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

echo
echo "🔧 Adding SOAP credentials to worldserver.conf..."

# Create a temporary script to run inside the container
docker exec "$CONTAINER_NAME" bash -c "cat >> $CONF_PATH << 'EOFSOAP'

#
#    SOAP.User
#        Description: Username for SOAP HTTP Basic Authentication
#        Default:     \"\" - (No authentication)
#        Added by: AzerothCoreManager fix-soap-auth.sh

SOAP.User = \"$SOAP_USER\"

#
#    SOAP.Password
#        Description: Password for SOAP HTTP Basic Authentication
#        Default:     \"\" - (No authentication)
#        Added by: AzerothCoreManager fix-soap-auth.sh

SOAP.Password = \"$SOAP_PASSWORD\"
EOFSOAP
"

echo "✓ SOAP credentials added to worldserver.conf"
echo

echo "🔄 Restarting worldserver container..."
docker restart "$CONTAINER_NAME"

echo "⏳ Waiting for worldserver to start (15 seconds)..."
sleep 15

echo
echo "✅ Configuration complete!"
echo
echo "=================================================="
echo "Testing SOAP Endpoint"
echo "=================================================="
echo

# Test the SOAP endpoint
SOAP_PORT=$(docker port "$CONTAINER_NAME" 7878 2>/dev/null | cut -d: -f2)
if [ -z "$SOAP_PORT" ]; then
    SOAP_PORT=7878
    echo "⚠️  Could not detect SOAP port mapping, using default: $SOAP_PORT"
else
    echo "✓ Detected SOAP port: $SOAP_PORT"
fi

SOAP_URL="http://localhost:$SOAP_PORT/"

echo
echo "Testing with curl..."
RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" --basic --user "$SOAP_USER:$SOAP_PASSWORD" \
  -H "Content-Type: text/xml" \
  -H "SOAPAction: \"urn:AC#executeCommand\"" \
  -d "<?xml version=\"1.0\" encoding=\"utf-8\"?>
<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ns1=\"urn:AC\">
  <SOAP-ENV:Body>
    <ns1:executeCommand>
      <command>server info</command>
      <username>$SOAP_USER</username>
      <password>$SOAP_PASSWORD</password>
    </ns1:executeCommand>
  </SOAP-ENV:Body>
</SOAP-ENV:Envelope>" \
  "$SOAP_URL" 2>&1)

HTTP_CODE=$(echo "$RESPONSE" | grep "HTTP_CODE:" | cut -d: -f2)
BODY=$(echo "$RESPONSE" | sed '/HTTP_CODE:/d')

echo
if [ "$HTTP_CODE" = "200" ]; then
    echo "✅ SUCCESS! SOAP authentication working (HTTP 200)"
    echo
    echo "Response:"
    echo "$BODY"
elif [ "$HTTP_CODE" = "401" ]; then
    echo "❌ FAILED! Still getting 401 Unauthorized"
    echo
    echo "Possible issues:"
    echo "1. Worldserver hasn't fully restarted yet (wait 30-60 seconds)"
    echo "2. Configuration file wasn't updated correctly"
    echo "3. Credentials mismatch"
    echo
    echo "Check worldserver logs:"
    echo "  docker logs $CONTAINER_NAME --tail 50"
else
    echo "⚠️  Unexpected HTTP code: $HTTP_CODE"
    echo
    echo "Response:"
    echo "$BODY"
fi

echo
echo "=================================================="
echo "Next Steps"
echo "=================================================="
echo
echo "1. Verify SOAP credentials in your AzerothCoreManager match:"
echo "   - SOAP Username: $SOAP_USER"
echo "   - SOAP Password: [check your stack configuration]"
echo
echo "2. Test via your API:"
echo "   POST /api/accounts/soap/execute"
echo "   Body: { \"stackId\": \"$STACK_ID\", \"command\": \"server info\" }"
echo
echo "3. Check worldserver logs if issues persist:"
echo "   docker logs $CONTAINER_NAME -f"
echo
echo "4. Verify the configuration persists:"
echo "   docker exec $CONTAINER_NAME grep 'SOAP\.' $CONF_PATH"
echo
