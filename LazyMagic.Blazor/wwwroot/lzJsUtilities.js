
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
    // We can't use reload() because the current browser URL may include a Blazor "page" (component)
    // and that would cause a 404. Example: /myapp/HomePage.
    // Also, the reload behavior is different for reload in dev (localhost) and
    // reload from a non-dev server.
    const isDev = window.location.hostname.includes('localhost');
    const baseHrefElement = document.querySelector('base');
    const appPath = new URL(baseHrefElement.href).pathname;
    if (isDev) {
        location.href = new URL("/", self.location.origin);
    }
    else {
        console.log("reload appPath:" + appPath);
        location.href = new URL(appPath, self.location.origin);
    }
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

// Usage in another module:
// import { setCookie, getCookie, deleteCookie } from './cookieStorage.js';