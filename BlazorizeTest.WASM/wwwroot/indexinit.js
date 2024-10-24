/**
 * This module is used to initialize the Blazor WebAssembly app.
 * use a link in your index.html file to import this module:
 * <script type="module" src="indexinit.js"></script>
 * 
 * This module is NOT used in the MAUI Hybrid app. 
 * The WASM app can run in three modes:
 * - local development calling the remoteApiUrl and uses UIFetch module
 * - local development calling the localApiUrl and uses UIFetch module
 * - remote deployment, registers and uses service-worker.js
 * 
 * For local development, the app code is served by the localhost server. However, 
 * the app calls the cloud for static assets. The app may make service calls against either
 *  the cloud application or the localhost application api.
 * The websocket connection is used to process messages from the cloud. Since these messages 
 * are generally the result of data being written to the cloud database, we always connect
 * the development environment to the cloud websocket.
 */

window.isLoaded = false;

window.checkIfLoaded = function () {
    return window.isLoaded;
};


if (window.location.origin.includes("localhost")) {
    /*** APP LOADED FROM THE LOCALHOST ***/
    console.debug("Running from local development host");
    const { appDevConfig } = await import('./_content/BlazorUI/appDevConfig.js');
    const { appConfig } = await import('./_content/BlazorUI/appConfig.js');

    window.appConfig = {
        appPath: appConfig.appPath,
        appUrl: window.location.origin,
        androidAppUrl: appDevConfig.androidAppUrl,
        remoteApiUrl: appDevConfig.remoteApiUrl,
        localApiUrl: appDevConfig.localApiUrl,
        assetsUrl: appDevConfig.assetsUrl,
        wsUrl: appDevConfig.wsUrl
    };
    const { uIFetchLoadStaticAssets } = await import('./_content/BlazorUI/UIFetch.js');

    window.isLoaded = true; // This lets the app know it can proceed with the Blazor app startup

    await uIFetchLoadStaticAssets(); // This will load the static assets into the cache(s)

} else {
    /**** APP LOADED FROM NON-DEV HOST (cloud, remote host etc.) ****/
    // When runing from the cloud, the baseHref is set to the base URL of the app.
    // We do not use appDevConfig.js when running from the cloud.
    console.debug("Running from cloud or remote server");
    // We are not using anything from appConfig.js when using a service worker. 
    // const { appConfig } = await import('./_content/BlazorUI/appConfig.js');

    // Note that the base href is updated on publish by a target in our
    // csproj file. It is difficult to set it dynamically as it is used
    // in the index.html file before we have a chance to modify it 
    // with a script.
    const baseHrefElement = document.querySelector('base');
    const appPath = new URL(baseHrefElement.href).pathname;
    window.appConfig = {
        appPath: appPath,
        appUrl: window.location.origin + "/",
        androidAppUrl: "",
        remoteApiUrl: window.location.orgin + "/",
        localhostApiUrl: "",
        assetsUrl: window.location.origin + "/",
        wsUrl: window.location.origin.replace(/^http/, 'ws') + "/"
    };

    window.isLoaded = true; // This lets the app know it can proceed with the Blazor app startup

    // Note that the service worker activate event kicks off the asset caching process.
    navigator.serviceWorker.register('service-worker.js', { type: 'module', scope: appPath });


}
