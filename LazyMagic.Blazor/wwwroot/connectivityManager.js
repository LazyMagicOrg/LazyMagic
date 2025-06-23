/**
 * connectivityManager.js
 * Main thread connectivity manager for Blazor integration
 */

import { connectivityService } from './connectivityService.js';

class ConnectivityManager {
    constructor() {
        this.dotNetRef = null;
        this.isInitialized = false;
        
        // Add listener for connectivity changes
        connectivityService.addListener((isOnline) => {
            this.notifyDotNet(isOnline);
        });
    }

    /**
     * Initialize with .NET reference for callbacks
     * @param {any} dotNetRef - DotNetObjectReference from Blazor
     */
    initialize(dotNetRef) {
        this.dotNetRef = dotNetRef;
        this.isInitialized = true;
        console.log('ConnectivityManager initialized');
        
        // Send initial status
        this.notifyDotNet(connectivityService.isOnline);
    }

    /**
     * Get current connectivity status
     * @returns {Promise<boolean>}
     */
    async getConnectivityStatus() {
        return await connectivityService.forceCheck();
    }

    /**
     * Check if we should make network requests
     * @returns {Promise<boolean>}
     */
    async shouldMakeNetworkRequest() {
        const isOnline = await connectivityService.isReallyOnline();
        if (!isOnline) {
            console.log('Network request blocked - device appears to be offline');
        }
        return isOnline;
    }

    /**
     * Notify .NET/Blazor of connectivity changes
     */
    notifyDotNet(isOnline) {
        if (this.isInitialized && this.dotNetRef) {
            try {
                this.dotNetRef.invokeMethodAsync('OnConnectivityChanged', isOnline);
            } catch (error) {
                console.error('Failed to notify .NET of connectivity change:', error);
            }
        }
    }

    /**
     * Dispose resources
     */
    dispose() {
        if (this.dotNetRef) {
            this.dotNetRef.dispose();
            this.dotNetRef = null;
        }
        this.isInitialized = false;
    }
}

// Create singleton instance
const connectivityManager = new ConnectivityManager();

// Global functions for Blazor interop
window.initializeConnectivity = (dotNetRef) => {
    connectivityManager.initialize(dotNetRef);
};

window.getConnectivityStatus = async () => {
    return await connectivityManager.getConnectivityStatus();
};

window.shouldMakeNetworkRequest = async () => {
    return await connectivityManager.shouldMakeNetworkRequest();
};

window.disposeConnectivity = () => {
    connectivityManager.dispose();
};

export { connectivityManager };