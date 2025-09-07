
//test02
/*
This service-worker is used when the app is running from a remote server.
In addition to the minimum set of service-worker features, this implementation 
includes support for:
- Integration with our static asset caching module.
- Redirection to the base URL when the app is navigated to a different URL.
- Graceful application updates.
*/
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

const offlineAssetsInclude = [/\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.svg$/, /\.woff2$/];
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
    console.info('Service worker installing...');
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
                        if (asset.url.includes('index.html')) {
                            // We don't us asset.hash on index.html because we update the base tag after the 
                            // msbuild publish step.
                            return new Request(asset.url, {
                                cache: 'no-cache'
                            });
                        }
                        else {
                            return new Request(asset.url, {
                                // integrity: asset.hash,
                                cache: 'no-cache'
                            });
                        }
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
    console.log("Service worker activating");
    event.waitUntil((async () => {
        await self.staticContentModule.activateApplicationCache();
        await self.clients.claim();
        await self.staticContentModule.checkAssetCaches();
        await sendMessage("ServiceWorkerUpdateCompleted", "The new version has been installed.");
    })());
});

self.addEventListener('fetch', event => {
    event.respondWith((async () => {
        let url = new URL(event.request.url);
        let path = url.pathname;
        const isOnline = await self.connectivityService.isReallyOnline();
        let request = event.request;

        // Redirect to the base URL if the app is navigated to a different URL
        // Note that this doesn't handle hard relaods as these bypass the service worker.
        // You must handle hard reloads on the server side.
        // The following code breaks the PWA. When the app is run, it doesn't load 
        // and when we inspect appInfo it says the domain is insecure.
        if (request.mode === 'navigate' || request.url.endsWith('Page')) {
            // Blazor navigation detected - ignore
            // We don't want to fetch when all we are doing is updating the URL for an internal Blazor navigation.
            // e.g. when we route to a different "page" (really just a Blazor component) in the SPA.
            // Note: It is important to not return the null, 204 response when the path is the appPrefix 
            // as this will break the PWA. The PWA will not load and the appInfo will say the domain is insecure.
            console.log('Blazor navigation to root detected', url);
            url = new URL(event.request.url);
            url.pathname = self.appConfig.appPath; // Redirect to the root document
            console.debug('Redirecting to:', url.toString());
            request = new Request(url.toString(), {
                method: 'GET',
                headers: event.request.headers,
                mode: 'same-origin',
                credentials: event.request.credentials,
                cache: event.request.cache
            });
        }

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
                                if (!response.ok) {
                                    console.error('Fetch error:', response.url);
                                    return new Response(null, { status: 204, statusText: 'no content' });
                                }
                                return response;
                            })
                            .catch(error => {
                                console.error('Fetch error:', request.url, error);
                                return new Response(null, { status: 204, statusText: 'no content' });
                            })
                    }
                }
                else {
                    console.warn('No cache associated/available for url:' + request.url);
                }
            }
            catch (error) {
                console.error('Error during fetch:', event.request.url, error);
                return new Response('Error during fetch: ', { status: 500 });
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

