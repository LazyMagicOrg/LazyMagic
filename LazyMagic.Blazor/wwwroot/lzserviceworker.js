
//test02
/*
This service-worker is used when the app is running from a remote server.
In addition to the minimum set of service-worker features, this implementation 
includes support for:
- Integration with our static asset caching module.
- Redirection to the base URL when the app is navigated to a different URL.
- Graceful application updates.
*/

// Standard logging utility for service worker
function logSW(methodName, message, level = 'info') {
    const timestamp = new Date().toISOString().substr(11, 12); // HH:mm:ss.fff format
    console[level](`[${methodName}][${timestamp}] ${message}`);
}

logSW('ServiceWorker', 'Loading service worker script', 'warn');

// In the service worker file (e.g., service-worker.published.js)
// First, import the config files using static imports

let version = '';

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

// Patterns the SW's install fan-out includes when populating the offline
// app cache. Match against URLs in service-worker-assets.js. Anything
// matching is fetched + put into APP_CACHE_NAME at install time so the
// PWA can serve the file when the browser is offline.
//
// .wasm matters specifically for .NET 8+ Blazor: assemblies (including
// lazy-loaded component assemblies like SetsCmp.HASH.wasm and
// Snap3DCmp.HASH.wasm) ship with the .wasm extension, not .blat / .dll
// as in earlier .NET versions. Without .wasm in this list the SW never
// caches the framework, and "PWA offline" depends entirely on the
// browser HTTP cache holding the right URLs — which is fragile under
// storage pressure or after eviction. Including .wasm makes offline
// support work reliably on first install.
//
// .blat and .dat preserved for backwards compatibility with
// pre-.NET-8 consumers that may still use those extensions.
const offlineAssetsInclude = [/\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.svg$/, /\.woff2$/, /\.wasm$/];
const offlineAssetsExclude = [/GoogleTag\.js$/]; // Excluding GoogleTag.js because ad blockers block it and this will cause the caching to fail.

self.addEventListener('message', async event => {
    switch (event.data.action) {
        case 'checkForNewAssetData':
            console.log('service worker checkForNewAssetData.');
            await sendMessage('AssetDataCheckStarted', "");
            await self.staticContentModule.checkAssetCaches();
            break;
        case 'loadStaticAssets':
            console.log('service worker loadStaticAssets.');
            await caches.keys().then(cacheNames => {
                console.log('Available caches:', cacheNames);
            });
            await self.staticContentModule.readAssetCachesByType("PreCache");
            break;
        case 'listCaches':
            caches.keys().then(cacheNames => {
                console.log('Available caches:', cacheNames);
            });
            break;
        default:
            break;
    }
    if (event.data.type === 'SET_ASSET_HOST_URL') {
        try {
            self.assetHostUrl = new URL(event.data.url);
        } catch (error) {
            console.error(error);
        }
    }
});


self.addEventListener('install', event => {
    logSW('install', 'Service worker installing...');
    event.waitUntil(
        (async () => {
            try {
                // Let the main UI thread know there is a new service worker installing
                //const clients = await navigator.serviceWorker?.controller.clients.matchAll({ type: 'window' });
                await sendMessage('ServiceWorkerUpdateStarted', 'A new version is being installed.');

                // Fetch and cache all matching items from the assets manifest into the temp cache
                version = self.assetsManifest.version;
                console.log('assetRequests.length():' + self.assetsManifest.assets.length + ", version:" + version);
                const assetsRequests = self.assetsManifest.assets
                    .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
                    .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
                    .map(asset => {
                        // Make sure to use the full absolute URL for the request,
                        //otherwise the request may fail when the service worker is not at the root.
                        if (!asset.url.startsWith('/') )
                            asset.url = '/' + asset.url;
                        // cache: 'default' lets the browser reuse the
                        // HTTP cache copy that the WASM cold-boot just
                        // populated. This avoids the "double-fetch on
                        // first visit" pattern: the runtime pulls every
                        // asset fresh during boot, then the SW install
                        // would refetch the same set with cache:'no-cache'
                        // — round-tripping ~2-3 MB of non-wasm assets
                        // post-TTI for nothing.
                        //
                        // Safety: server-side cache-control headers
                        // already gate freshness correctly. Hashed
                        // assets (/_framework/*.HASH.{js,wasm,dat}) are
                        // public, max-age=1y, immutable — HTTP cache
                        // hits are always correct. Unhashed assets
                        // (index.html, service-worker-assets.js, /config,
                        // etc.) are served with no-cache, must-revalidate
                        // so the browser still revalidates them on
                        // every install.
                        return new Request(asset.url, {
                            //integrity: asset.hash,
                            cache: 'default'
                        });
                    });

                await self.staticContentModule.cacheApplicationAssets(assetsRequests);
                self.skipWaiting(); // Activate the new service worker immediately
            } catch (error) {
                console.error('Error during service worker install:', error);
            }

        })()
    );
});

