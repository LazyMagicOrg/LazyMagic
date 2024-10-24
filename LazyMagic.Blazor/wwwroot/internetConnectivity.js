// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function checkInternetConnectivity() {
    return navigator.onLine;
}

const internetStatusListeners = new Set();
var dotNetReference = null;

export function initializeInternetStatusInterop(dotNetReferenceArg) {
    dotNetReference = dotNetReferenceArg
    if (dotNetReference === null)
        throw new ("dotNetReference is null");
    const onlineHandler = () => {
        dotNetReference.invokeMethodAsync("HandleNetworkStatusChange", true);
    };

    const offlineHandler = () => {
        dotNetReference.invokeMethodAsync("HandleNetworkStatusChange", false);
    };

    window.addEventListener("online", onlineHandler);
    window.addEventListener("offline", offlineHandler);

    // Store event listeners for later removal
    internetStatusListeners.add([onlineHandler, offlineHandler]);
}

export function removeInternetStatusInterop() {
    for (const [onlineHandler, offlineHandler] of internetStatusListeners) {
        window.removeEventListener("online", onlineHandler);
        window.removeEventListener("offline", offlineHandler);
    }
    internetStatusListeners.clear();
}
