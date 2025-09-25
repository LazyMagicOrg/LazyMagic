// This module wraps ClientJS and exposes its methods for use by Blazor
// Dynamically loads ClientJS library

let clientInstance = null;
let clientJSLoaded = false;

// Function to load ClientJS dynamically
async function loadClientJS() {
    if (clientJSLoaded && window.ClientJS) {
        return true;
    }
    
    return new Promise((resolve) => {
        // Create a script element to load ClientJS
        const script = document.createElement('script');
        script.src = './_content/LazyMagic.Blazor/client.min.js';
        
        script.onload = () => {
            // Check if ClientJS is available
            if (window.ClientJS) {
                clientJSLoaded = true;
                resolve(true);
            } else {
                console.error('ClientJS library loaded but ClientJS constructor not available');
                resolve(false);
            }
        };
        
        script.onerror = (error) => {
            console.error('Failed to load ClientJS library:', error);
            resolve(false);
        };
        
        // Add script to document
        document.head.appendChild(script);
    });
}

// Initialize ClientJS instance
async function getClientInstance() {
    if (clientInstance) {
        return clientInstance;
    }
    
    const loaded = await loadClientJS();
    if (loaded && window.ClientJS) {
        clientInstance = new window.ClientJS();
        return clientInstance;
    }
    
    return null;
}

// Export functions that can be called from Blazor
export async function getFingerprint() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return 0;
    }
    // getFingerprint returns a number, convert to string
    return client.getFingerprint();
}

export async function getBrowser() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getBrowser() || '';
}

export async function getBrowserVersion() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getBrowserVersion() || '';
}

export async function getOS() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getOS() || '';
}

export async function getOSVersion() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getOSVersion() || '';
}

export async function getResolution() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    // getCurrentResolution returns a string like "1920x1080", not an array
    return client.getCurrentResolution() || '';
}

// Alternative: Get all info at once
export async function getBrowserInfo() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return {
            fingerprint: 0,
            browser: '',
            browserVersion: '',
            os: '',
            osVersion: '',
            resolution: ''
        };
    }
    
    return {
        // Basic info
        fingerprint: client.getFingerprint(),
        browser: client.getBrowser() || '',
        browserVersion: client.getBrowserVersion() || '',
        os: client.getOS() || '',
        osVersion: client.getOSVersion() || '',
        resolution: client.getCurrentResolution() || '',

        // Extended info
        userAgent: client.getUserAgent() || '',
        device: client.getDevice() || '',
        deviceType: client.getDeviceType() || '',
        deviceVendor: client.getDeviceVendor() || '',
        language: client.getLanguage() || '',
        timeZone: client.getTimeZone() || '',
        screenPrint: client.getScreenPrint() || '',

        // Browser flags
        isMobile: client.isMobile() || false,
        isChrome: client.isChrome() || false,
        isFirefox: client.isFirefox() || false,
        isSafari: client.isSafari() || false,
        isIE: client.isIE() || false
    };
}

// Additional useful functions
export async function getUserAgent() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getUserAgent() || '';
}

export async function getDevice() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getDevice() || '';
}

export async function getDeviceType() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getDeviceType() || '';
}

export async function getDeviceVendor() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getDeviceVendor() || '';
}

export async function isMobile() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return false;
    }
    return client.isMobile() || false;
}

export async function isChrome() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return false;
    }
    return client.isChrome() || false;
}

export async function isFirefox() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return false;
    }
    return client.isFirefox() || false;
}

export async function isSafari() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return false;
    }
    return client.isSafari() || false;
}

export async function isIE() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return false;
    }
    return client.isIE() || false;
}

export async function getLanguage() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getLanguage() || '';
}

export async function getTimeZone() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getTimeZone() || '';
}

export async function getScreenPrint() {
    const client = await getClientInstance();
    if (!client) {
        console.error('ClientJS instance not available');
        return '';
    }
    return client.getScreenPrint() || '';
}
