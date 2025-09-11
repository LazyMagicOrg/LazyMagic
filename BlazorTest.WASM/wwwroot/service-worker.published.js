// This service worker delegates to the actual implementation in BaseApp.BlazorUI
// The import statement will execute the BaseApp.BlazorUI service worker code,
// which will register all the necessary event listeners in this context

// Impot the app configuration and settings
import { appConfig } from './_content/BlazorUI/appConfig.js';
import { settings } from './_content/BlazorUI/staticContentSettings.js';

// Import the Blazor-generated assets manifest
import './service-worker-assets.js';

// Now import the service worker and caching support code
import * as staticContentModule from './_content/LazyMagic.Blazor/staticContentModule.js'; // Note namespace use
import { connectivityService } from './_content/LazyMagic.Blazor/connectivityService.js';
import './_content/LazyMagic.Blazor/lzserviceworker.js';

// Note that JS modules are loaded before any assignments to the `self` object
// This means you must use these self properties inside functions, not inside 
// moudle code exected at import time.
self.appConfig = appConfig;
self.settings = settings;
self.staticContentModule = staticContentModule;
self.connectivityService = connectivityService;


