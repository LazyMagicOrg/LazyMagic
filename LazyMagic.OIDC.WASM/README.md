# LazyMagic Blazor WASM Authentication Flow

## Overview
This document describes the expected authentication behavior for the LazyMagic Blazor WASM application, including RememberMe functionality and token management.

## Authentication State Diagram

```
┌─────────────────┐     Login     ┌──────────────────┐
│  Unauthenticated│─────────────▶ │   Authenticating │
│   (No tokens)   │               │   (OAuth flow)   │
└─────────────────┘               └──────────────────┘
         ▲                                   │
         │                                   │ Success
         │ Logout                           ▼
         │              ┌────────────────────────────────┐
         │              │       Authenticated            │
         │              │   (Valid tokens in storage)    │
         │              └────────────────────────────────┘
         │                           │                │
         │                           │                │
         └───────────────────────────┘                │
                                                      │
                    App Restart                       │
                         │                            │
                         ▼                            │
              ┌─────────────────────┐                 │
              │   Token Check       │                 │
              │   (On app startup)  │                 │
              └─────────────────────┘                 │
                         │                            │
                    ┌────┴────┐                       │
                    │         │                       │
           RememberMe=True    RememberMe=False        │
                    │         │                       │
                    │    Clear tokens                 │
                    │    Go to Unauthenticated        │
                    │         │                       │
                    │         └─────────────────────▲ │
                    │                               │ │
              Validate tokens                       │ │
                    │                               │ │
              ┌─────┴─────┐                         │ │
              │           │                         │ │
         Valid tokens  Invalid tokens               │ │
              │           │                         │ │
              │      Clear tokens                   │ │
              │      Go to Unauthenticated          │ │
              │           │                         │ │
              │           └─────────────────────────┘ │
              │                                       │
              └───────────────────────────────────────┘
```

## Token Storage Strategy

### RememberMe = True
- Tokens stored in **localStorage** (persistent across browser sessions)
- On app restart: Tokens copied from localStorage to sessionStorage
- User remains logged in across browser sessions

### RememberMe = False  
- Tokens stored in **sessionStorage** (cleared when browser closes)
- On app restart: No token restoration
- User must log in again after browser restart

### Logout Behavior
- **Always** clear tokens from both localStorage and sessionStorage
- Navigate to unauthenticated state
- RememberMe setting is irrelevant during logout

## Test Cases

### Test 1: Fresh App Launch (No Tokens)
**Setup**: Clear all browser storage
**Action**: Launch app
**Expected**: 
- Fast loading (< 1 second)
- Shows "Log in" button
- No authentication delays

### Test 2: Login Process
**Setup**: Fresh app (no tokens)
**Action**: Click "Log in" button
**Expected**:
- Redirects to Cognito hosted UI
- After credentials: Returns to app
- Shows authenticated state (user name, "Log out" button)
- Fast transition (< 2 seconds)

### Test 3: RememberMe Enabled During Session
**Setup**: User is authenticated
**Action**: Enable RememberMe toggle
**Expected**:
- Tokens moved from sessionStorage to localStorage
- User remains authenticated
- RememberMe setting stored

### Test 4: RememberMe Disabled During Session
**Setup**: User is authenticated with RememberMe enabled
**Action**: Disable RememberMe toggle  
**Expected**:
- Tokens moved from localStorage to sessionStorage
- User remains authenticated in current session
- RememberMe setting updated

### Test 5: App Restart with RememberMe Enabled
**Setup**: User authenticated with RememberMe enabled, close/restart browser
**Action**: Launch app
**Expected**:
- Loading screen shows briefly
- Automatic authentication (no login prompt)
- User authenticated within 3 seconds
- Shows authenticated state

### Test 6: App Restart with RememberMe Disabled
**Setup**: User authenticated with RememberMe disabled, close/restart browser
**Action**: Launch app
**Expected**:
- Fast loading (< 1 second)
- Shows "Log in" button (unauthenticated state)
- No tokens in storage

### Test 7: Logout with RememberMe Enabled
**Setup**: User authenticated with RememberMe enabled
**Action**: Click "Log out"
**Expected**:
- Clears ALL tokens (localStorage + sessionStorage)
- Shows logout page briefly
- Returns to unauthenticated state
- Shows "Log in" button

### Test 8: Logout with RememberMe Disabled  
**Setup**: User authenticated with RememberMe disabled
**Action**: Click "Log out"
**Expected**:
- Clears ALL tokens (sessionStorage)
- Shows logout page briefly  
- Returns to unauthenticated state
- Shows "Log in" button

### Test 9: OAuth Callback Interrupted
**Setup**: User clicks login, gets redirected to Cognito, returns with auth code
**Action**: App processes OAuth callback
**Expected**:
- Completes authentication flow
- Processes authorization code
- Stores tokens appropriately based on RememberMe setting
- Shows authenticated state

### Test 10: Expired Token Handling
**Setup**: User has expired tokens in storage
**Action**: Launch app
**Expected**:
- Detects expired tokens
- Clears invalid tokens
- Shows unauthenticated state
- No long delays or errors

## Performance Requirements

- **Fresh app launch**: < 1 second to show UI
- **RememberMe auto-login**: < 3 seconds total
- **Login flow**: < 2 seconds after Cognito redirect
- **Logout**: < 2 seconds to complete
- **UI transitions**: Immediate (no "Authorizing..." delays)

## Common Issues

### Long Loading Delays
- Usually caused by unnecessary OIDC validation calls
- Should only validate when tokens exist and are needed

### "Authorizing..." Delays
- Indicates UI is waiting for authentication state
- Should be resolved by proper OIDC warmup timing

### Login Without Credentials  
- Caused by stale tokens not being cleared after logout
- Logout must clear ALL tokens regardless of RememberMe setting

### Broken OAuth Flow
- Usually caused by clearing tokens during OAuth callback
- Must preserve OAuth state tokens during authentication flow

## Token Cleanup Rules

1. **Logout**: Always clear ALL tokens
2. **Fresh launch + RememberMe=False**: Clear any leftover tokens
3. **OAuth callback**: Never clear tokens (needed for flow completion)
4. **Expired tokens**: Clear and show unauthenticated state
5. **RememberMe toggle**: Move tokens between storage types, don't clear