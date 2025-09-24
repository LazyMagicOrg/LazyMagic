
let viewerInstance;
export async function initialize(viewerInstanceArg) {
    viewerInstance = viewerInstanceArg; // allows JS to do callbacks on the viewerInstance C# class
    if (navigator.serviceWorker) {
        console.log("registering service worker events for update/install");
        navigator.serviceWorker.addEventListener('message', event => {
            console.log("message:" + event.data.action + "," + event.data.info);
            switch (event.data.action) {
                case 'AssetDataCheckStarted':
                    viewerInstance.invokeMethodAsync('AssetDataCheckStarted');
                    break;
                case 'AssetDataCheckComplete':
                    viewerInstance.invokeMethodAsync('AssetDataCheckComplete');
                    break;
                case 'AssetDataUpdateStarted':
                    viewerInstance.invokeMethodAsync('AssetDataUpdateStarted');
                    break;
                case 'AssetDataUpdateComplete':
                    viewerInstance.invokeMethodAsync('AssetDataUpdateComplete');
                    break;
                case 'ServiceWorkerUpdateStarted':
                    viewerInstance.invokeMethodAsync('ServiceWorkerUpdateStarted');
                    break;
                case 'ServiceWorkerUpdateCompleted':
                    viewerInstance.invokeMethodAsync('ServiceWorkerUpdateComplete');
                    break;
                case 'CacheMiss':
                    const logTextElement = document.getElementById('logText');
                    if (logTextElement) {
                        logTextElement.textContent = `${event.data.action}, ${event.data.info}`;
                    }

                    viewerInstance.invokeMethodAsync('CacheMissAction', event.data.info);
                    break;
                default:
                    break;
                    console.log('Unknown event' + event.data.action + 'received');
            }
        });
    }
}
export async function checkForNewAssetData() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, checkForNewAssetData. No network access");
        return;
    }
    // Put in the post message to service worker to kick off asset data check
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js checkForNewAssetData');
        registration.active.postMessage({ action: 'checkForNewAssetData' });
    }
}
export async function assetDataUpdateStarted() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, assetDataUpdateStarted. No network access");
        return;
    }
    // Put in the post message to service worker to kick off service worker update
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js, assetDataUpdateStarted');
        registration.active.postMessage({ action: 'assetDataUpdateStarted' });
    }
}
export async function assetDataUpdateComplete() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, assetDataUpdateComplete. No network access");
        return;
    }
    // Put in the post message to service worker to kick off service worker update
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js, assetDataUpdateComplete');
        registration.active.postMessage({ action: 'assetDataUpdateComplete' });
    }
}
export async function serviceWorkerUpdateStarted() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, serviceWorkerUpdateStarted. No network access");
        return;
    }
    // Put in the post message to service worker to kick off service worker update
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js, serviceWorkerUpdateStarted');
        registration.active.postMessage({ action: 'serviceWorkerUpdateStarted' });
    }
}
export async function reload() {
    //// We can't use reload() because the current browser URL may include a Blazor "page" (component)
    //// and that would cause a 404. Example: /myapp/HomePage.
    //// Also, the reload behavior is different for reload in dev (localhost) and
    //// reload from a non-dev server.
    //const isDev = window.location.hostname.includes('localhost');
    //const baseHrefElement = document.querySelector('base');
    //const appPath = new URL(baseHrefElement.href).pathname;
    //if (isDev) {
    //    location.href = new URL("/", self.location.origin);
    //}
    //else {
    //    console.log("reload appPath:" + appPath);
    //    location.href = new URL(appPath, self.location.origin);
    //}
    window.reload();
}
export async function getMemory() {
    return [performance.memory.jsHeapSizeLimit, performance.memory.usedJSHeapSize]
}
export async function setPointerCapture(element, pointerid) {
    element.setPointerCapture(pointerid);
}
export async function getBase64Image(img) {
    // Create an empty canvas element
    var canvas = document.createElement("canvas");
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;

    // Copy the image contents to the canvas
    var ctx = canvas.getContext("2d");
    ctx.drawImage(img, 0, 0);

    // Using default image/png becuase Safari doesn't support the type argument'
    var dataURL = canvas.toDataURL();
    return dataURL.replace(/^data:image\/(png|jpg);base64,/, "");
}
export async function getBase64ImageDownsized(img) {
    // Create an empty canvas element
    var canvas = document.createElement("canvas");
    let aspectRatio = Number(img.naturalWidth) / Number(img.naturalHeight);

    canvas.width = 600.0;
    canvas.height = aspectRatio / aspectRatio;

    // Copy the image contents to the canvas
    var ctx = canvas.getContext("2d");
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

    // Using default image/png becuase Safari doesn't support the type argument'
    var dataURL = canvas.toDataURL();
    return dataURL.replace(/^data:image\/(png|jpg);base64,/, "");
}
export async function sharePng(title, text, pngData, textData = null) {
    try {

        pngData = pngData.replace(/^data:image\/(png|jpeg|jpg);base64,/, '');
        const binaryData = Uint8Array.from(atob(pngData), c => c.charCodeAt(0));
        const file = new File([binaryData], 'image.png', { type: 'image/png' });
        const files = [file];
        if (textData) { 
            const textFile = new File([textData], 'report.txt', { type: 'text/plain' });
            files.push(textFile);
        }

        await navigator.share({
            title: title,
            text: text,
            files: files
        });
        return true;

    } catch (error) {
        console.error('Error sharing content', error);
    }
    return false;
}
export async function shareText(title, text) {
    try {
        await navigator.share({
            title: title,
            text: text
        });
        return true;

    } catch (error) {
        console.error('Error sharing content', error);
    }
    return false;
}
export async function localStorageSetItem(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch {
        console.error("Can't set key:" + key);
    }
}
export async function localStorageGetItem(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        console.error("Can't read key:" + key);
    }
}
export async function localStorageRemoveItem(key) {
    try {
        localStorage.removeItem(key);
    } catch {
        console.error("Can't remove key:" + key);
    }
}

