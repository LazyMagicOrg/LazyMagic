/**
 * connectivityService.js
 * Provides reliable internet connectivity detection for PWAs
 * Handles airplane mode, network failures, and offline scenarios
 */

export class ConnectivityService {
    constructor() {
        this.isOnline = navigator.onLine;
        this.lastCheckTime = Date.now();
        this.checkInterval = 30000; // 30 seconds
        this.listeners = new Set();
        this.intervalId = null;
        this.eventHandlers = new Map();
        this.assetsUrl = ''; // Will be set during initialization
        
        this.init();
    }
    
    /**
     * Set the assets URL for connectivity checks
     * @param {string} url - Base URL for assets from ILzHost
     */
    setAssetsUrl(url) {
        this.assetsUrl = url;
        console.debug(`[ConnectivityService] Assets URL set to: ${url}`);
    }

    init() {
        // Detect execution context - improved detection
        const isServiceWorker = typeof importScripts === 'function';
        const globalScope = isServiceWorker ? self : (typeof window !== 'undefined' ? window : self);
        
        if (!isServiceWorker) {
            // Main thread only - listen to browser online/offline events
            const onlineHandler = () => this.handleConnectivityChange(true);
            const offlineHandler = () => this.handleConnectivityChange(false);
            
            globalScope.addEventListener('online', onlineHandler);
            globalScope.addEventListener('offline', offlineHandler);
            
            // Store handlers for cleanup
            this.eventHandlers.set('online', onlineHandler);
            this.eventHandlers.set('offline', offlineHandler);
            
            // Check on focus (user returning to app) - main thread only
            if (typeof document !== 'undefined') {
                const visibilityHandler = () => {
                    if (!document.hidden) {
                        this.checkConnectivity();
                    }
                };
                document.addEventListener('visibilitychange', visibilityHandler);
                this.eventHandlers.set('visibilitychange', visibilityHandler);
            }
        }
        
        // Periodic connectivity check (works in both contexts) - store interval ID
        this.intervalId = setInterval(() => this.checkConnectivity(), this.checkInterval);
    }

    /**
     * Primary method to check if truly online
     * @returns {Promise<boolean>}
     */
    async isReallyOnline() {
        // First check navigator.onLine
        if (!navigator.onLine) {
            //console.debug('[ConnectivityService] navigator.onLine is false');
            return false;
        }

        // Use HEAD request with no-cors mode to avoid CORS issues
        // HEAD is lightweight and doesn't retrieve body content
        const baseUrl = this.assetsUrl || '';
        // Remove trailing slash from baseUrl if present, then add /config
        const cleanBaseUrl = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
        const url = `${cleanBaseUrl}/config?t=${Date.now()}`;
        //console.debug(`[ConnectivityService] Testing connectivity using HEAD request to: ${url}`);
        
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 5000); // 5 second timeout
            
            await fetch(url, {
                method: 'HEAD',
                mode: 'no-cors',  // This bypasses CORS entirely
                cache: 'no-store',
                signal: controller.signal
            });
            
            clearTimeout(timeoutId);
            
            // With no-cors mode, we can't read the response but if fetch succeeds, we're online
            //console.debug('[ConnectivityService] Connectivity check succeeded');
            return true;
        } catch (error) {
            //console.debug('[ConnectivityService] Connectivity check failed:', error.message);
            
            // If the HEAD request fails, we're likely offline or have network issues
            //console.info('[ConnectivityService] Device appears to be offline');
            return false;
        }
    }

    /**
     * Check connectivity and notify listeners if changed
     */
    async checkConnectivity() {
        const wasOnline = this.isOnline;
        this.isOnline = await this.isReallyOnline();
        
        if (wasOnline !== this.isOnline) {
            this.notifyListeners(this.isOnline);
        }
        
        this.lastCheckTime = Date.now();
        return this.isOnline;
    }

    /**
     * Handle browser online/offline events
     */
    handleConnectivityChange(isOnline) {
        // Browser events are not always reliable, so we verify
        this.checkConnectivity();
    }

    /**
     * Add a listener for connectivity changes
     * @param {Function} callback - Function to call with (isOnline) parameter
     */
    addListener(callback) {
        this.listeners.add(callback);
        // Immediately notify with current status
        callback(this.isOnline);
    }

    /**
     * Remove a connectivity listener
     */
    removeListener(callback) {
        this.listeners.delete(callback);
    }

    /**
     * Notify all listeners of connectivity change
     */
    notifyListeners(isOnline) {
        this.listeners.forEach(callback => {
            try {
                callback(isOnline);
            } catch (error) {
                console.error('Error in connectivity listener:', error);
            }
        });
    }

    /**
     * Get time since last connectivity check
     */
    getTimeSinceLastCheck() {
        return Date.now() - this.lastCheckTime;
    }

    /**
     * Force an immediate connectivity check
     */
    async forceCheck() {
        return await this.checkConnectivity();
    }
    
    /**
     * Clean up resources
     */
    dispose() {
        // Clear interval
        if (this.intervalId) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }
        
        // Remove event listeners
        const isServiceWorker = typeof importScripts === 'function';
        const globalScope = isServiceWorker ? self : (typeof window !== 'undefined' ? window : self);
        
        if (!isServiceWorker) {
            // Remove window event listeners
            const onlineHandler = this.eventHandlers.get('online');
            const offlineHandler = this.eventHandlers.get('offline');
            
            if (onlineHandler) {
                globalScope.removeEventListener('online', onlineHandler);
            }
            if (offlineHandler) {
                globalScope.removeEventListener('offline', offlineHandler);
            }
            
            // Remove document event listener
            if (typeof document !== 'undefined') {
                const visibilityHandler = this.eventHandlers.get('visibilitychange');
                if (visibilityHandler) {
                    document.removeEventListener('visibilitychange', visibilityHandler);
                }
            }
        }
        
        // Clear handlers map
        this.eventHandlers.clear();
        
        // Clear listeners
        this.listeners.clear();
    }
}

// Create singleton instance
export const connectivityService = new ConnectivityService();

// For service worker usage
if (typeof self !== 'undefined' && self.ServiceWorkerGlobalScope) {
    self.connectivityService = connectivityService;
}