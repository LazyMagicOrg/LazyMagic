// This module is used when you don't have a service worker (like in local dev or in MAUI).
// It overrides window.fetch so we can intercept fetch requests and serve assets from the cache.
// In the service-worker, the fetch override is implemented is impolemented as a fetch event listener.

// Import the module

const originalFetch = fetch;
let fetchRecursion = 0; // Used to prevent infinite recursion when fetching assets
const assetHostUrl = new URL(window.appConfig.assetsUrl);
let staticContentModule;


try {
    console.log("Starting to import staticContentModule");
    staticContentModule = await import('./staticContentModule.js');
    console.log("staticContentModule imported successfully");
    console.log("Installing override fetch");

    window.fetch = async function (...args) {
        //console.log("Fetch override called");
        async function modifyUrl(originalUrl) {
            let url = new URL(originalUrl);
            url.hostname = assetHostUrl.hostname;
            url.port = assetHostUrl.port;
            return url.href;
        }

        try {

            fetchRecursion++;

            let request = args[0];
            let options = args[1] || {};

            // construct a Request object if all we got was a string
            if (typeof request === 'string')
                request = new Request(request, options);

            if (request.method === 'GET' && options.cache !== "no-cache") {
                if (staticContentModule) {
                    // examine the request path to see if it matches a cache name
                    let cacheName = await staticContentModule.getCacheName(request.url);
                    // console.log("Cache name: " + cacheName);
                    if (cacheName) {
                        if (fetchRecursion == 1)
                            await staticContentModule.lazyLoadAssetCache(cacheName);

                        const newUrl = await modifyUrl(request.url);
                        request = new Request(newUrl, {
                            method: request.method,
                            headers: request.headers,
                            mode: request.mode,
                            credentials: request.credentials,
                            redirect: request.redirect
                        });
                        const cachedResponse = await staticContentModule.getCachedResponse(cacheName, request);
                        return cachedResponse || await originalFetch(request);
                    }
                }
            }
            return await originalFetch(request);
        } catch (error) {
            console.error(error);
        } finally {
            if (fetchRecursion > 0) fetchRecursion--;
        }
    }
    console.log("Fetch override installed");

} catch (error) {
    console.error(error);
}


export async function uIFetchLoadStaticAssets() {
    if (staticContentModule) {
        await staticContentModule.readAssetCachesByType("PreCache");
    }   
 }
