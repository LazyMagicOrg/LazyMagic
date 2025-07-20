/** 
 * staticContentModule.js
 * This module is used to provide caching behavior for both application and static assets.
 * It is imported into the UIFetch module for use in the main thread (Dev, and MAUI) and 
 * in the service worker (PWA).
 * 
 */

console.log("Starting to initialize staticContentModule");
export let assetCaches = {}; // Dictionary of static assets data where key is a tdurl and value is { version: "" }
let assetCachesInitialized = false; // Flag to indicate if the asset caches have been initialized
let assetCachesInitializeFailed = false; // Flag to indicate if the asset caches initialization failed
let appConfig;
let settings;
let appPrefix;
let assetsUrl;
let fetchingPrefetchCaches = false;
let TEMP_APP_CACHE_NAME;
let APP_CACHE_NAME;
const isRunningInServiceWorker = 'ServiceWorkerGlobalScope' in self && self instanceof ServiceWorkerGlobalScope;
const cacheOptions = {
    ignoreSearch: true,    // Ignore query string
    //ignoreMethod: true,    // Ignore HTTP method
    ignoreVary: true       // Ignore Vary headers
};

async function initializeModule() {
    if (assetCachesInitialized) return true; // Already initialized)
    if (assetCachesInitializeFailed) return false; // Initialization failed previously
    assetCachesInitializeFailed = true;
    try {
        appConfig = isRunningInServiceWorker ? self.appConfig : window.appConfig;
        settings = isRunningInServiceWorker ? self.settings : window.settings;

        appPrefix = appConfig.appPath;
        TEMP_APP_CACHE_NAME = `temp-${appPrefix}-app-cache`;
        APP_CACHE_NAME = `${appPrefix}-app-cache`;

        assetsUrl = isRunningInServiceWorker
            ? self.location.origin.endsWith('/') ? self.location.origin.slice(0, -1) : self.location.origin
            : appConfig.assetsUrl.endsWith('/') ? appConfig.assetsUrl.slice(0, -1) : appConfig.assetsUrl;
        makeAssetCaches(); // Initialize the asset caches dictionary

    } catch (error) {
        console.error(`Error initializing staticContentModule: `, error);
        return false;
    }
    assetCachesInitializeFailed = false;
    assetCachesInitialized = true; // Set the flag to indicate that the asset caches have been initialized
    return true;
}


/**
 * Cache application assets. This is called when the service worker is installed.
 * Only called from a service worker.
 * @param {any} assetRequests
 */
export async function cacheApplicationAssets(assetsRequests) {
    const intilized = await initializeModule();
    if (!intilized) return;
    console.debug(`Caching application assets: ${assetsRequests.length}`);
    await loadCache(assetsRequests, TEMP_APP_CACHE_NAME); // CALL THE MODULE
}

/**
 * Active the application cache. This is called when the service worker is activated. 
 * Only called from a service worker.
 */
export async function activateApplicationCache() {
    const intilized = await initializeModule();
    if (!intilized) return;
    console.debug(`Activating application cache`);
    await copyCache(TEMP_APP_CACHE_NAME, APP_CACHE_NAME);
}

/**
 * Get the current assets cache, if any, for the given URL.
 * Called from the fetch override to test if a 
 * specified url can be served from a cache.
 * @param {any} url
 * @returns null or a cache name
 */
//export async function getCacheName(url) {
//    console.debug(`getCacheName(${url})`);
//    let cacheName = null;
//    // note special key {appPrefix}-app-cache is used for the application cache
//    for (const key of Object.keys(assetCaches)) {
//        if (url.includes(key)) {
//            cacheName = key; // assets cache
//            break;
//        }
//    }
    
//    if (cacheName === null && url.startsWith(appPrefix + '/')) {
//        console.warn('checking ' + url + ' for application cache.');
//        // If no asset cache is found, use the application cache
//        cacheName = APP_CACHE_NAME; // application cache
//    }

//    return cacheName;
//}

export async function getCacheName(url) {
    const intilized = await initializeModule();
    if (!intilized) return;
    const urlObj = new URL(url);
    const path = urlObj.pathname;
    // console.debug('getCacheName: ' + path);
    let cacheName = null;
    // note special key {appPrefix}-app-cache is used for the application cache
    for (const key of Object.keys(assetCaches)) {
        if (path.includes(key)) {
            cacheName = key; // assets cache
            break;
        }
    }
    // appPrefix includes a beginning slash and ending slash
    if (cacheName === null && path.startsWith(appPrefix))
        cacheName = APP_CACHE_NAME; // application cache

    return cacheName;
}

