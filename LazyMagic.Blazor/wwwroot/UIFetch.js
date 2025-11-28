// This module is used when you are running in a MAUI Hybrid app
// It overrides window.fetch so we can intercept fetch requests and serve assets from the cache.
// In the service-worker, the fetch override is implemented is impolemented as a fetch event listener.

// Import the module

const originalFetch = fetch;
let fetchRecursion = 0; // Used to prevent infinite recursion when fetching assets
let staticContentModule;

try {

    window.fetch = async function (...args) {

        // TODO: Review this method and where it is called.
        // The argument orignialUrl is not used.
        // I'm not sure why we need this method at all. In what
        // circumstances would the original URL need to be modified?
        async function modifyUrl(url) {
            // Check if url is a string and convert to URL object if needed
            const urlObj = typeof url === 'string' ? new URL(url) : url;

            const assetHostUrl = new URL(window.appConfig.assetsUrl);
            console.log("Asset host URL: " + assetHostUrl.href + ", originalUrl: " + urlObj.href);

            urlObj.hostname = assetHostUrl.hostname;
            urlObj.port = assetHostUrl.port;

            return urlObj.href;
        }

        try {

            fetchRecursion++;

            let request = args[0];
            let options = args[1] || {};
            // construct a Request object if all we got was a string
            if (typeof request === 'string')
                request = new Request(request, options);

            // Redirect to the base URL if the app is navigated to a different URL
            // Note that this doesn't handle hard relaods as these bypass the service worker.
            // You must handle hard reloads on the server side.
            if (request.mode === 'navigate') {
                // Blazor navigation detected - ignore
                // We don't want to fetch when all we are doing is updating the URL for an internal Blazor navigation.
                // e.g. when we route to a different "page" (really just a Blazor component) in the SPA.
                console.log('Blazor navigation detected', request.url);
                return new Response(null, { status: 204, statusText: 'no content' });
            }

            if (request.method === 'GET' && options.cache !== "no-cache") {
                if (window.staticContentModule) {
                    // examine the request path to see if it matches a cache name
                    let cacheName = await window.staticContentModule.getCacheName(request.url);
                    // console.log("Cache name: " + cacheName);
                    if (cacheName) {
                        if (fetchRecursion == 1)
                            await window.staticContentModule.lazyLoadAssetCache(cacheName);
                        console.log("Fetching from cache: " + cacheName + ", URL: " + request.url);
                        const newUrl = await modifyUrl(request.url);
                        request = new Request(newUrl, {
                            method: request.method,
                            headers: request.headers,
                            mode: request.mode,
                            credentials: request.credentials,
                            redirect: request.redirect
                        });
                        const cachedResponse = await window.staticContentModule.getCachedResponse(cacheName, request);
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
        await window.staticContentModule.readAssetCachesByType("PreCache");
    }
}
