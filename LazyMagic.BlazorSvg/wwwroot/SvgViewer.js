let dotNetObjectReference;
let s;
let fillColor = "#f00";
// Snap.svg is not a ES6 module, so we have to load and attach it
// to the window object as a script tag.
export function initAsync(dotNetObjectReferenceArg) {
    let url = './_content/LazyMagic.BlazorSvg/snap.svg.js';
    dotNetObjectReference = dotNetObjectReferenceArg;
    return new Promise((resolve, reject) => {
        if (document.querySelector('script[src="' + url + '"]')) {
            resolve(); // Already loaded
            return;
        }
        let script = document.createElement('script');
        script.src = url;
        script.onload = () => resolve();
        script.onerror = () => reject('Snap.svg could not be loaded');
        document.head.appendChild(script);
    });
}
// Application Code
export function loadSvgAsync(svgContent) {
    if (s) {
        s.selectAll("path").forEach(function (path) {
            let domPath = path.node;
            if (domPath._handleSelection) {
                domPath.removeEventListener("click", domPath._handleSelection);
                delete domPath._handleSelection; // Remove the reference
            }
        });
        
    }
    let svgElement = document.querySelector("#svg");
    if (svgElement)
        svgElement.innerHTML = "";
    s = Snap("#svg");
    return new Promise((resolve, reject) => {
        // Using fetch instead of Snap.load because of CORS issues
        fetch(svgContent)
            .then(response => {
                if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`)
                return response.text();
            })
            .then(svgText => { 
                let f = Snap.parse(svgText);

                if (f) {
                    s.append(f);
                    let svg = s.select("svg");
                    let viewBox = svg.attr("viewBox");
                    svg.attr({ width: "100%", height: "100%", preserveAspectRatio: "xMidYMid meet" });
                    s.selectAll("path").forEach(function (path) {
                        path.node.addEventListener("click", handleSelection);
                    });
                    resolve();
                }
                else
                    reject('Svg could not be loaded');
            })
            .catch(error => {
                reject(`Error loading SVG: ${error.message}`);
            });
    });
}

function handleSelection(event) {
    const path = Snap(event.target); // Wrap the DOM element with Snap
    const currentFillColor = path.attr("fill");
    const id = path.attr("id");
    if (currentFillColor !== hexToRgb(fillColor)) {
        if (selectPath(id))
            dotNetObjectReference.invokeMethodAsync("OnPathSelected", id);
    } else {
        if (unselectPath(id))
            dotNetObjectReference.invokeMethodAsync("OnPathUnselected", id);
    }
}
export function selectPath(pathId) {
    if (s === undefined) return false;
    let path = s.select("#" + pathId);
    if(path === undefined || path === null) return false;
    if (path?.data("isSelected") === true) return false;
    let originalColor = path.attr("fill");
    path.data("isSelected", true);
    path.data("originalColor", originalColor);
    path.attr({ fill: fillColor }); // Change fill color for selection   
    return true;
}
export function  unselectPath(pathId) {
    if (s === undefined) return false;
    let path = s.select("#" + pathId);
    if(path === undefined || path === null) return false;
    if (path.data("isSelected") === false) return false;
    let originalColor = path.data("originalColor");
    path.data("isSelected", false);
    path.attr({ fill: originalColor });  
    return true;
}
export function unselectAllPaths() {   
    if(s === undefined || s === null) return;
    s.selectAll("path").forEach(function (path) {
        unselectPath(path.node.id);
    });
}   
function hexToRgb(hex) {
    // Expand shorthand form (e.g. "03F") to full form (e.g. "0033FF")
    let fullHex = hex.length === 4 ? '#' + hex[1] + hex[1] + hex[2] + hex[2] + hex[3] + hex[3] : hex;

    let r = parseInt(fullHex.substring(1, 3), 16);
    let g = parseInt(fullHex.substring(3, 5), 16);
    let b = parseInt(fullHex.substring(5, 7), 16);

    return `rgb(${r}, ${g}, ${b})`;
}
