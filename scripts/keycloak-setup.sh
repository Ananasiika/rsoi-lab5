#!/bin/bash

echo "Setting up Keycloak realm and client..."

# Ждем запуска Keycloak
until curl -f http://localhost:8081/realms/master; do
    echo "Waiting for Keycloak..."
    sleep 5
done

echo "Keycloak is ready, configuring..."

# Получаем access token для администратора
ACCESS_TOKEN=$(curl -s -X POST \
  http://localhost:8081/realms/master/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=admin&password=admin&grant_type=password&client_id=admin-cli" | jq -r '.access_token')

if [ "$ACCESS_TOKEN" == "null" ]; then
    echo "Failed to get admin access token"
    exit 1
fi

echo "Access token obtained"

# Создаем realm
curl -X POST \
  http://localhost:8081/admin/realms \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "realm": "flight-booking",
    "enabled": true,
    "displayName": "Flight Booking System"
  }'

# Создаем client
CLIENT_ID=$(curl -s -X POST \
  http://localhost:8081/admin/realms/flight-booking/clients \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "flight-booking-client",
    "enabled": true,
    "publicClient": false,
    "secret": "flight-booking-secret-2025-rsoi-lab5",
    "directAccessGrantsEnabled": true,
    "serviceAccountsEnabled": true,
    "authorizationServicesEnabled": true,
    "protocol": "openid-connect",
    "attributes": {
      "access.token.lifespan": 300
    }
  }' | jq -r '.id')

echo "Client created with ID: $CLIENT_ID"

# Создаем пользователя
curl -X POST \
  http://localhost:8081/admin/realms/flight-booking/users \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "enabled": true,
    "email": "test@example.com",
    "firstName": "Test",
    "lastName": "User",
    "credentials": [
      {
        "type": "password",
        "value": "testpassword",
        "temporary": false
      }
    ]
  }'

echo "Keycloak setup completed successfully"