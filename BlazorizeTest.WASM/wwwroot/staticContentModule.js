/** 
 * staticContentModule.js
 * This module is used to provide caching behavior for both application and static assets.
 * It is imported into the UIFetch module for use in the main thread (Dev, and MAUI) and 
 * in the service worker (PWA).
 * 
 */


console.log("Starting to initialize staticContentModule");
export let assetCaches = {}; // Dictionary of static assets data where key is a tdurl and value is { version: "" }

import settings from './staticContentSettings.js';
import * as appConfigFile from './appConfig.js'; // appConfig
const appPrefix = appConfigFile.appConfig.appPath;

const TEMP_APP_CACHE_NAME = `temp-${appPrefix}-app-cache`;
const APP_CACHE_NAME = `${appPrefix}-app-cache`;
const isRunningInServiceWorker = 'ServiceWorkerGlobalScope' in self && self instanceof ServiceWorkerGlobalScope;

// The assetsUrl is acquired differently for service workers and the main thread. This assetsUrl is used 
// for constructing requests to the server for static assets (but not app assets).
const assetsUrl = isRunningInServiceWorker
    ? self.location.origin.endsWith('/') ? self.location.origin.slice(0, -1) : self.location.origin
    : window.appConfig.assetsUrl.endsWith('/') ? window.appConfig.assetsUrl.slice(0, -1) : window.appConfig.assetsUrl;

makeAssetCaches();
let fetchingPrefetchCaches = false;
/**
 * Cache application assets. This is called when the service worker is installed.
 * Only called from a service worker.
 * @param {any} assetRequests
 */
export async function cacheApplicationAssets(assetsRequests) {
    await loadCache(assetsRequests, TEMP_APP_CACHE_NAME); // CALL THE MODULE
}

/**
 * Active the application cache. This is called when the service worker is activated. 
 * Only called from a service worker.
 */
export async function activateApplicationCache() {
    await copyCache(TEMP_APP_CACHE_NAME, APP_CACHE_NAME);
}   

/**
 * Get the current assets cache, if any, for the given URL.
 * Called from the fetch override to test if a 
 * specified url can be served from a cache.
 * @param {any} url
 * @returns null or a cache name
 */
export async function getCacheName(url) {
    let cacheName = null;   
    for (const tdurl of Object.keys(assetCaches)) {
        if (url.includes(tdurl)) {
            cacheName = tdurl; // assets cache
            break;
        }
    }
    return cacheName;
}

/**
 * Check the cache for the specified request and return the response if found.
 * @param {any} cacheName
 * @param {any} requeststat
 * @returns
 */
export async function getCachedResponse(cacheName, request) {
    const cache = await caches.open(cacheName);
    return await cache.match(request);
}

/**
 * Copy the contents of one cache to another. 
 * We use this when loading app assets to ensure a self-consistent collection of cached 
 * items. This is particularly important when updating the application code. 
 * @param {string} sourceCache - The name of the temporary cache.
 * @param {string} targetCache - The name of the permanent cache.
 */
export async function copyCache(sourceCache, targetCache) {
    console.debug(`Copying cache: ${sourceCache} to ${targetCache}`);
    const cacheNames = await caches.keys();

    await caches.delete(targetCache);

    // Open both the temporary and the new cache
    const tempCache = await caches.open(sourceCache);
    const cache = await caches.open(targetCache);

    // Get all the requests from the temporary cache and put them into the new cache
    const tempCacheKeys = await tempCache.keys();
    await Promise.all(
        tempCacheKeys.map(async request => {
            const response = await tempCache.match(request);
            await cache.put(request, response);
        })
    );

    // Delete the temporary cache
    await caches.delete(sourceCache);
}
/**
 * Loads requests/responses into cache. 
 * @param {Request[]} cacheRequests - Array of requests to be cached.
 * @param {string} cacheName - The name of the cache to load assets into.
 */
