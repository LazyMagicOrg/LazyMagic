
//test02
/*
This service-worker is used when the app is running from a remote server.
In addition to the minimum set of service-worker features, this implementation 
includes support for:
- Integration with our static asset caching module.
- Redirection to the base URL when the app is navigated to a different URL.
- Graceful application updates.
*/

console.warn('Loading service worker script');

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
                        // Make sure to use the full absolute URL for the request,
                        //otherwise the request may fail when the service worker is not at the root.
                        if (!asset.url.startsWith('/') )
                            asset.url = '/' + asset.url;  
                        return new Request(asset.url, {
                            //integrity: asset.hash,
                            cache: 'no-cache'
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
                
                // Special paths that should not be redirected
                const isSpecialPath = path.includes('/authentication/') || 
                                     path.includes('/_framework/') || 
                                     path.includes('/_content/');
                
                // If it's a navigation request without a file extension and not a special path,
                // redirect to the app root (without index.html) so Blazor can handle client-side routing
                if (!hasFileExtension && !isSpecialPath) {
                    console.log('SPA route detected, redirecting to app root:', path);
                    url.pathname = self.appConfig.appPath;
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
        
        // Log the final request details for debugging
        console.debug('Fetch processing:', {
            originalUrl: event.request.url,
            finalUrl: request.url,
            method: request.method,
            cache: request.cache,
            isOnline: isOnline,
            mode: request.mode
        });


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

