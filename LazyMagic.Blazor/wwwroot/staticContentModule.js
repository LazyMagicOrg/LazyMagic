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
// Per-tier asset catalogs, keyed by tier ("system" | "tenancy" | "subtenancy").
//   object -> authoritative { "<lang>/<Group>/": "<version>" } map of the groups
//             that actually exist for this tenant. A declared group absent from
//             it is simply not deployed — not an error.
//   null   -> that tier publishes no catalog (a deployment predating
//             asset-groups.json). Fall back to the per-group version.json probe.
let assetCatalogs = {};
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
        console.warn(`staticContentModule: appPrefix: ${appPrefix}, TEMP_APP_CACHE_NAME: ${TEMP_APP_CACHE_NAME}, APP_CACHE_NAME: ${APP_CACHE_NAME}`);
        assetsUrl = isRunningInServiceWorker
            ? self.location.origin.endsWith('/') ? self.location.origin.slice(0, -1) : self.location.origin
            : appConfig.assetsUrl.endsWith('/') ? appConfig.assetsUrl.slice(0, -1) : appConfig.assetsUrl;
        console.warn(`staticContentModule: assetsUrl: ${assetsUrl}`);
        await loadAssetCatalogs(); // Learn which asset groups actually exist BEFORE registering any
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
        // Server-side version. Prefer the tier catalog fetched once during init: it already
        // carries every group's version, so this collapses N per-group version.json probes
        // into a single request per tier. Falls back to the original probe when the tier
        // publishes no catalog (older deployment).
        const catalogVersion = assetCaches[cacheName] ? assetCaches[cacheName].serverVersion : undefined;
        const version = catalogVersion !== undefined
            ? catalogVersion
            : await readAssetsCacheVersionNoCache(cacheName); // the version on the server
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
 * Normalize one staticContentSettings entry. TWO declaration formats exist in the wild:
 *
 *   legacy: { "/system/base/System/": "PreCache" }             key IS the path
 *   object: { path: "...", cacheType: "...", shared: true }    named fields
 *
 * The original implementation did `Object.entries(entry)[0]` unconditionally. Against the
 * object form that yields ["path", "<the path>"] — so EVERY entry collapsed onto a single
 * key literally named "path", whose cacheType was the last entry's path string. Effects,
 * all silent: readAssetCachesByType("PreCache") matched nothing so PreCache never ran;
 * getCacheName() never matched a real URL so every asset fell through to network forever;
 * and checkAssetCaches probed a nonexistent "{assetsUrl}/pathversion.json". Nothing threw,
 * so the entire asset cache was inert with no error anywhere.
 *
 * Detect the object form explicitly rather than by shape-guessing.
 */
function normalizeAssetEntry(entry) {
    if (entry && typeof entry.path === 'string')
        return { path: entry.path, cacheType: entry.cacheType || 'LazyCache' };
    const [key, value] = Object.entries(entry)[0];
    return { path: key, cacheType: value };
}

/** Declared entries, normalized. Tolerates a missing/!array staticAssets. */
function declaredAssetEntries() {
    if (!settings || !Array.isArray(settings.staticAssets)) return [];
    return settings.staticAssets
        .map(e => { try { return normalizeAssetEntry(e); } catch { return null; } })
        .filter(e => e && typeof e.path === 'string' && e.path.length > 0);
}

/** Leading path segment — the tier. "system" | "tenancy" | "subtenancy". */
function tierOf(path) {
    const p = path.startsWith('/') ? path.slice(1) : path;
    const i = p.indexOf('/');
    return i < 0 ? p : p.substring(0, i);
}

/** Path with the tier stripped — the catalog key. e.g. "base/System/". */
function groupKeyOf(path) {
    const p = path.startsWith('/') ? path.slice(1) : path;
    const i = p.indexOf('/');
    return i < 0 ? '' : p.substring(i + 1);
}

/**
 * Fetch the per-tier asset catalogs (`/{tier}/asset-groups.json`) that declare which
 * asset groups actually exist for this tenant, and at what version.
 *
 * Why this exists: a group declared by the app but absent for this tenant used to be
 * discovered by REQUESTING its version.json and getting a 403/404. That request is
 * issued by the service worker itself, so it bypasses the SW's own fetch handler and
 * all of its noise suppression — the browser logs the failure directly, and no amount
 * of try/catch in JS can silence it. The only fix is to not issue the request. The
 * catalog is what makes absence knowable without asking.
 */
async function loadAssetCatalogs() {
    assetCatalogs = {};
    const tiers = [...new Set(declaredAssetEntries().map(e => tierOf(e.path)))];
    const host = isRunningInServiceWorker ? self.location.hostname : window.location.hostname;
    const isSubtenantHost = host.split('.').length > 2;

    for (const tier of tiers) {
        // /subtenancy/ only routes on a subtenant host; on the apex it is guaranteed to
        // fail (verified: apex 403, {clinic}.host 200). Mirrors the hostParts.Length > 2
        // rule in LzClientConfig.InitializeAsync. Treat as "no groups" without asking.
        if (tier === 'subtenancy' && !isSubtenantHost) { assetCatalogs[tier] = {}; continue; }
        try {
            const url = new URL(`${tier}/asset-groups.json`, assetsUrl + '/').href;
            const resp = await fetch(url, { cache: 'no-cache' });
            if (!resp.ok) { assetCatalogs[tier] = null; continue; } // pre-catalog deployment
            const doc = await resp.json();
            assetCatalogs[tier] = (doc && typeof doc.groups === 'object' && doc.groups) ? doc.groups : {};
        } catch {
            assetCatalogs[tier] = null; // network/parse failure -> fall back, don't disable caching
        }
    }
}

/**
 * Convert the list of static asset urls from the settings into a dictionary.
 * The key of the dictionary is the cacheName and the value is the cacheType.
 * Note that the cacheName is the path to the cache as well.
 */
function makeAssetCaches() {
    try {
        console.debug("makeAssetCaches(), Creating AssetCaches dictionary");
        if (Object.keys(assetCaches).length > 0) return;
        const notDeployed = [];
        for (const entry of declaredAssetEntries()) {
            const tier = tierOf(entry.path);
            const catalog = assetCatalogs[tier];
            const groupKey = groupKeyOf(entry.path);
            // catalog === null  -> tier publishes no catalog: register everything and let
            //                      the per-group probe decide, exactly as before.
            // catalog is object -> authoritative. A declared group that is absent is not
            //                      deployed for this tenant; skipping it is the point.
            if (catalog && !(groupKey in catalog)) { notDeployed.push(entry.path); continue; }
            assetCaches[entry.path] = {
                cacheType: entry.cacheType,
                version: "",
                // undefined when there is no catalog -> readAssetsCache falls back to the probe.
                serverVersion: catalog ? catalog[groupKey] : undefined
            };
        }
        console.log(`AssetCaches dictionary created (${Object.keys(assetCaches).length} active, ${notDeployed.length} declared but not deployed): `, JSON.stringify(assetCaches));
        if (notDeployed.length)
            console.debug("Asset groups declared but not deployed for this tenant (no request issued): ", notDeployed);
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