/**
 * SimpleAuth - Minimal JavaScript Authentication App
 * Handles multi-auth OIDC with Cognito and subtenant routing
 */

// ============================================================================
// Cookie Management
// ============================================================================

export function setCookie(name, value, options = {}) {
    const domain = options.domain || `.${getRootDomain()}`;
    const path = options.path || '/';
    const days = options.days || 30;
    const secure = options.secure !== false;
    const sameSite = options.sameSite || 'Lax';

    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    const parts = [
        `${name}=${encodeURIComponent(value)}`,
        `domain=${domain}`,
        `path=${path}`,
        `expires=${expires}`,
        `samesite=${sameSite}`
    ];

    if (secure) parts.push('secure');

    document.cookie = parts.join('; ');
    console.log(`Cookie set: ${name}=${value} for domain ${domain}`);
}

export function getCookie(name) {
    const value = document.cookie
        .split('; ')
        .find(row => row.startsWith(`${name}=`))
        ?.split('=')[1];
    return value ? decodeURIComponent(value) : null;
}

export function getRootDomain() {
    const parts = window.location.hostname.split('.');
    return parts.length > 2 ? parts.slice(-2).join('.') : window.location.hostname;
}

// ============================================================================
// Config and Token Management
// ============================================================================

export async function fetchAuthConfigs() {
    try {
        const response = await fetch('/config');
        if (!response.ok) {
            throw new Error(`Failed to fetch config: ${response.status}`);
        }
        const config = await response.json();
        return config.authConfigs || {};
    } catch (error) {
        console.error('Error fetching auth configs:', error);
        throw error;
    }
}

export async function fetchSubtenants() {
    try {
        const response = await fetch('/ActiveSubTenantModule/activesubtenant/list');
        if (!response.ok) {
            throw new Error(`Failed to fetch subtenants: ${response.status}`);
        }
        const subtenants = await response.json();
        console.log('Fetched subtenants:', subtenants);
        return subtenants;
    } catch (error) {
        console.error('Error fetching subtenants:', error);
        throw error;
    }
}

export function checkForValidTokens(authConfigs) {
    for (const [name, config] of Object.entries(authConfigs)) {
        const authority = config.authority;
        const clientId = config.ClientId || config.clientId;

        if (!authority || !clientId) continue;

        const key = `oidc.user:${authority}:${clientId}`;
        const tokenJson = localStorage.getItem(key);

        if (tokenJson) {
            try {
                const tokenData = JSON.parse(tokenJson);
                const expiresAt = tokenData.expires_at;
                const now = Math.floor(Date.now() / 1000);

                if (expiresAt && expiresAt > now) {
                    console.log(`Found valid tokens for: ${name}`);
                    return { valid: true, authConfigName: name };
                }
            } catch (error) {
                console.warn(`Error parsing tokens for ${name}:`, error);
            }
        }
    }

    return { valid: false, authConfigName: null };
}

export function storeTokens(tokens, authConfig) {
    const authority = authConfig.authority;
    const clientId = authConfig.ClientId || authConfig.clientId;
    const expiresAt = Math.floor(Date.now() / 1000) + (tokens.expires_in || 3600);

    const tokenData = {
        access_token: tokens.access_token,
        id_token: tokens.id_token,
        refresh_token: tokens.refresh_token,
        token_type: tokens.token_type || 'Bearer',
        scope: tokens.scope || 'openid profile email',
        expires_at: expiresAt
    };

    const key = `oidc.user:${authority}:${clientId}`;
    localStorage.setItem(key, JSON.stringify(tokenData));
    console.log(`Tokens stored for: ${key}`);
}

// ============================================================================
// OAuth Flow
// ============================================================================

export function buildAuthorizeUrl(authConfig, stateData) {
    const authority = authConfig.HostedUIDomain || authConfig.authority;
    const clientId = authConfig.ClientId || authConfig.clientId;
    const redirectUri = `${window.location.origin}/auth/callback`;

    // Encode state as Base64 JSON
    const stateJson = JSON.stringify(stateData);
    const stateEncoded = btoa(stateJson);

    const params = new URLSearchParams({
        client_id: clientId,
        redirect_uri: redirectUri,
        response_type: 'code',
        scope: 'openid profile email',
        state: stateEncoded
    });

    return `${authority}/oauth2/authorize?${params.toString()}`;
}

export async function exchangeCodeForTokens(code, authConfig) {
    const authority = authConfig.HostedUIDomain || authConfig.authority;
    const clientId = authConfig.ClientId || authConfig.clientId;
    const redirectUri = `${window.location.origin}/auth/callback`;
    const tokenEndpoint = `${authority}/oauth2/token`;

    const params = new URLSearchParams({
        grant_type: 'authorization_code',
        client_id: clientId,
        code: code,
        redirect_uri: redirectUri
    });

    console.log(`Exchanging code at: ${tokenEndpoint}`);

    const response = await fetch(tokenEndpoint, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded'
        },
        body: params.toString()
    });

    if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Token exchange failed: ${response.status} - ${errorText}`);
    }

    return await response.json();
}

// ============================================================================
// URL Helpers
// ============================================================================

export function getQueryParams() {
    const params = new URLSearchParams(window.location.search);
    return {
        code: params.get('code'),
        state: params.get('state'),
        error: params.get('error'),
        error_description: params.get('error_description'),
        returnUrl: params.get('returnUrl'),
        subtenant: params.get('subtenant'),
        authname: params.get('authname'),
        app: params.get('app')
    };
}

export function decodeState(stateParam) {
    try {
        const stateJson = atob(stateParam);
        return JSON.parse(stateJson);
    } catch (error) {
        console.error('Error decoding state:', error);
        throw new Error('Invalid state parameter');
    }
}

export function buildRedirectUrl(subtenant, returnUrl, targetDomain) {
    if (subtenant && targetDomain) {
        const protocol = window.location.protocol;
        return `${protocol}//${subtenant}.${targetDomain}${returnUrl || '/'}`;
    }
    return returnUrl || '/';
}

// ============================================================================
// Display Helpers
// ============================================================================

export function formatAuthConfigName(name) {
    // Convert "ConsumerAuth" to "Consumer Auth"
    return name.replace(/([A-Z])/g, ' $1').trim();
}

export function showElement(id) {
    const element = document.getElementById(id);
    if (element) element.style.display = 'block';
}

export function hideElement(id) {
    const element = document.getElementById(id);
    if (element) element.style.display = 'none';
}

export function setText(id, text) {
    const element = document.getElementById(id);
    if (element) element.textContent = text;
}

export function setHTML(id, html) {
    const element = document.getElementById(id);
    if (element) element.innerHTML = html;
}