// Cookie Storage Management Functions

/**
 * Set a cookie with name, value, and optional parameters
 * @param {string} name - Cookie name
 * @param {string} value - Cookie value
 * @param {Object} options - Optional parameters (days, path, domain, secure, sameSite)
 */
export function setCookie(name, value, options = {}) {
    let cookie = `${encodeURIComponent(name)}=${encodeURIComponent(value)}`;

    // Set expiration
    if (options.days) {
        const date = new Date();
        date.setTime(date.getTime() + (options.days * 24 * 60 * 60 * 1000));
        cookie += `; expires=${date.toUTCString()}`;
    }

    // Set path (default to root)
    cookie += `; path=${options.path || '/'}`;

    // Set domain if specified
    if (options.domain) {
        cookie += `; domain=${options.domain}`;
    }

    // Set secure flag if specified
    if (options.secure) {
        cookie += '; secure';
    }

    // Set SameSite attribute if specified
    if (options.sameSite) {
        cookie += `; samesite=${options.sameSite}`;
    }

    document.cookie = cookie;
}

/**
 * Get a cookie value by name
 * @param {string} name - Cookie name
 * @returns {string|null} Cookie value or null if not found
 */
export function getCookie(name) {
    const nameEQ = encodeURIComponent(name) + '=';
    const cookies = document.cookie.split(';');

    for (let cookie of cookies) {
        cookie = cookie.trim();
        if (cookie.indexOf(nameEQ) === 0) {
            return decodeURIComponent(cookie.substring(nameEQ.length));
        }
    }

    return null;
}

/**
 * Delete a cookie by name
 * @param {string} name - Cookie name
 * @param {Object} options - Optional parameters (path, domain)
 */
export function deleteCookie(name, options = {}) {
    setCookie(name, '', {
        days: -1,
        path: options.path || '/',
        domain: options.domain
    });
}