// This event listener is used to make sure all existing clients are claimed by the new service worker
self.addEventListener('activate', (event) => {
    logSW('activate', 'Service worker activating');
    event.waitUntil((async () => {
        await self.staticContentModule.activateApplicationCache();
        await self.clients.claim();
        await self.staticContentModule.checkAssetCaches();
        await sendMessage("ServiceWorkerUpdateCompleted", "The new version has been installed.");
    })());
});

self.addEventListener('fetch', event => {
    // ────────────────────────────────────────────────────────────────
    // Special-path handling — runs BEFORE the main event.respondWith.
    // ────────────────────────────────────────────────────────────────
    // Two surfaces require special care:
    //
    //   1. Auth + nonSpa paths. /auth/, /oauth2/, /authentication/
    //      302 to Cognito's Hosted UI; returning a redirected
    //      Response from event.respondWith fails navigation per
    //      spec. Consumer-declared nonSpaPaths are similar (e.g.
    //      bypassing SW for app-specific endpoints). For these, we
    //      MUST early-return WITHOUT calling event.respondWith so
    //      the browser handles the fetch natively.
    //
    //   2. SRI + Content-Encoding (Chromium pipeline bug). Blazor
    //      adds `integrity` to its hashed `_framework/*` requests.
    //      When CloudFront serves them with Content-Encoding
    //      (precompressed sibling OR edge auto-compression),
    //      Chromium has a pipeline-ordering bug where
    //      event.respondWith(fetch(req)) computes the SRI digest
    //      against the ENCODED body instead of the decoded one —
    //      failing SRI even though server bytes are correct. See
    //      Platform/AssetDeliveryReview.md (BCProjects).
    //
    //      Prior fix (3.0.9): early-return on hasIntegrity to let
    //      the browser fetch natively, which decodes first then
    //      checks SRI. That worked online but BROKE PWA offline
    //      because the browser's native fetch can't reach the
    //      network when offline, and never consults the SW's cache.
    //
    //      Current fix: serve integrity'd requests from cache via
    //      event.respondWith. cache.put stores the decoded Response
    //      (Content-Encoding stripped per fetch spec), so a cached
    //      Response has no Content-Encoding header and SRI computes
    //      against the right (decoded) bytes — no pipeline bug.
    //      Cache miss falls back to fetch(event.request); on miss
    //      we accept the SRI-bug risk to keep the cache-hit path
    //      offline-capable. Misses are rare (cache eviction or first
    //      controller install) and on first install the SW isn't
    //      controlling yet, so the browser handles boot fetches
    //      natively without hitting this branch at all.
    //
    // SCOPING NOTE: the 3.0.7 version excluded broad path prefixes
    // (/_framework/, /_content/) from SW handling. That over-fired:
    //   - /_content/* files don't have integrity attributes — the
    //     SRI bug doesn't apply. Skipping them broke PWA offline.
    //   - Non-hashed /_framework/* files (blazor.webassembly.js,
    //     dotnet.js, blazor.boot.json) also lack integrity — same
    //     story.
    // The narrower correct conditions are below.
    const reqPath = new URL(event.request.url).pathname;
    const consumerNonSpaPaths = Array.isArray(self.appConfig?.nonSpaPaths)
        ? self.appConfig.nonSpaPaths
        : [];
    const hasIntegrity = !!event.request.integrity;
    const isAuthPath = reqPath.includes('/authentication/') ||
                       reqPath.startsWith('/auth/') ||
                       reqPath.startsWith('/oauth2/');
    const isNonSpa = consumerNonSpaPaths.some(p => reqPath.startsWith(p));

    // Auth + nonSpa: bare return, browser handles natively (avoids the
    // redirected-Response navigation-failure spec corner).
    if (isAuthPath || isNonSpa) {
        return;
    }

    // Integrity'd requests: cache-first via respondWith. Cache hits avoid
    // the Chromium SRI/Content-Encoding pipeline bug because the cached
    // Response has no Content-Encoding header (stripped at cache.put time).
    // Cache misses fall back to fetch — accepts the SRI-bug risk on the
    // rare miss path to gain offline support on the common hit path.
    if (hasIntegrity) {
        event.respondWith((async () => {
            try {
                const cached = await caches.match(event.request, {
                    ignoreSearch: true,
                    ignoreVary: true
                });
                if (cached) {
                    console.debug('[SW] integrity cache hit:', event.request.url);
                    return cached;
                }
            } catch (err) {
                console.error('[SW] integrity cache lookup error:', event.request.url, err);
            }
            // Cache miss: bail to network. This path retains the SRI-bug
            // risk if CloudFront serves Content-Encoding. In practice,
            // misses occur on first-controller install (when the browser
            // already has the asset in HTTP cache) or after eviction
            // (where a stale-then-refresh is acceptable).
            console.debug('[SW] integrity cache miss; fetching:', event.request.url);
            return fetch(event.request);
        })());
        return;
    }

    event.respondWith((async () => {
        let url = new URL(event.request.url);
        let path = url.pathname;
        const isOnline = await self.connectivityService.isReallyOnline();
        let request = event.request;

        // Belt-and-suspenders: integrity-bearing requests are routed above.
        // This branch should be unreachable; if reached, fall through to
        // network rather than risking the cache-first pipeline misbehaving
        // for an SRI-attributed request.
        if (event.request.integrity) {
            return fetch(event.request);
        }

        // Handle navigation requests for Blazor/SPA applications
        if (request.mode === 'navigate') {
            console.debug('Handling navigation request for:', path);

            // Check if we're at the exact app root path
            const isAppRoot = path === self.appConfig.appPath ||
                            path === self.appConfig.appPath + '/';

            if (isAppRoot) {
                // For the app root, we need to explicitly request index.html
                // because that's how it's stored in the cache
                console.log('App root requested, changing to index.html for cache lookup');
                const newPath = self.appConfig.appPath + (self.appConfig.appPath.endsWith('/') ? '' : '/') + 'index.html';
                url.pathname = newPath;
                request = new Request(url.toString(), {
                    method: 'GET',
                    headers: event.request.headers,
                    mode: 'same-origin',
                    credentials: event.request.credentials,
                    cache: event.request.cache
                });
            } else {
                // For other paths, check if it's a client-side route that needs redirection
                const hasFileExtension = /\.[a-zA-Z0-9]+$/.test(path);

                // If it's a navigation request without a file extension,
                // serve the app root's index.html so Blazor can handle client-side routing.
                // We rewrite to appPath + index.html (NOT just appPath) for two reasons:
                //   1. The cache stores the document under /index.html, so a lookup
                //      for /index.html hits; a lookup for / would miss and fall through
                //      to a network fetch.
                //   2. A network fetch for the app root may be 302-redirected by edge
                //      logic (e.g. front-door gates that send fresh visits to a static
                //      home page). fetch() follows that redirect, producing a Response
                //      with `redirected: true`. Per the SW spec, returning a redirected
                //      Response from a navigation fetch handler causes the browser to
                //      fail the navigation (Chrome: net::ERR_FAILED). Fetching the
                //      index.html file directly bypasses any directory-level redirects.
                // (Special paths were already short-circuited above.)
                if (!hasFileExtension) {
                    console.log('SPA route detected, rewriting to app root index.html:', path);
                    const newPath = self.appConfig.appPath + (self.appConfig.appPath.endsWith('/') ? '' : '/') + 'index.html';
                    url.pathname = newPath;
                    request = new Request(url.toString(), {
                        method: 'GET',
                        headers: event.request.headers,
                        mode: 'same-origin',
                        credentials: event.request.credentials,
                        cache: event.request.cache
                    });
                }
            }
        }
        
        // Blazor issues fetch requests with "no-cache" for some items and this breaks PWA offline support.
        // So, if we are offline and the request is "no-cache", we change it to "default" to allow the cache to be used.
        if (!isOnline && request.cache === "no-cache") {
            await sendMessage('no-cache detected, switching to default', ' no-cache request. method:' + request.method + ', url:' + request.url);
            url = new URL(event.request.url);
            request = new Request(url.toString(), {
                method: 'GET',
                headers: event.request.headers,
                mode: 'same-origin',
                credentials: event.request.credentials,
                cache: 'default'
            });
        }

        if (request.method === 'GET' && request.cache !== "no-cache") {
            try {

                // examine the request path and determine if this may be a cached asset
                const cacheName = await self.staticContentModule.getCacheName(request.url);
                if (cacheName) {
                    await self.staticContentModule.lazyLoadAssetCache(cacheName);
                    const cachedResponse = await self.staticContentModule.getCachedResponse(cacheName, request);
                    console.debug('Cache lookup for request:', request.url, 'cacheName:', cacheName, 'found:', !!cachedResponse);
                    if (cachedResponse instanceof Response) {
                        return cachedResponse;
                    } else {

                        if (!isOnline) {
                            return new Response(null, { status: 204, statusText: 'offline' });
                        }

                        // Item is not in cache so just fetch it. We don't add it to the cache here because of
                        // thread safety issues. This is not a performance issue becuase the browser's native
                        // cache will have the item, for the cache load to use, when the cache load catches up.
                        //await sendMessage('CacheMiss', JSON.stringify(request));
                        console.debug('Cache miss for request:', request.url, JSON.stringify(request), 'cacheName:', cacheName);
                        return fetch(request)
                            .then(response => {
                                // Pass real status through. Previously this branch
                                // substituted a 204 'no content' for any non-2xx,
                                // which lied about the failure: callers expecting
                                // JSON would parse the empty body and crash with
                                // confusing errors, and any 4xx looked the same
                                // to any code reading response.status. Pass-through
                                // is safe — the browser cache won't store 4xx
                                // either way — and lets callers handle real
                                // failure modes (e.g. an Images.json file that's
                                // missing for one tenant in a sparse Tenancies/
                                // tree should look like a 403 to the consumer,
                                // not a phantom 204).
                                if (!response.ok) {
                                    console.error('Cache-miss fetch returned HTTP ' + response.status + ' for', response.url);
                                }
                                return response;
                            })
                            .catch(error => {
                                // Genuine network failure (DNS, abort, opaque
                                // CORS error). 504 'Gateway Timeout' is more
                                // honest than 204 — the resource may exist;
                                // we just couldn't reach it. Lets callers
                                // distinguish "endpoint refused" from
                                // "endpoint unreachable."
                                console.error('Cache-miss network error for', request.url, error);
                                return new Response(null, { status: 504, statusText: 'Gateway Timeout' });
                            })
                    }
                }
                else {
                    console.warn('No cache associated/available for url:' + request.url);
                }
            }
            catch (error) {
                // Defensive: if the cache logic above (getCacheName /
                // lazyLoadAssetCache / getCachedResponse) throws — which
                // can happen when staticContentSettings drifts out of
                // sync with the actual deployed Tenancies/ tree — fall
                // through to a plain network fetch instead of returning
                // a synthetic 500. Returning 500 here for unrelated
                // requests breaks the page; pass-through preserves the
                // happy path even when our cache layer is degraded.
                console.error('SW cache layer error; passing through to network for', event.request.url, error);
                if (isOnline) {
                    try { return await fetch(event.request); }
                    catch (passthroughError) {
                        console.error('Pass-through network error for', event.request.url, passthroughError);
                        return new Response(null, { status: 504, statusText: 'Gateway Timeout' });
                    }
                }
                return new Response(null, { status: 504, statusText: 'Gateway Timeout' });
            }
        }
        if (isOnline)
            return fetch(request)
                .then(response => {
                    if (!response.ok) {
                        console.error('HTTP error:', response.url, response.status);
                        // Return the original response to preserve status code
                        return response;
                    }
                    return response;
                })
                .catch(error => {
                    console.error('Network error:', request.url, error.message);
                    // Return a proper error response
                    return new Response('Network error occurred', {
                        status: 503,
                        statusText: 'Service Unavailable'
                    });
                });
        else {
            console.warn('Offline fetch request failure for url:' + request.url);
            return new Response(null, { status: 204, statusText: 'offline' });
        }
    })()
    );
});

