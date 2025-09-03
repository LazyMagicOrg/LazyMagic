let dotNetObjectReference;
let s;
let fillColor = "#f00";
let boundingBoxRect;
let isUpdating = false;
let boundingBoxPathIds = new Set();
let selectedIds;
let selectedPaths;

// --- Dynamic LAYERS (Inkscape) ---
let layers = {};           // { <label>: SnapElement }
let activeLayerKey = null; // current label (lowercase, no spaces/specials as you noted)

// Active scope = current layer group (or whole paper if none detected)
function scope() {
    return (activeLayerKey && layers[activeLayerKey]) ? layers[activeLayerKey] : s;
}

// Find nearest ancestor that is an Inkscape layer; return its label/id key
function findLayerKeyFromNode(node) {
    let el = node;
    while (el && el.nodeType === 1) {
        const gm = el.getAttribute('inkscape:groupmode');
        if (gm === 'layer') {
            // Prefer inkscape:label, fall back to id
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
function isInActiveLayer(domNode) {
    if (!activeLayerKey || !layers[activeLayerKey]) return true;
    return findLayerKeyFromNode(domNode) === activeLayerKey;
}

// Discover layers by Inkscape attributes
function bootstrapLayers() {
    layers = {};
    const nodes = s.selectAll('g[inkscape\\:groupmode="layer"]');
    nodes.forEach(g => {
        const label = g.attr('inkscape:label') || g.attr('id');
        if (!label) return;
        const key = String(label).trim(); // you said labels are clean lowercase without spaces/specials
        if (key && !layers[key]) layers[key] = g;
    });
}

// Activate a layer by key (label/id). Clears selection to avoid cross-layer mixes.
// We keep pointer events enabled so you can click on another layer to switch.
export function activateLayer(name) {
    if (!s) return false;
    if (!layers[name]) return false;

    unselectAllPaths();

    activeLayerKey = name;

    // Visual cue: dim non-active layers (pointer events remain enabled for click-to-switch)
    Object.entries(layers).forEach(([k, g]) => {
        g.attr({ opacity: k === activeLayerKey ? 1 : 0.6 });
    });

    return true;
}

export function initAsync(dotNetObjectReferenceArg) {
    let url = './_content/LazyMagic.BlazorSvg/snap.svg.js';
    dotNetObjectReference = dotNetObjectReferenceArg;
    return new Promise((resolve, reject) => {
        if (document.querySelector('script[src="' + url + '"]')) {
            resolve();
            return;
        }
        let script = document.createElement('script');
        script.src = url;
        script.onload = () => resolve();
        script.onerror = () => reject('Snap.svg could not be loaded');
        document.head.appendChild(script);
    });
}

export function loadSvgAsync(svgContent) {
    if (s) {
        s.selectAll("path").forEach(function (path) {
            path.node.removeEventListener("click", handleSelection);
        });
        // s.clear();
        boundingBoxRect = null;
        boundingBoxPathIds.clear();
    }

    let svgElement = document.querySelector("#svg");
    if (!svgElement) {
        return Promise.reject('SVG element with ID "svg" not found.');
    }

    return new Promise((resolve, reject) => {
        fetch(svgContent)
            .then(response => {
                if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
                return response.text();
            })
            .then(svgText => {
                const fragment = Snap.parse(svgText);
                if (fragment) {
                    s = Snap("#svg");
                    s.append(fragment);

                    // Initialize per-path data + click handler
                    s.selectAll("path").forEach(function (path) {
                        path.node.addEventListener("click", handleSelection);
                        path.data("isSelected", false);
                        path.data("originalColor", path.attr("fill"));
                    });

                    // Discover Inkscape layers and pick a default (first found)
                    bootstrapLayers();
                    const keys = Object.keys(layers);
                    if (keys.length > 0) {
                        activateLayer(keys[0]);
                    } else {
                        activeLayerKey = null; // no layers detected -> global scope
                    }

                    resolve();
                } else {
                    reject('Svg could not be parsed');
                }
            })
            .catch(error => {
                reject(`Error loading SVG: ${error.message}`);
            });
    });
}

function handleSelection(event) {
    const path = Snap(event.target);

    // If click happened in another layer, auto-activate it first.
    const targetKey = findLayerKeyFromNode(path.node);
    if (targetKey && layers[targetKey] && targetKey !== activeLayerKey) {
        activateLayer(targetKey);
    }

    // Now only respond if the clicked node is in the active layer
    if (!isInActiveLayer(path.node)) return;

    const id = path.attr("id");
    const isSelected = path.data("isSelected");

    if (isSelected) {
        unselectPath(id);
        dotNetObjectReference.invokeMethodAsync("OnPathUnselected", id);
    } else {
        selectPath(id);
        dotNetObjectReference.invokeMethodAsync("OnPathSelected", id);
    }
    getPaths();
    dotNetObjectReference.invokeMethodAsync("OnPathsChanged", (... selectedIds));
}

/**
 * Compute IDs of all paths whose bbox overlaps the current selection box
 * by at least overlapThreshold (default 0.5). Scoped to ACTIVE LAYER.
 */
function computeIdsInsideBoundingBox(overlapThreshold = 0.5) {
    const inside = new Set();
    if (!boundingBoxRect || !s) return inside;

    const bbox = boundingBoxRect.getBBox();
    const allPaths = scope().selectAll("path");

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

/**
 * Auto-select any path ≥ threshold inside the current selection box.
 * Uses guard to avoid recursive bbox updates. Scoped to ACTIVE LAYER.
 */
function autoSelectInBoundingBox() {
    if (!boundingBoxRect || !s) return;

    const insideIds = computeIdsInsideBoundingBox(0.5);
    const previouslyUpdating = isUpdating;
    isUpdating = true;

    insideIds.forEach(id => {
        const path = s.select("#" + id);
        if (path && !path.data("isSelected") && isInActiveLayer(path.node)) {
            selectPath(id); // guarded: won't recompute bbox
        }
    });

    isUpdating = previouslyUpdating;
    // highlight() will be called by updateGlobalBoundingBox()
}

function updateGlobalBoundingBox() {
    if (boundingBoxRect != null) {
        boundingBoxRect.remove();
        boundingBoxRect = null;
    }

    const selectedPaths = scope().selectAll(".is-selected");

    if (selectedPaths && selectedPaths.length > 0) {
        let svg = s.select("svg");
        const tempGroup = svg.g();
        selectedPaths.forEach(path => tempGroup.append(path.clone()));

        const bbox = tempGroup.getBBox();
        tempGroup.remove();

        boundingBoxRect = svg.rect(bbox.x, bbox.y, bbox.width, bbox.height);
        boundingBoxRect.attr({
            stroke: '#00F',
            strokeWidth: 2,
            fill: 'none',
            strokeDasharray: '4 2',
            "pointer-events": "none"
        });
        svg.append(boundingBoxRect);

        // Geometry set from CURRENT box within ACTIVE LAYER
        boundingBoxPathIds = computeIdsInsideBoundingBox(0.5);

        if (!isUpdating) {
            autoSelectInBoundingBox();
            highlight();
        } else {
            highlight();
        }
    } else {
        boundingBoxPathIds.clear();
        highlight();
    }
}

function highlight() {
    getPaths();
    const allInsideSelected = [...boundingBoxPathIds].every(id => selectedIds.has(id));

    selectedPaths.forEach(path => {
        path.attr({ fill: allInsideSelected ? "#00FF00" : "#f00" });
    });
}

function getPaths() {
    selectedPaths = s ? scope().selectAll(".is-selected") : [];
    selectedIds = new Set();
    selectedPaths.forEach(p => selectedIds.add(p.attr("id")));

}
export function selectPath(pathId) {
    if (s === undefined) return false;
    let path = s.select("#" + pathId);
    if (!path) return false;

    if (!isInActiveLayer(path.node)) return false;
    if (path.data("isSelected") === true) return false;

    path.data("isSelected", true);
    path.attr({ fill: fillColor });
    path.addClass("is-selected");

    if (!isUpdating) {
        updateGlobalBoundingBox();
    } else {
        highlight();
    }
    return true;
}

export function unselectPath(pathId) {
    if (s === undefined) return false;
    let path = s.select("#" + pathId);
    if (!path) return false;

    if (!isInActiveLayer(path.node)) return false;
    if (path.data("isSelected") === false) return false;

    const prevIsUpdating = isUpdating;
    isUpdating = true;

    let originalColor = path.data("originalColor");
    path.data("isSelected", false);
    path.attr({ fill: originalColor });
    path.removeClass("is-selected");

    updateGlobalBoundingBox();

    isUpdating = prevIsUpdating;
    highlight();
    return true;
}

export function unselectAllPaths() {
    if (!s) return;
    s.selectAll(".is-selected").forEach(function (path) {
        let originalColor = path.data("originalColor");
        path.data("isSelected", false);
        path.attr({ fill: originalColor });
        path.removeClass("is-selected");
    });
    if (boundingBoxRect) {
        boundingBoxRect.remove();
        boundingBoxRect = null;
    }
    boundingBoxPathIds.clear();
    highlight();
}
