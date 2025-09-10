// SvgViewer class to handle multiple instances
class SvgViewerInstance {
    constructor(containerId, dotNetObjectReference) {
        this.containerId = containerId;
        this.dotNetObjectReference = dotNetObjectReference;
        this.s = null;
        this.fillColor = "#f00";
        this.boundingBoxRect = null;
        this.isUpdating = false;
        this.boundingBoxPathIds = new Set();
        this.selectedIds = null;
        this.selectedPaths = null;
        this.layers = {};
        this.activeLayerKey = null;
    }

    // Active scope = current layer group (or whole paper if none detected)
    scope() {
        return (this.activeLayerKey && this.layers[this.activeLayerKey]) ? this.layers[this.activeLayerKey] : this.s;
    }

    // Find nearest ancestor that is an Inkscape layer; return its label/id key
    findLayerKeyFromNode(node) {
        let el = node;
        while (el && el.nodeType === 1) {
            const gm = el.getAttribute('inkscape:groupmode');
            if (gm === 'layer') {
                const label = el.getAttribute('inkscape:label');
                const id = el.getAttribute('id');
                const key = (label || id || '').trim();
                return key || null;
            }
            el = el.parentNode;
        }
        return null;
    }

    // TRUE if a node is inside the active layer (or no layers detected)
    isInActiveLayer(domNode) {
        if (!this.activeLayerKey || !this.layers[this.activeLayerKey]) return true;
        return this.findLayerKeyFromNode(domNode) === this.activeLayerKey;
    }

    // Discover layers by Inkscape attributes
    bootstrapLayers() {
        this.layers = {};
        const nodes = this.s.selectAll('g[inkscape\\:groupmode="layer"]');
        nodes.forEach(g => {
            const label = g.attr('inkscape:label') || g.attr('id');
            if (!label) return;
            const key = String(label).trim();
            if (key && !this.layers[key]) this.layers[key] = g;
        });
    }

    // Activate a layer by key (label/id). Clears selection to avoid cross-layer mixes.
    activateLayer(name) {
        if (!this.s) return false;
        if (!this.layers[name]) return false;

        this.unselectAllPaths();
        this.activeLayerKey = name;

        // Visual cue: dim non-active layers
        Object.entries(this.layers).forEach(([k, g]) => {
            g.attr({ opacity: k === this.activeLayerKey ? 1 : 0.6 });
        });

        return true;
    }

    async loadSvgAsync(svgContent) {
        if (typeof Snap === 'undefined') {
            throw new Error('Snap.svg is not loaded. Please call initAsync first.');
        }
        
        if (this.s) {
            this.s.selectAll("path").forEach((path) => {
                path.node.removeEventListener("click", this.handleSelection.bind(this));
            });
            this.boundingBoxRect = null;
            this.boundingBoxPathIds.clear();
        }

        let svgElement = document.querySelector(`#${this.containerId}`);
        if (!svgElement) {
            throw new Error(`SVG element with ID "${this.containerId}" not found.`);
        }

        const response = await fetch(svgContent);
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
        const svgText = await response.text();
        
        const fragment = Snap.parse(svgText);
        if (fragment) {
            this.s = Snap(`#${this.containerId}`);
            this.s.append(fragment);
            
            // Make SVG responsive
            const svgEl = this.s.select("svg");
            if (svgEl) {
                svgEl.attr({
                    width: "100%",
                    height: "100%",
                    preserveAspectRatio: "xMidYMid meet"
                });
            }

            // Initialize per-path data + click handler
            this.s.selectAll("path").forEach((path) => {
                path.node.addEventListener("click", this.handleSelection.bind(this));
                path.data("isSelected", false);
                path.data("originalColor", path.attr("fill"));
            });

            // Discover Inkscape layers
            this.bootstrapLayers();
            const keys = Object.keys(this.layers);
            if (keys.length > 0) {
                this.activateLayer(keys[0]);
            } else {
                this.activeLayerKey = null;
            }
        } else {
            throw new Error('Svg could not be parsed');
        }
    }

