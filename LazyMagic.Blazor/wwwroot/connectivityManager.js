/**
 * connectivityManager.js
 * Main thread connectivity manager for Blazor integration
 */

import { connectivityService } from './connectivityService.js';

class ConnectivityManager {
    constructor() {
        this.dotNetRef = null;
        this.isInitialized = false;
        this.connectivityListener = null;
        
        // Create and store listener for connectivity changes
        this.connectivityListener = (isOnline) => {
            this.notifyDotNet(isOnline);
        };
        connectivityService.addListener(this.connectivityListener);
    }

    /**
     * Initialize with .NET reference for callbacks and assets URL
     * @param {any} dotNetRef - DotNetObjectReference from Blazor
     * @param {string} assetsUrl - Base URL for assets from ILzHost
     */
    initialize(dotNetRef, assetsUrl) {
        this.dotNetRef = dotNetRef;
        this.isInitialized = true;
        
        // Configure the connectivity service with the assets URL
        connectivityService.setAssetsUrl(assetsUrl);
        
        console.log('ConnectivityManager initialized with assets URL:', assetsUrl);
        
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
        // Remove connectivity listener
        if (this.connectivityListener) {
            connectivityService.removeListener(this.connectivityListener);
            this.connectivityListener = null;
        }
        
        if (this.dotNetRef) {
            // Note: We don't dispose dotNetRef here as it's managed by .NET
            this.dotNetRef = null;
        }
        
        this.isInitialized = false;
        console.log('ConnectivityManager disposed');
    }
}

// Create singleton instance
const connectivityManager = new ConnectivityManager();

// Global functions for Blazor interop
window.initializeConnectivity = (dotNetRef, assetsUrl) => {
    connectivityManager.initialize(dotNetRef, assetsUrl);
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