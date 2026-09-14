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
        this.lastProbeTime = 0; // when the last HEAD probe was sent
        this.probeInFlight = null; // the pending probe, shared by concurrent callers
        this.listeners = new Set();
        this.intervalId = null;
        this.eventHandlers = new Map();
        this.assetsUrl = ''; // Will be set during initialization
        
        this.init();
    }
    
    /**
     * Standard logging with method name and timestamp
     * @param {string} methodName - Name of the calling method
     * @param {string} message - Log message
     * @param {string} level - Log level (debug, info, warn, error)
     */
    log(methodName, message, level = 'info') {
        const timestamp = new Date().toISOString().substr(11, 12); // HH:mm:ss.fff format
        console[level](`[${methodName}][${timestamp}] ${message}`);
    }
    
    /**
     * Set the assets URL for connectivity checks
     * @param {string} url - Base URL for assets from ILzHost
     */
    setAssetsUrl(url) {
        this.assetsUrl = url;
        this.log('setAssetsUrl', `Assets URL set to: ${url}`, 'debug');
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
        
        // Periodic connectivity check (works in both contexts) - store interval ID.
        // The interval is the cadence, so it always probes (see isReallyOnline).
        this.intervalId = setInterval(() => this.checkConnectivity(true), this.checkInterval);
    }

    /**
     * Primary method to check if truly online.
     *
     * Sends at most one probe per checkInterval from this context unless forced: a caller
     * that arrives while a probe is in flight shares it, and one that arrives within
     * checkInterval of the last probe gets that probe's verdict without a request. The
     * service worker's fetch handler used to call this for every request it handled - one
     * HEAD /config per request, about 190 for a single console load.
     * @param {boolean} force - Probe even if the last probe is recent (still shares one in flight)
     * @returns {Promise<boolean>}
     */
    async isReallyOnline(force = false) {
        // First check navigator.onLine
        if (!navigator.onLine) {
            this.log('isReallyOnline', 'navigator.onLine is false', 'debug');
            this.recordVerdict(false);
            return false;
        }

        if (this.probeInFlight) {
            return await this.probeInFlight;
        }
        if (!force && Date.now() - this.lastProbeTime < this.checkInterval) {
            return this.isOnline;
        }

        this.lastProbeTime = Date.now();
        this.probeInFlight = this.probe();
        try {
            const isOnline = await this.probeInFlight;
            this.recordVerdict(isOnline);
            return isOnline;
        } finally {
            this.probeInFlight = null;
        }
    }

    /**
     * Send one HEAD request and report whether anything answered it
     * @returns {Promise<boolean>}
     */
    async probe() {
        // Use HEAD request with no-cors mode to avoid CORS issues
        // HEAD is lightweight and doesn't retrieve body content
        const baseUrl = this.assetsUrl || '';
        // Remove trailing slash from baseUrl if present, then add /config
        const cleanBaseUrl = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
        const url = `${cleanBaseUrl}/config?t=${Date.now()}`;
        //console.debug(`[ConnectivityService] Testing connectivity using HEAD request to: ${url}`);
        
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 500); 
            
            await fetch(url, {
                method: 'HEAD',
                mode: 'no-cors',  // This bypasses CORS entirely
                cache: 'no-store',
                signal: controller.signal
            });
            
            clearTimeout(timeoutId);
            
            return true;
        } catch (error) {
            //console.debug('[ConnectivityService] Connectivity check failed:', error.message);
            
            // If the HEAD request fails, we're likely offline or have network issues
            //console.info('[ConnectivityService] Device appears to be offline');
            return false;
        }
    }

    /**
     * Record a verdict and notify listeners if it changed. Every verdict lands here, so a
     * direct isReallyOnline() caller updates the state that later callers are answered from.
     * @param {boolean} isOnline
     */
    recordVerdict(isOnline) {
        const wasOnline = this.isOnline;
        this.isOnline = isOnline;
        this.lastCheckTime = Date.now();

        if (wasOnline !== isOnline) {
            this.notifyListeners(isOnline);
        }
    }

    /**
     * Check connectivity and notify listeners if changed
     * @param {boolean} force - Probe even if the last probe is recent
     */
    async checkConnectivity(force = false) {
        return await this.isReallyOnline(force);
    }

    /**
     * Handle browser online/offline events
     */
    handleConnectivityChange(isOnline) {
        // Browser events are not always reliable, so we verify - now, since the network changed
        this.checkConnectivity(true);
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
                this.log('notifyListeners', `Error in connectivity listener: ${error.message}`, 'error');
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
        return await this.checkConnectivity(true);
    }

    /**
     * Enable or disable polling
     * @param {boolean} enabled - Whether polling should be enabled
     */
    setPollingEnabled(enabled) {
        if (enabled && !this.intervalId) {
            // Start polling
            this.intervalId = setInterval(() => this.checkConnectivity(true), this.checkInterval);
            this.log('setPollingEnabled', 'Polling enabled', 'info');
        } else if (!enabled && this.intervalId) {
            // Stop polling
            clearInterval(this.intervalId);
            this.intervalId = null;
            this.log('setPollingEnabled', 'Polling disabled', 'info');
        }
    }

    /**
     * Check if polling is currently enabled
     * @returns {boolean}
     */
    isPollingEnabled() {
        return this.intervalId !== null;
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