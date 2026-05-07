
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
    // Special-path early-return — runs BEFORE event.respondWith.
    // ────────────────────────────────────────────────────────────────
    // For paths we don't actually want to intermediate, the SW MUST
    // return without calling event.respondWith. Letting the browser
    // handle the fetch natively avoids two distinct failure modes
    // that previously surfaced when we wrapped a pass-through
    // `fetch(event.request)` in `event.respondWith`:
    //
    //   1. SRI + Content-Encoding interaction (the immediate trigger).
    //      Blazor adds `integrity` to its `_framework/*` requests.
    //      CFRequest.js rewrites `_framework/X.wasm` → `X.wasm.br`
    //      based on Accept-Encoding, so the response carries
    //      `Content-Encoding: br`. Per spec, integrity is checked
    //      against the DECODED body. But `event.respondWith(fetch(req))`
    //      hits a long-standing Chromium pipeline-ordering issue
    //      where the SRI digest is computed against the *encoded*
    //      body — failing the check even though server bytes are
    //      correct. Symptom: "Failed to find a valid digest in the
    //      'integrity' attribute … with computed SHA-256 integrity X.
    //      The resource has been blocked." Native fetch (no SW
    //      interception) decodes first, then checks SRI, and works.
    //
    //   2. Cross-origin redirect mishandling. Auth-related paths
    //      (/auth/, /oauth2/, /authentication/) frequently 302 to
    //      Cognito's Hosted UI. Returning a redirected Response
    //      from event.respondWith fails the navigation per spec.
    //
    // Special paths the SW must NOT touch:
    //   /authentication/  — Blazor's OIDC RemoteAuthenticatorView routes
    //   /_framework/      — Blazor WASM runtime assets (SRI + br)
    //   /_content/        — Razor static-asset library content
    //   /auth/            — host-rooted OIDC façade (CFAuth.js)
    //   /oauth2/          — apex OAuth callbacks (CFAuthCallback.js)
    // Plus consumer-declared `appConfig.nonSpaPaths` (e.g. /explore/).
    const reqPath = new URL(event.request.url).pathname;
    const consumerNonSpaPaths = Array.isArray(self.appConfig?.nonSpaPaths)
        ? self.appConfig.nonSpaPaths
        : [];
    const isSpecialPathEarly = reqPath.includes('/authentication/') ||
                               reqPath.includes('/_framework/') ||
                               reqPath.includes('/_content/') ||
                               reqPath.startsWith('/auth/') ||
                               reqPath.startsWith('/oauth2/') ||
                               consumerNonSpaPaths.some(p => reqPath.startsWith(p));
    if (isSpecialPathEarly) {
        // Returning without calling event.respondWith hands control
        // back to the browser's default fetch pipeline. The browser
        // does its own network fetch + content decoding + SRI check.
        return;
    }

    event.respondWith((async () => {
        let url = new URL(event.request.url);
        let path = url.pathname;
        const isOnline = await self.connectivityService.isReallyOnline();
        let request = event.request;

        // Belt-and-suspenders: re-derive isSpecialPath inside the
        // respondWith body in case future edits change the early
        // exit condition. This branch is unreachable as long as the
        // early return above stays in sync with this list.
        const isSpecialPath = isSpecialPathEarly;
        if (isSpecialPath) {
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

