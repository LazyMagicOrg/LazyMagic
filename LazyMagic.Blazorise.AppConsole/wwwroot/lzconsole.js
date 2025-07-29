let originalConsole = {
    log: console.log,
    warn: console.warn,
    error: console.error,
    info: console.info
};

let db;
const dbName = "ClaudeConsoleLogsDB";
const storeName = "Logs";

async function initIndexedDB() {
    return new Promise((resolve, reject) => {
        console.log("Initializing IndexedDB...");
        const request = indexedDB.open(dbName, 1);

        request.onerror = () => {
            console.error("IndexedDB init failed");
            reject("IndexedDB init failed.");
        };

        request.onsuccess = (event) => {
            db = event.target.result;
            console.log("IndexedDB initialized successfully:", dbName);
            resolve();
        };

        request.onupgradeneeded = (event) => {
            db = event.target.result;
            if (!db.objectStoreNames.contains(storeName)) {
                db.createObjectStore(storeName, { keyPath: "id", autoIncrement: true });
                console.log("Created object store:", storeName);
            }
        };
    });
}

function storeLogToDB(logEntry) {
    if (!db) {
        console.warn("Tried to store log but DB is not initialized:", logEntry);
        return;
    }

    const tx = db.transaction(storeName, "readwrite");
    const store = tx.objectStore(storeName);
    store.add(logEntry);
}

function interceptConsole(type, fn) {
    console[type] = function (...args) {
        const message = args.join(" ");
        const timestamp = new Date().toISOString();

        const logEntry = {
            type,
            message,
            timestamp
        };

        storeLogToDB(logEntry);
        fn.apply(console, args);

        if (window.dotNetRef) {
            window.dotNetRef.invokeMethodAsync("OnConsoleEvent", `[${type.toUpperCase()}] ${message}`);
        }
    };
}

export async function initConsole(selector) {
    const el = document.querySelector(selector);
    if (el) {
        el.innerHTML = `
            <div style="font-family: monospace; background: #111; color: #0f0; padding: 10px;">
                Claude Console Initialized
            </div>
        `;
    }

    await initIndexedDB();

    // Clear logs BEFORE intercepting
    const tx = db.transaction(storeName, "readwrite");
    const store = tx.objectStore(storeName);
    await store.clear();
    console.log("Cleared logs on init");

    interceptConsole("log", originalConsole.log);
    interceptConsole("warn", originalConsole.warn);
    interceptConsole("error", originalConsole.error);
    interceptConsole("info", originalConsole.info);

    console.log("Console interception active");
}


export function setDotNetRef(dotNetHelper) {
    window.dotNetRef = dotNetHelper;
}

export async function getAllLogs() {
    return new Promise((resolve, reject) => {
        if (!db) {
            reject("IndexedDB not initialized for reading");
            return;
        }

        const logs = [];
        const tx = db.transaction(storeName, "readonly");
        const store = tx.objectStore(storeName);
        const cursorReq = store.openCursor();

        cursorReq.onerror = () => reject("Error reading logs.");
        cursorReq.onsuccess = (event) => {
            const cursor = event.target.result;
            if (cursor) {
                logs.push(cursor.value);
                cursor.continue();
            } else {
                resolve(logs);
            }
        };
    });
}

export async function clearLogs() {
    if (!db) {
        console.warn("DB not initialized. Initializing now...");
        await initIndexedDB();
    }

    const tx = db.transaction(storeName, "readwrite");
    const store = tx.objectStore(storeName);
    store.clear();

    console.log("Cleared logs from IndexedDB");
}


// Optional test helper
window.lzconsole = {
    testIndexedDBWrite: async function () {
        await initIndexedDB();
        storeLogToDB({
            type: "log",
            message: "Manual test from testIndexedDBWrite()",
            timestamp: new Date().toISOString()
        });
        console.log("Test log stored manually.");
    },
    clearLogs
};
