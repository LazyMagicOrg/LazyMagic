// Algorithm parameter configurations for testing

export const parameterSets = {
    baseline: {
        name: "Baseline (Current)",
        description: "Current production parameters",
        gridStep: 12.0,
        polylabelPrecision: 1.0,
        aspectRatios: [0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0],
        binarySearchPrecision: 0.001,
        binarySearchMaxIterations: 15
    },

    finerGrid: {
        name: "Finer Grid",
        description: "Reduce grid step to 8px for more centroid samples",
        gridStep: 8.0,
        polylabelPrecision: 1.0,
        aspectRatios: [0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0],
        binarySearchPrecision: 0.001,
        binarySearchMaxIterations: 15
    },

    moreAspects: {
        name: "More Aspect Ratios",
        description: "Add more aspect ratio samples for better exploration",
        gridStep: 12.0,
        polylabelPrecision: 1.0,
        aspectRatios: [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0],
        binarySearchPrecision: 0.001,
        binarySearchMaxIterations: 15
    },

    fineTuned: {
        name: "Fine Tuned Binary Search",
        description: "Increase binary search precision and iterations",
        gridStep: 12.0,
        polylabelPrecision: 1.0,
        aspectRatios: [0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0],
        binarySearchPrecision: 0.0001,
        binarySearchMaxIterations: 20
    },

    aggressive: {
        name: "Aggressive",
        description: "Combine finer grid, more aspects, and better search",
        gridStep: 8.0,
        polylabelPrecision: 0.5,
        aspectRatios: [0.5, 0.6, 0.7, 0.85, 1.0, 1.2, 1.4, 1.7, 2.0, 2.3, 2.5, 2.8, 3.0],
        binarySearchPrecision: 0.0001,
        binarySearchMaxIterations: 20
    },

    veryFine: {
        name: "Very Fine Grid",
        description: "6px grid for maximum centroid sampling",
        gridStep: 6.0,
        polylabelPrecision: 0.5,
        aspectRatios: [0.5, 0.7, 1.0, 1.4, 2.0, 2.5, 3.0],
        binarySearchPrecision: 0.001,
        binarySearchMaxIterations: 15
    }
};

// Helper to apply parameters to the algorithm
export function applyParameters(SvgViewerOptimized, paramSet) {
    // Note: This would require modifying SvgViewerOptimized.js to accept parameters
    // For now, we'll document what needs to change
    return {
        instructions: `
To apply ${paramSet.name}:
1. Set gridStep to ${paramSet.gridStep} (line ~703)
2. Set polylabel precision to ${paramSet.polylabelPrecision} (line ~661)
3. Set aspectRatios to [${paramSet.aspectRatios.join(', ')}] (line ~1413)
4. Set binary search precision to ${paramSet.binarySearchPrecision} (line ~1430)
5. Set maxIterations to ${paramSet.binarySearchMaxIterations} (line ~1429)
        `.trim()
    };
}

// Export default set for quick testing
export const defaultTestSet = 'baseline';