    handleSelection(event) {
        const path = Snap(event.target);

        // Auto-activate layer if clicked
        const targetKey = this.findLayerKeyFromNode(path.node);
        if (targetKey && this.layers[targetKey] && targetKey !== this.activeLayerKey) {
            this.activateLayer(targetKey);
        }

        if (!this.isInActiveLayer(path.node)) return;

        const id = path.attr("id");
        const isSelected = path.data("isSelected");

        if (isSelected) {
            this.unselectPath(id);
            this.dotNetObjectReference.invokeMethodAsync("OnPathUnselected", id);
        } else {
            this.selectPath(id);
            this.dotNetObjectReference.invokeMethodAsync("OnPathSelected", id);
        }
        
        this.getPaths();
        const mySelectedIds = Array.from(this.selectedIds);
        this.dotNetObjectReference.invokeMethodAsync("OnPathsChanged", mySelectedIds);
    }

    computeIdsInsideBoundingBox(overlapThreshold = 0.5) {
        const inside = new Set();
        if (!this.boundingBoxRect || !this.s) return inside;

        const bbox = this.boundingBoxRect.getBBox();
        const allPaths = this.scope().selectAll("path");

        allPaths.forEach(p => {
            const b = p.getBBox();
            const ix1 = Math.max(b.x, bbox.x);
            const iy1 = Math.max(b.y, bbox.y);
            const ix2 = Math.min(b.x + b.width, bbox.x + bbox.width);
            const iy2 = Math.min(b.y + b.height, bbox.y + bbox.height);

            const iw = Math.max(0, ix2 - ix1);
            const ih = Math.max(0, iy2 - iy1);
            const inter = iw * ih;
            const area = b.width * b.height;

            if (area > 0 && inter / area >= overlapThreshold) {
                const id = p.attr("id");
                if (id) inside.add(id);
            }
        });

        return inside;
    }

    autoSelectInBoundingBox() {
        if (!this.boundingBoxRect || !this.s) return;

        const insideIds = this.computeIdsInsideBoundingBox(0.5);
        const previouslyUpdating = this.isUpdating;
        this.isUpdating = true;

        insideIds.forEach(id => {
            const path = this.s.select("#" + id);
            if (path && !path.data("isSelected") && this.isInActiveLayer(path.node)) {
                this.selectPath(id);
            }
        });

        this.isUpdating = previouslyUpdating;
    }

    updateGlobalBoundingBox() {
        if (this.boundingBoxRect != null) {
            this.boundingBoxRect.remove();
            this.boundingBoxRect = null;
        }

        const selectedPaths = this.scope().selectAll(".is-selected");

        if (selectedPaths && selectedPaths.length > 0) {
            let svg = this.s.select("svg");
            const tempGroup = svg.g();
            selectedPaths.forEach(path => tempGroup.append(path.clone()));

            const bbox = tempGroup.getBBox();
            tempGroup.remove();

            this.boundingBoxRect = svg.rect(bbox.x, bbox.y, bbox.width, bbox.height);
            this.boundingBoxRect.attr({
                stroke: '#00F',
                strokeWidth: 2,
                fill: 'none',
                strokeDasharray: '4 2',
                "pointer-events": "none"
            });
            svg.append(this.boundingBoxRect);

            this.boundingBoxPathIds = this.computeIdsInsideBoundingBox(0.5);

            if (!this.isUpdating) 
                this.autoSelectInBoundingBox();
                
            this.highlight();
        } else {
            this.boundingBoxPathIds.clear();
            this.highlight();
        }
    }

    highlight() {
        this.getPaths();
        const allInsideSelected = [...this.boundingBoxPathIds].every(id => this.selectedIds.has(id));

        this.selectedPaths.forEach(path => {
            path.attr({ fill: allInsideSelected ? "#00FF00" : "#f00" });
        });
    }

    getPaths() {
        this.selectedPaths = this.s ? this.scope().selectAll(".is-selected") : [];
        this.selectedIds = new Set();
        this.selectedPaths.forEach(p => this.selectedIds.add(p.attr("id")));
    }

    selectPath(pathId) {
        if (!this.s) return false;
        let path = this.s.select("#" + pathId);
        if (!path) return false;

        if (!this.isInActiveLayer(path.node)) return false;
        if (path.data("isSelected") === true) return false;

        path.data("isSelected", true);
        path.attr({ fill: this.fillColor });
        path.addClass("is-selected");

        if (!this.isUpdating) {
            this.updateGlobalBoundingBox();
        } else {
            this.highlight();
        }
        return true;
    }

