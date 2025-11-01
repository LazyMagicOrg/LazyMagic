// Minimal service worker for auth app
// In production, this file is copied to service-worker.js

self.addEventListener('install', event => {
    console.log('Auth app service worker: Install');
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    console.log('Auth app service worker: Activate');
    return self.clients.claim();
});

self.addEventListener('fetch', event => {
    // Pass through all requests - no caching for auth app
    return;
});