/**
 * Check the cache for the specified request and return the response if found.
 * @param {any} cacheName
 * @param {any} requeststat
 * @returns
 */
export async function getCachedResponse(cacheName, request) {
    const intilized = await initializeModule();
    if (!intilized) return;
    console.debug(`getCachedResponse(${cacheName}, ${request.url})`);
    const cache = await caches.open(cacheName);

    return await cache.match(request, cacheOptions);
}

/**
 * Copy the contents of one cache to another. 
 * We use this when loading app assets to ensure a self-consistent collection of cached 
 * items. This is particularly important when updating the application code. 
 * @param {string} sourceCache - The name of the temporary cache.
 * @param {string} targetCache - The name of the permanent cache.
 */
export async function copyCache(sourceCache, targetCache) {
    const intilized = await initializeModule();
    if (!intilized) return;
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
    const intilized = await initializeModule();
    if (!intilized) return;
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
    console.debug(`Loaded ${cacheName} cached: ${successCount} failed: ${failureCount}`);
}
/*
 * Fetches caches of type PreCache.
 */
export async function readAssetCachesByType(cacheType) {
    try {
        const intilized = await initializeModule();
        if (!intilized) return;
        console.debug(`Fetching static assets list for cacheType: ${cacheType}`);
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
        const intilized = await initializeModule();
        if (!intilized) return;
        console.debug(`Reading cache ${cacheName}`);    
        // Read the asset cache version
        //console.log(`Reading cache ${cacheName}`);
        const currentVersion = await readAssetsCacheVersionCached(cacheName); // the version currently in the cache
        const version = await readAssetsCacheVersionNoCache(cacheName); // the version on the server
        assetCaches[cacheName].version = currentVersion;  // set the current version for use by the lazyLoadAssetCache function
        if (currentVersion === version) return; // nothing to do

        let assetsManifestResponse;
        try { assetsManifestResponse = await fetch(new Request(cacheName + "assets-manifest.json", { cache: 'no-cache' })); }
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
                // The version is not in the assets-manifest.json file because its value is calculated based on the
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
        const intilized = await initializeModule();
        if (!intilized) return;
        console.debug(`Reading asset cache version from cache: ${cacheName}`);
        const url = new URL(cacheName + 'version.json', assetsUrl);
        const request = new Request(url);
        const cache = await caches.open(cacheName);
        const response = await cache.match(request, cacheOptions);
        if (!response) return "";
        let versionObj = await response.json();
        let version = versionObj.version;
        return version;

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
        const intilized = await initializeModule();
        if (!intilized) return;
        console.debug(`Reading asset cache version from server: ${cacheName}`);
        // NOTE: In a service worker, this fetch will circument the fetch event handler,
        // On the UI thread, this fetch will be intercepted by the window.fetch override.
        const url = new URL(cacheName + "version.json", assetsUrl).href;
        //console.log(`Reading asset cache version from server: ${cacheName} url: ${url}`);
        let versionResponse = await fetch(url, {
            method: 'GET',
            cache: 'no-cache' // Ignore the local cache and go to the server
        });
            //.then(response => {
            //    if (!response.ok)
            //        return new Response(null, { status: 404, statusText: 'not found' });
            //    return response;
            //})
            //.catch(error => {
            //    return new Response(null, { status: 404, statusText: 'not found' });
            //});
        if (!versionResponse.ok)
            return "";
        let versionObj = await versionResponse.json();
        let version = versionObj.version;
        return version;

    } catch (error) {
        // This should never fire. 
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
        const intilized = await initializeModule();
        if (!intilized) return;
        console.debug('Checking asset cache');
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
        const intilized = await initializeModule();
        if (!intilized) return;
        if (fetchingPrefetchCaches) return;
        if (cacheName === APP_CACHE_NAME) return;
        console.debug(`Lazy loading cache ${cacheName}`);
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
        if (settings.staticAssets)
            for (const cacheName of settings.staticAssets) {
                const [key, value] = Object.entries(cacheName)[0];
                assetCaches[key] = {
                    cacheType: value,
                    version: ""
                };
            }
        console.log("AssetCaches dictionary created: ", JSON.stringify(assetCaches));
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