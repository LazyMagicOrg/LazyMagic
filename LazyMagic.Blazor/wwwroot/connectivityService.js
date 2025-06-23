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
        this.testUrl = '/favicon.ico'; // Small file that should always exist
        this.alternateTestUrls = [
            'https://www.google.com/favicon.ico',
            'https://www.cloudflare.com/favicon.ico'
        ];
        
        this.init();
    }

    init() {
        // Detect execution context
        const isServiceWorker = typeof self !== 'undefined' && self.ServiceWorkerGlobalScope;
        const globalScope = isServiceWorker ? self : (typeof window !== 'undefined' ? window : self);
        
        if (!isServiceWorker) {
            // Main thread only - listen to browser online/offline events
            globalScope.addEventListener('online', () => this.handleConnectivityChange(true));
            globalScope.addEventListener('offline', () => this.handleConnectivityChange(false));
            
            // Check on focus (user returning to app) - main thread only
            if (typeof document !== 'undefined') {
                document.addEventListener('visibilitychange', () => {
                    if (!document.hidden) {
                        this.checkConnectivity();
                    }
                });
            }
        }
        
        // Periodic connectivity check (works in both contexts)
        setInterval(() => this.checkConnectivity(), this.checkInterval);
    }

    /**
     * Primary method to check if truly online
     * @returns {Promise<boolean>}
     */
    async isReallyOnline() {
        // First check navigator.onLine
        if (!navigator.onLine) {
            return false;
        }

        // Then try actual network request
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 5000); // 5 second timeout
            
            const response = await fetch(this.testUrl, {
                method: 'HEAD',
                mode: 'no-cors',
                cache: 'no-store',
                signal: controller.signal
            });
            
            clearTimeout(timeoutId);
            
            // For no-cors mode, we just check if fetch succeeded
            // Response will be opaque, so we can't check status
            return true;
        } catch (error) {
            // If local test failed, try external URLs (for development scenarios)
            const globalScope = typeof window !== 'undefined' ? window : self;
            if (globalScope.location.hostname === 'localhost') {
                for (const url of this.alternateTestUrls) {
                    try {
                        const controller = new AbortController();
                        const timeoutId = setTimeout(() => controller.abort(), 3000);
                        
                        await fetch(url, {
                            method: 'HEAD',
                            mode: 'no-cors',
                            cache: 'no-store',
                            signal: controller.signal
                        });
                        
                        clearTimeout(timeoutId);
                        return true;
                    } catch {
                        continue;
                    }
                }
            }
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
}

// Create singleton instance
export const connectivityService = new ConnectivityService();

// For service worker usage
if (typeof self !== 'undefined' && self.ServiceWorkerGlobalScope) {
    self.connectivityService = connectivityService;
}