
let viewerInstance;
export async function initialize(viewerInstanceArg) {
    viewerInstance = viewerInstanceArg; // allows JS to do callbacks on the viewerInstance C# class
    if (navigator.serviceWorker) {
        console.log("registering service worker events for update/install");
        navigator.serviceWorker.addEventListener('message', event => {
            console.log("message:" + event.data.action + "," + event.data.info);
            switch (event.data.action) {
                case 'AssetDataCheckStarted':
                    viewerInstance.invokeMethodAsync('AssetDataCheckStarted');
                    break;
                case 'AssetDataCheckComplete':
                    viewerInstance.invokeMethodAsync('AssetDataCheckComplete');
                    break;
                case 'AssetDataUpdateStarted':
                    viewerInstance.invokeMethodAsync('AssetDataUpdateStarted');
                    break;
                case 'AssetDataUpdateComplete':
                    viewerInstance.invokeMethodAsync('AssetDataUpdateComplete');
                    break;
                case 'ServiceWorkerUpdateStarted':
                    viewerInstance.invokeMethodAsync('ServiceWorkerUpdateStarted');
                    break;
                case 'ServiceWorkerUpdateCompleted':
                    viewerInstance.invokeMethodAsync('ServiceWorkerUpdateComplete');
                    break;
                case 'CacheMiss':
                    const logTextElement = document.getElementById('logText');
                    if (logTextElement) {
                        logTextElement.textContent = `${event.data.action}, ${event.data.info}`;
                    }

                    viewerInstance.invokeMethodAsync('CacheMissAction', event.data.info);
                    break;
                default:
                    break;
                    console.log('Unknown event' + event.data.action + 'received');
            }
        });
    }
}
export async function checkForNewAssetData() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, checkForNewAssetData. No network access");
        return;
    }
    // Put in the post message to service worker to kick off asset data check
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js checkForNewAssetData');
        registration.active.postMessage({ action: 'checkForNewAssetData' });
    }
}
export async function assetDataUpdateStarted() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, assetDataUpdateStarted. No network access");
        return;
    }
    // Put in the post message to service worker to kick off service worker update
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js, assetDataUpdateStarted');
        registration.active.postMessage({ action: 'assetDataUpdateStarted' });
    }
}
export async function assetDataUpdateComplete() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, assetDataUpdateComplete. No network access");
        return;
    }
    // Put in the post message to service worker to kick off service worker update
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js, assetDataUpdateComplete');
        registration.active.postMessage({ action: 'assetDataUpdateComplete' });
    }
}
export async function serviceWorkerUpdateStarted() {
    if (!navigator.onLine) {
        console.warn("baseapp.js, serviceWorkerUpdateStarted. No network access");
        return;
    }
    // Put in the post message to service worker to kick off service worker update
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
        const registration = await navigator.serviceWorker.getRegistration();
        console.log('baseapp.js, serviceWorkerUpdateStarted');
        registration.active.postMessage({ action: 'serviceWorkerUpdateStarted' });
    }
}
export async function reload() {
    // We can't use reload() because the current browser URL may include a Blazor "page" (component)
    // and that would cause a 404. Example: /myapp/HomePage.
    // Also, the reload behavior is different for reload in dev (localhost) and
    // reload from a non-dev server.
    const isDev = window.location.hostname.includes('localhost');
    const baseHrefElement = document.querySelector('base');
    const appPath = new URL(baseHrefElement.href).pathname;
    if (isDev) {
        location.href = new URL("/", self.location.origin);
    }
    else {
        console.log("reload appPath:" + appPath);
        location.href = new URL(appPath, self.location.origin);
    }
}
export async function getMemory() {
    return [performance.memory.jsHeapSizeLimit, performance.memory.usedJSHeapSize]
}
export async function setPointerCapture(element, pointerid) {
    element.setPointerCapture(pointerid);
}
export async function getBase64Image(img) {
    // Create an empty canvas element
    var canvas = document.createElement("canvas");
    canvas.width = img.naturalWidth;
    canvas.height = img.naturalHeight;

    // Copy the image contents to the canvas
    var ctx = canvas.getContext("2d");
    ctx.drawImage(img, 0, 0);

    // Using default image/png becuase Safari doesn't support the type argument'
    var dataURL = canvas.toDataURL();
    return dataURL.replace(/^data:image\/(png|jpg);base64,/, "");
}
export async function getBase64ImageDownsized(img) {
    // Create an empty canvas element
    var canvas = document.createElement("canvas");
    let aspectRatio = Number(img.naturalWidth) / Number(img.naturalHeight);

    canvas.width = 600.0;
    canvas.height = aspectRatio / aspectRatio;

    // Copy the image contents to the canvas
    var ctx = canvas.getContext("2d");
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

    // Using default image/png becuase Safari doesn't support the type argument'
    var dataURL = canvas.toDataURL();
    return dataURL.replace(/^data:image\/(png|jpg);base64,/, "");
}
export async function sharePng(title, text, pngData, textData = null) {
    try {

        pngData = pngData.replace(/^data:image\/(png|jpeg|jpg);base64,/, '');
        const binaryData = Uint8Array.from(atob(pngData), c => c.charCodeAt(0));
        const file = new File([binaryData], 'image.png', { type: 'image/png' });
        const files = [file];
        if (textData) { 
            const textFile = new File([textData], 'report.txt', { type: 'text/plain' });
            files.push(textFile);
        }

        await navigator.share({
            title: title,
            text: text,
            files: files
        });
        return true;

    } catch (error) {
        console.error('Error sharing content', error);
    }
    return false;
}
export async function shareText(title, text) {
    try {
        await navigator.share({
            title: title,
            text: text
        });
        return true;

    } catch (error) {
        console.error('Error sharing content', error);
    }
    return false;
}
export async function localStorageSetItem(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch {
        console.error("Can't set key:" + key);
    }
}
export async function localStorageGetItem(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        console.error("Can't read key:" + key);
    }
}
export async function localStorageRemoveItem(key) {
    try {
        localStorage.removeItem(key);
    } catch {
        console.error("Can't remove key:" + key);
    }
}