    selectPaths(paths) {
        if (!this.s || !Array.isArray(paths)) return;
        this.isUpdating = true;
        const ids = paths
            .filter((id) => id != null && String(id).trim() !== "")
            .map((id) => String(id).trim());

        for (const id of ids) {
            const path = this.s.select("#" + id);
            if (!path) continue;
            if (path.data("isSelected") === true) continue;

            path.data("isSelected", true);
            path.attr({ fill: this.fillColor });
            path.addClass("is-selected");
        }
        this.updateGlobalBoundingBox();
        this.isUpdating = false;
        return true;
    }

    unselectPath(pathId) {
        if (!this.s) return false;
        let path = this.s.select("#" + pathId);
        if (!path) return false;

        if (!this.isInActiveLayer(path.node)) return false;
        if (path.data("isSelected") === false) return false;

        const prevIsUpdating = this.isUpdating;
        this.isUpdating = true;

        let originalColor = path.data("originalColor");
        path.data("isSelected", false);
        path.attr({ fill: originalColor });
        path.removeClass("is-selected");

        this.updateGlobalBoundingBox();

        this.isUpdating = prevIsUpdating;
        this.highlight();
        return true;
    }

    unselectAllPaths() {
        if (!this.s) return;
        this.s.selectAll(".is-selected").forEach((path) => {
            let originalColor = path.data("originalColor");
            path.data("isSelected", false);
            path.attr({ fill: originalColor });
            path.removeClass("is-selected");
        });
        if (this.boundingBoxRect) {
            this.boundingBoxRect.remove();
            this.boundingBoxRect = null;
        }
        this.boundingBoxPathIds.clear();
        this.highlight();
    }
}

// Global instance management
const instances = new Map();
let snapLoadingPromise = null;

export function initAsync(containerId, dotNetObjectReference) {
    let url = './_content/LazyMagic.BlazorSvg/snap.svg.js';
    
    // If already loading, return the existing promise
    if (snapLoadingPromise) {
        return snapLoadingPromise.then(() => {
            const instance = new SvgViewerInstance(containerId, dotNetObjectReference);
            instances.set(containerId, instance);
            return containerId;
        });
    }
    
    // Check if already loaded
    if (typeof Snap !== 'undefined') {
        const instance = new SvgViewerInstance(containerId, dotNetObjectReference);
        instances.set(containerId, instance);
        return Promise.resolve(containerId);
    }
    
    // Check if script tag exists but Snap might still be loading
    if (document.querySelector('script[src="' + url + '"]')) {
        snapLoadingPromise = new Promise((resolve) => {
            const checkSnap = setInterval(() => {
                if (typeof Snap !== 'undefined') {
                    clearInterval(checkSnap);
                    snapLoadingPromise = null;
                    resolve();
                }
            }, 50);
        });
        return snapLoadingPromise.then(() => {
            const instance = new SvgViewerInstance(containerId, dotNetObjectReference);
            instances.set(containerId, instance);
            return containerId;
        });
    }
    
    // Create and load the script
    snapLoadingPromise = new Promise((resolve, reject) => {
        let script = document.createElement('script');
        script.src = url;
        script.onload = () => {
            snapLoadingPromise = null;
            resolve();
        };
        script.onerror = () => {
            snapLoadingPromise = null;
            reject('Snap.svg could not be loaded');
        };
        document.head.appendChild(script);
    });
    
    return snapLoadingPromise.then(() => {
        const instance = new SvgViewerInstance(containerId, dotNetObjectReference);
        instances.set(containerId, instance);
        return containerId;
    });
}

export async function loadSvgAsync(containerId, svgContent) {
    const instance = instances.get(containerId);
    if (!instance) throw new Error(`No instance found for container ${containerId}`);
    await instance.loadSvgAsync(svgContent);
}

export function selectPath(containerId, pathId) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.selectPath(pathId);
}

export function selectPaths(containerId, paths) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.selectPaths(paths);
}

export function unselectPath(containerId, pathId) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.unselectPath(pathId);
}

export function unselectAllPaths(containerId) {
    const instance = instances.get(containerId);
    if (!instance) return;
    instance.unselectAllPaths();
}

export function activateLayer(containerId, name) {
    const instance = instances.get(containerId);
    if (!instance) return false;
    return instance.activateLayer(name);
}

export function disposeInstance(containerId) {
    instances.delete(containerId);
}