export async function loadCache(cacheRequests, cacheName) {
    const cache = await caches.open(cacheName);

    // Use Promise.allSettled to handle each request individually
    const results = await Promise.allSettled(cacheRequests.map(request => fetch(request)));
    let successCount = 0;
    let failureCount = 0;

    for (const result of results) {
        if (result.status === 'fulfilled') {
            try {
                await cache.put(result.value.url, result.value);
                successCount++;
            } catch (error) {
                console.error(`Failed to cache (${cacheName} ):`, result.value.url, error);
                failureCount++;
            }
        } else {
            console.error(`Fetch failed (${cacheName}):`, result.reason);
            failureCount++;
        }
    }
    console.debug(`Loaded ${cacheName } cached: ${successCount} failed: ${failureCount}`);
}
/*
 * Fetches caches of type PreCache.
 */
export async function readAssetCachesByType(cacheType) {
    try {
        fetchingPrefetchCaches = true;
        for (const cacheName of Object.keys(assetCaches)) {
            if (assetCaches[cacheName].cacheType === cacheType)
                await readAssetsCache(cacheName); // reads assets into temporary cache
        }
        return;
    } catch (error) {
        console.error(`Error fetching static assets list: `, error);
        return;
    } finally {
        fetchingPrefetchCaches = false;
    }
}

/**
 * Reads asset cache and update the cache version.
 * @param {string} cacheName - The URL of the asset cache.
 */
export async function readAssetsCache(cacheName) {
    try {
        // Read the asset cache version
        const version = await readAssetsCacheVersionNoCache(cacheName); // the version on the server
        const currentVersion = await readAssetsCacheVersionCached(cacheName); // the version currently in the cache
        assetCaches[cacheName].version = currentVersion;  // set the current version for use by the lazyLoadAssetCache function
        if(currentVersion === version) return; // nothing to do

        let assetsManifestResponse;
        try { assetsManifestResponse = await fetch(new Request(cacheName + "assets-manifest.json", {cache: 'no-cache'})); }
        catch { throw new Error(`fetching ${cacheName}assets-manifest.json for version: ${version}`); }

        if (assetsManifestResponse.ok) {
            let assetsManifest;
            try { assetsManifest = await assetsManifestResponse.json(); }
            catch { throw new Error("parsing assets-manifest.json"); }

            assetCaches[cacheName].version = version;

            let assetsRequests;
            try {
                assetsRequests = assetsManifest.map(asset => {
                    const url = new URL(asset.url, assetsUrl).href;
                    //console.log(`asset.url: ${asset.url}, url: ${url}`);
                    return new Request(url, { cache: 'no-cache' });
                });
                // The version.json file is not in the assets-manifest.json file because its value is calculated based on the
                // content of the assets-manifest.json file. We need to add it to the list of requests so we have a persisent
                // record of the version of the cache.
                const versionJsonRequest = new Request(new URL(cacheName + "version.json", assetsUrl).href, { cache: 'no-cache' });
                assetsRequests.push(new Request(versionJsonRequest, { cache: 'no-cache' }));
            }
            catch { throw new Error("mapping assets-manifest.json"); }

            try { await loadCache(assetsRequests, cacheName); }
            catch { throw new Error("loading cache"); }
        }

    } catch (error) {
        console.error(`Error: reading cache ${cacheName} ${error}`);
    }
}
/**
 * Reads the version of an asset cache from the cache
 * @param {string} cacheName - The URL of the asset cache.
 */
export async function readAssetsCacheVersionCached(cacheName) {
    try {
        const url = new URL(cacheName + 'version.json', assetsUrl);
        const request = new Request(url);
        const cache = await caches.open(cacheName);
        const isInCache = cache ? await cache.match(request) : false;
        if (!isInCache) return "";

        let versionResponse = await fetch(url, {
            method: 'GET'
        });
        if (versionResponse.ok) { 
            let versionObj = await versionResponse.json();
            let version = versionObj.version;
            //console.debug(`Asset cache ${cacheName} version: ${version}`);
            return version;
        }
        return "";

    } catch (error) {
        console.error(`Error reading asset cache version from cache: ${cacheName}`, error);
    }
}

