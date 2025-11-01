/**
 * Auth app initialization module
 * Simplified version - no service worker, no subtenant handling in indexinit
 */

window.isLoaded = false;
window.checkIfLoaded = function () {
    return window.isLoaded;
};

if (window.location.origin.includes("localhost")) {
    try {
        console.debug("Auth app: Running from local development host");
        const { appConfig } = await import('./appConfig.js');
        window.appConfig = {
            appPath: "/auth/",
            appUrl: window.location.origin,
            androidAppUrl: "",
            remoteApiUrl: appConfig.remoteApiUrl,
            localApiUrl: appConfig.localApiUrl,
            assetsUrl: appConfig.assetsUrl,
            authConfigName: "",
        };
    } catch (error) {
        console.error("Error loading appConfig.js:", error);
    }
} else {
    // Running from cloud
    const baseHrefElement = document.querySelector('base');
    const fullAppPath = new URL(baseHrefElement.href).pathname;
    const pathSegments = fullAppPath.split('/').filter(segment => segment !== '');
    const appPath = pathSegments.length > 0 ? '/' + pathSegments[0] + '/' : '/';
    const { appConfig } = await import('./appConfig.js');

    window.appConfig = {
        appPath: appPath,
        appUrl: window.location.origin + "/",
        androidAppUrl: "",
        remoteApiUrl: window.location.origin + "/",
        localhostApiUrl: "",
        assetsUrl: window.location.origin + "/",
        wsUrl: window.location.origin.replace(/^http/, 'ws') + "/",
        authConfigName: "",
    };

    if (navigator.serviceWorker) {
        console.log("Auth app: Registering service worker");
        navigator.serviceWorker.addEventListener('message', event => {
            console.log("message:" + event.data.action + "," + event.data.info);
        });
        navigator.serviceWorker.register('service-worker.js', { type: 'module', scope: appPath });
    }
}

window.isLoaded = true;
