/**
 * SubtenantService.js
 * Provides functionality for managing subtenant switching and cache cleanup
 * in a multi-tenant Blazor application.
 *
 * This module works with the Hybrid Shared/Specific Caching architecture where:
 * - Shared assets (/system/*, /tenancy/*) are cached once for all subtenants
 * - Subtenant-specific assets (/subtenancy/*) are stored per subtenant and swapped on switch
 */

(function() {
    'use strict';

    console.log('SubtenantService.js: Initializing');

    /**
     * Switch to a new subtenant by:
     * 1. Copying cached assets from storage path (/{subtenant}/subtenancy/*) to active path (/subtenancy/*)
     * 2. Updating localStorage with new subtenant
     * 3. Notifying service worker
     * 4. Reloading the application
     *
     * @param {string} newSubtenant - The subtenant ID to switch to (e.g., "uptown", "downtown")
     * @returns {Promise<void>}
     */
    async function switchSubtenant(newSubtenant) {
        try {
            console.log(`switchSubtenant: Switching to subtenant '${newSubtenant}'`);

            const currentSubtenant = localStorage.getItem('subtenant');
            if (currentSubtenant === newSubtenant) {
                console.log('switchSubtenant: Already on this subtenant, no action needed');
                return;
            }

            // Step 1: Copy assets from storage to active cache
            if (typeof window.copySubtenantCache === 'function') {
                console.log('switchSubtenant: Copying cached assets...');
                const copiedCount = await window.copySubtenantCache(newSubtenant, '/subtenancy/');
                console.log(`switchSubtenant: Copied ${copiedCount} cached assets`);
            } else {
                console.warn('switchSubtenant: copySubtenantCache function not available, skipping cache copy');
            }

            // Step 2: Update localStorage
            localStorage.setItem('subtenant', newSubtenant);
            console.log(`switchSubtenant: localStorage updated to '${newSubtenant}'`);

            // Step 3: Notify service worker (if available)
            if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
                navigator.serviceWorker.controller.postMessage({
                    action: 'subtenantChanged',
                    subtenant: newSubtenant
                });
                console.log('switchSubtenant: Service worker notified');
            }

            // Step 4: Reload the application
            console.log('switchSubtenant: Reloading application...');
            window.location.reload();

        } catch (error) {
            console.error('switchSubtenant: Error switching subtenant:', error);
            alert(`Failed to switch to ${newSubtenant}. Please try again.`);
        }
    }

    /**
     * Clean up cached assets for subtenants that are no longer in the accessible list.
     * This function should be called periodically (e.g., on app startup) to prevent
     * stale caches from accumulating.
     *
     * @param {string[]} accessibleSubtenants - Array of subtenant IDs the user can access
     * @returns {Promise<number>} Number of cache entries removed
     */
    async function cleanupUnusedSubtenantCaches(accessibleSubtenants = []) {
        try {
            console.log('cleanupUnusedSubtenantCaches: Starting cache cleanup');

            const currentSubtenant = localStorage.getItem('subtenant') || 'default';

            // Always keep current subtenant
            const keepSubtenants = new Set([currentSubtenant, ...accessibleSubtenants]);
            console.log(`cleanupUnusedSubtenantCaches: Keeping caches for: ${Array.from(keepSubtenants).join(', ')}`);

            if (!('caches' in window)) {
                console.warn('cleanupUnusedSubtenantCaches: Cache API not available');
                return 0;
            }

            const cache = await caches.open('asset-cache');
            const cachedRequests = await cache.keys();
            let deletedCount = 0;

            for (const request of cachedRequests) {
                const url = new URL(request.url);
                const path = url.pathname;

                // Check if this is a subtenant-specific cache entry
                // Pattern: /{subtenant}/subtenancy/*
                const subtenantMatch = path.match(/^\/([^\/]+)\/subtenancy\//);

                if (subtenantMatch) {
                    const subtenant = subtenantMatch[1];

                    // If this subtenant is not in our keep list, delete it
                    if (!keepSubtenants.has(subtenant)) {
                        await cache.delete(request);
                        deletedCount++;
                        console.debug(`cleanupUnusedSubtenantCaches: Deleted cache for subtenant '${subtenant}': ${path}`);
                    }
                }
            }

            console.log(`cleanupUnusedSubtenantCaches: Completed. Removed ${deletedCount} cache entries.`);
            return deletedCount;

        } catch (error) {
            console.error('cleanupUnusedSubtenantCaches: Error during cleanup:', error);
            return 0;
        }
    }

    /**
     * Pre-load subtenant caches in the background for faster switching.
     * This function sends a message to the service worker to fetch and cache
     * assets for multiple subtenants.
     *
     * @param {string[]} accessibleSubtenants - Array of subtenant IDs to pre-load
     * @returns {Promise<void>}
     */
    async function preloadSubtenantCaches(accessibleSubtenants) {
        try {
            if (!accessibleSubtenants || accessibleSubtenants.length === 0) {
                console.log('preloadSubtenantCaches: No subtenants to pre-load');
                return;
            }

            const currentSubtenant = localStorage.getItem('subtenant') || 'default';

            // Filter out current subtenant (already loaded)
            const subtenantsToLoad = accessibleSubtenants.filter(st => st !== currentSubtenant);

            if (subtenantsToLoad.length === 0) {
                console.log('preloadSubtenantCaches: Current subtenant is the only accessible one');
                return;
            }

            console.log(`preloadSubtenantCaches: Pre-loading ${subtenantsToLoad.length} subtenant(s): ${subtenantsToLoad.join(', ')}`);

            // Check if service worker is available
            if (!('serviceWorker' in navigator) || !navigator.serviceWorker.controller) {
                console.warn('preloadSubtenantCaches: Service worker not available');
                return;
            }

            // Send message to service worker
            navigator.serviceWorker.controller.postMessage({
                action: 'preloadSubtenant',
                subtenants: subtenantsToLoad
            });

            console.log('preloadSubtenantCaches: Pre-load request sent to service worker');

        } catch (error) {
            console.error('preloadSubtenantCaches: Error requesting pre-load:', error);
        }
    }

    // Export functions to window object for global access
    window.switchSubtenant = switchSubtenant;
    window.cleanupUnusedSubtenantCaches = cleanupUnusedSubtenantCaches;
    window.preloadSubtenantCaches = preloadSubtenantCaches;

    console.log('SubtenantService.js: Initialized successfully');
    console.log('SubtenantService.js: Available functions: switchSubtenant, cleanupUnusedSubtenantCaches, preloadSubtenantCaches');

})();