/**
 * Reads the version of an asset cache from the server
 * @param {string} cacheName - The URL of the asset cache.
 */
export async function readAssetsCacheVersionNoCache(cacheName) { 
    try {
        // NOTE: In a service worker, this fetch will circument the fetch event handler,
        // On the UI thread, this fetch will be intercepted by the window.fetch override.
        const url = new URL(cacheName + "version.json", assetsUrl).href;
        let versionResponse = await fetch(url, {
            method: 'GET',
            cache: 'no-cache' // Ignore the local cache and go to the server
        });
        let versionObj = await versionResponse.json();
        let version = versionObj.version;
        return version;

    } catch (error) {
        console.error(`Error reading asset cache version: ${cacheName}`, error);
    }
}
/**
 * Checks and updates asset caches if necessary.
 * Fetch the version.json for each active cache source and load the new 
 * cache content if the version has changed. The version.json file 
 * is tiny and this makes it very fast to do this check. The version 
 * is also available in the assets-manifest.json file but that file 
 * can be large and we don't want to download it unless we need to.
 */
export async function checkAssetCaches() {
    var updating = false;
    try {
        await sendMessage('AssetDataCheckStarted', 'Checking assets data cache.');
        for (const cacheName of Object.keys(assetCaches)) {
                await readAssetsCache(cacheName);
        }
        await sendMessage('AssetDataCheckComplete', 'Assets data cache check complete.')
    } catch (error) {
        console.error('Error checking asset cache', error);
    } finally {
        if (updating) {
            console.debug('Assets data update complete.');
        }
        else
            console.debug('Assets data cache check complete.');
    }
}
/**
 * Lazily loads an asset cache. These are asset caches of type "LazyCache".
 * This routine will also load a cache of type "PreCache" if it is not already loaded.
 * A cache can be loaded lazily when it is needed. This is useful for the
 * initial load of the application when we don't want to load all the assets.
 * Note that this routine uses the assetCaches[key].version to determine if the cache
 * is loaded. readAssetsCache updates the version when the cache is loaded. Note 
 * that the assetCaches[key].version values are initialized to "" whenever the 
 * app is reloaded. This means the first time the lazyLoadAssetCache is called, the
 * readAssetsCache will be called, even if the cache is loaded in the browser's 
 * caches. readAssetsCach will update the assetCaches[key].version value to the
 * current version of the cache and return without trying to load the cache again.
 * @param {string} cacheName - The name of the asset cache to load.
 */
export async function lazyLoadAssetCache(cacheName) {
    // Load the cache if it is not already loaded 
    try {
        if (fetchingPrefetchCaches) return;
        const cacheItem = assetCaches[cacheName];
        if (cacheItem.version === "") {
            await readAssetsCache(cacheName);
        }
    } catch {
        console.warn(`lazyLoadCache[${cacheName}] not found`);
    }
}

/* PRIVATE FUNCTIONS */
/**
 * Convert the list of static asset urls from the settings into a dictionary.
 * The key of the dictionary is the cacheName and the value is the cacheType.
 * Note that the cacheName is the path to the cache as well.
 */
function makeAssetCaches() {
    try {
        console.debug("makeAssetCaches(), Creating AssetCaches dictionary");
        if (Object.keys(assetCaches).length > 0) return;
        //const _appPrefix = appPrefix.endsWith('/') ? appPrefix.slice(0,-1) : appPrefix;
        for (const cacheName of settings.staticAssets) {
            const [key, value] = Object.entries(cacheName)[0];
            assetCaches[key] = {
                cacheType: value,
                version: ""
            };
        }
    } catch (error) {
        console.error(`Error creating AssetCaches dictionary `, error);
    }
}


/**
 * Sends a message to all clients.
 * @param {string} action - The action to be performed.
 * @param {string} message - The message to be sent.
 */
async function sendMessage(action, info) {
    const clients = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    if (clients) {
        for (const client of clients) {
            client.postMessage({
                action: action,
                info: info
            });
        }
    }
}
console.log("Finished initializing staticContentModule");