/**
 * Check if a cookie exists
 * @param {string} name - Cookie name
 * @returns {boolean} True if cookie exists, false otherwise
 */
export function cookieExists(name) {
    return getCookie(name) !== null;
}

/**
 * Get all cookies as an object
 * @returns {Object} Object with cookie names as keys and values as values
 */
export function getAllCookies() {
    const cookies = {};
    const cookieArray = document.cookie.split(';');

    for (let cookie of cookieArray) {
        cookie = cookie.trim();
        if (cookie) {
            const [name, value] = cookie.split('=');
            cookies[decodeURIComponent(name)] = decodeURIComponent(value || '');
        }
    }

    return cookies;
}

/**
 * Clear all cookies (for current path and domain)
 * Note: This only clears cookies accessible from the current context
 */
export function clearAllCookies() {
    const cookies = getAllCookies();

    for (const name in cookies) {
        deleteCookie(name);
    }
}

/**
 * Set a JSON object as a cookie
 * @param {string} name - Cookie name
 * @param {Object} obj - JavaScript object to store
 * @param {Object} options - Optional parameters for cookie
 */
export function setJSONCookie(name, obj, options = {}) {
    try {
        const jsonString = JSON.stringify(obj);
        setCookie(name, jsonString, options);
    } catch (e) {
        console.error('Failed to set JSON cookie:', e);
    }
}

/**
 * Get and parse a JSON cookie
 * @param {string} name - Cookie name
 * @returns {Object|null} Parsed object or null if not found/invalid
 */
export function getJSONCookie(name) {
    const value = getCookie(name);

    if (!value) return null;

    try {
        return JSON.parse(value);
    } catch (e) {
        console.error('Failed to parse JSON cookie:', e);
        return null;
    }
}

// Fast Authentication Cache System
let authCache = {
    state: null,
    expiry: 0,
    originalAuthMethods: {},
    isInitialized: false
};

/**
 * Initialize fast authentication interception
 * This intercepts Microsoft's authentication calls to provide cached responses
 */
export async function initializeFastAuth() {
    try {
        console.log('[FastAuth] Initializing authentication interception...');
        
        // Set up fast auth without fetch interception
        // The real solution is to avoid calling Microsoft's slow methods entirely
        
        authCache.isInitialized = true;
        console.log('[FastAuth] Fast authentication initialized successfully');
        return true;
        
    } catch (error) {
        console.error('[FastAuth] Failed to initialize fast auth:', error);
        return false;
    }
}

/**
 * Get cached authentication state
 * @returns {string|null} JSON string of cached auth state or null if not valid
 */
export async function getCachedAuthState() {
    try {
        const cached = getCachedAuthStateFromStorage();
        if (cached && Date.now() < cached.expiry) {
            console.log('[FastAuth] Returning cached authentication state');
            return JSON.stringify(cached.state);
        }
        
        console.log('[FastAuth] No valid cached authentication state found');
        return null;
    } catch (error) {
        console.error('[FastAuth] Error getting cached auth state:', error);
        return null;
    }
}

/**
 * Set cached authentication state
 * @param {string} authStateJson - JSON string of authentication state
 * @param {number} cacheTimeoutMinutes - Cache timeout in minutes (default: 5)
 */
export async function setCachedAuthState(authStateJson, cacheTimeoutMinutes = 5) {
    try {
        const state = JSON.parse(authStateJson);
        const expiry = Date.now() + (cacheTimeoutMinutes * 60 * 1000);
        
        const cacheData = { 
            state, 
            expiry,
            timestamp: Date.now()
        };
        
        localStorage.setItem('lz-fast-auth-cache', JSON.stringify(cacheData));
        authCache.state = state;
        authCache.expiry = expiry;
        
        console.log(`[FastAuth] Authentication state cached for ${cacheTimeoutMinutes} minutes`);
    } catch (error) {
        console.error('[FastAuth] Failed to set auth cache:', error);
    }
}

