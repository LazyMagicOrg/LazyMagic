# Cognito Authentication Fix Guide

## Issues Identified

1. **Content Security Policy (CSP) Violation**
   - The Cognito domain is being blocked by CSP
   - Error: "Refused to frame 'https://496a-bd90-tenantauth.auth.us-west-2.amazoncognito.com/' because it violates the following Content Security Policy directive"

2. **Redirect URI Mismatch**
   - Cognito is returning `redirect_mismatch` error
   - App is using: `https://uptown.lazymagicdev.click/authentication/login-callback`

## Required Fixes

### 1. Update Content Security Policy

The current CSP is:
```
default-src 'self' https://0.0.0.0 https://*.lazymagicdev.click
```

This needs to be updated to include the Cognito domain. Update your CloudFront distribution or S3 bucket headers to:

```
Content-Security-Policy: default-src 'self' https://0.0.0.0 https://*.lazymagicdev.click https://*.amazoncognito.com; frame-src 'self' https://*.amazoncognito.com; frame-ancestors 'self' https://*.amazoncognito.com;
```

Or more specifically for your domain:
```
Content-Security-Policy: default-src 'self' https://0.0.0.0 https://*.lazymagicdev.click https://496a-bd90-tenantauth.auth.us-west-2.amazoncognito.com; frame-src 'self' https://496a-bd90-tenantauth.auth.us-west-2.amazoncognito.com;
```

### 2. Update Cognito App Client Settings

In AWS Cognito Console:

1. Go to your User Pool: `496a-bd90-tenantauth`
2. Navigate to "App integration" → "App client settings" 
3. Find client ID: `7s0r69bat6b7ojblvcof24jf0d`
4. Update the **Callback URL(s)** to include:
   ```
   https://uptown.lazymagicdev.click/authentication/login-callback
   ```

5. Update the **Sign out URL(s)** to include:
   ```
   https://uptown.lazymagicdev.click
   ```

6. Ensure **Allowed OAuth Flows** includes:
   - Authorization code grant

7. Ensure **Allowed OAuth Scopes** includes:
   - openid
   - profile
   - email

### 3. Alternative: Update App Configuration

If you cannot modify the Cognito settings, you can check what redirect URIs are currently configured and update your app to match. The app's redirect URI is currently:
- `https://uptown.lazymagicdev.click/authentication/login-callback`

Make sure this EXACTLY matches what's in Cognito (including trailing slashes).

## Verification Steps

1. After updating CSP, verify no console errors about frame violations
2. After updating Cognito redirect URIs, attempt login again
3. Check browser console for any remaining errors

## Common Issues

- **Trailing slash mismatch**: Ensure URLs match exactly (with or without trailing slash)
- **HTTP vs HTTPS**: Always use HTTPS for production
- **Case sensitivity**: URLs are case-sensitive in Cognito
- **Multiple environments**: Each environment needs its own redirect URI entry