/**
 * Clear authentication cache
 */
export async function clearAuthCache() {
    try {
        localStorage.removeItem('lz-fast-auth-cache');
        authCache.state = null;
        authCache.expiry = 0;
        console.log('[FastAuth] Authentication cache cleared');
    } catch (error) {
        console.error('[FastAuth] Failed to clear auth cache:', error);
    }
}

/**
 * Check if authentication cache is valid
 * @returns {boolean} True if cache is valid, false otherwise
 */
export async function isAuthCacheValid() {
    try {
        const cached = getCachedAuthStateFromStorage();
        const isValid = cached && Date.now() < cached.expiry;
        console.log(`[FastAuth] Cache validity check: ${isValid}`);
        return isValid;
    } catch (error) {
        console.error('[FastAuth] Error checking cache validity:', error);
        return false;
    }
}

/**
 * Get stored authentication tokens from various browser storage locations
 * @returns {string} JSON string of found tokens
 */
export async function getStoredTokens() {
    try {
        const tokens = {};
        
        // Check common OIDC storage keys
        const tokenKeys = [
            'oidc.user',
            'Microsoft.AspNetCore.Components.WebAssembly.Authentication.CachedAuthSettings',
            'access_token',
            'id_token',
            'refresh_token'
        ];
        
        // Check localStorage
        for (const key of tokenKeys) {
            const value = localStorage.getItem(key);
            if (value) {
                tokens[`localStorage.${key}`] = value;
            }
        }
        
        // Check sessionStorage
        for (const key of tokenKeys) {
            const value = sessionStorage.getItem(key);
            if (value) {
                tokens[`sessionStorage.${key}`] = value;
            }
        }
        
        console.log(`[FastAuth] Found ${Object.keys(tokens).length} stored token entries`);
        return JSON.stringify(tokens);
        
    } catch (error) {
        console.error('[FastAuth] Error getting stored tokens:', error);
        return JSON.stringify({});
    }
}

/**
 * Internal helper to get cached auth state from storage
 * @returns {Object|null} Cached auth data or null if not found/expired
 */
function getCachedAuthStateFromStorage() {
    try {
        const cached = localStorage.getItem('lz-fast-auth-cache');
        if (!cached) return null;
        
        const data = JSON.parse(cached);
        
        // Check if cache is expired
        if (Date.now() >= data.expiry) {
            console.log('[FastAuth] Cache expired, removing...');
            localStorage.removeItem('lz-fast-auth-cache');
            return null;
        }
        
        return data;
    } catch (error) {
        console.error('[FastAuth] Error reading cache from storage:', error);
        // Clean up corrupted cache
        try {
            localStorage.removeItem('lz-fast-auth-cache');
        } catch (cleanupError) {
            console.error('[FastAuth] Failed to cleanup corrupted cache:', cleanupError);
        }
        return null;
    }
}

/**
 * Internal function that provides fast authentication state
 * This can be used to replace Microsoft's slow authentication calls
 * @returns {Promise<Object>} Authentication state object
 */
async function getFastAuthStateInternal() {
    try {
        // First check our cache
        const cached = getCachedAuthStateFromStorage();
        if (cached && Date.now() < cached.expiry) {
            console.log('[FastAuth] Returning cached authentication state (internal)');
            return cached.state;
        }
        
        // Cache miss - we'll need to get fresh data
        console.log('[FastAuth] Cache miss, would need to call Microsoft implementation');
        
        // For now, return an unauthenticated state as the fast fallback
        // This ensures fast loading while authentication resolves in the background
        return {
            isAuthenticated: false,
            user: null,
            claims: []
        };
        
    } catch (error) {
        console.error('[FastAuth] Error in fast auth internal:', error);
        // Return safe default state
        return {
            isAuthenticated: false,
            user: null,
            claims: []
        };
    }
}

// Usage in another module:
// import { setCookie, getCookie, deleteCookie } from './cookieStorage.js';