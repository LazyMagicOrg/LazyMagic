// Test configuration for inscribed rectangle algorithm testing

export const testCases = [
    {
        name: "Test01",
        paths: ["Ballroom_1", "Ballroom_3"],
        expectedShape: "rectangle",
        description: "Two ballrooms side by side should form a perfect rectangle"
    },
    {
        name: "Test02",
        paths: ["Ballroom_1", "Ballroom_Aisle_12", "Ballroom_2"],
        expectedShape: "rectangle",
        description: "Two ballrooms connected by an aisle"
    },
    {
        name: "Test03",
        paths: ["Ballroom_1", "Ballroom_Aisle_12", "Ballroom_2", "Ballroom_4"],
        expectedShape: "complex",
        description: "Complex combination of ballrooms and aisle"
    }
];

export const config = {
    // Path to the SVG file
    svgPath: "../../BlazorTest.WASM/wwwroot/Level1.svg",

    // Path to the SvgViewer.js library
    svgViewerPath: "../wwwroot/SvgViewer.js",

    // Output directory for test results
    outputDir: "./test-results",

    // Validation thresholds
    validation: {
        // Maximum gap between inscribed rectangle and boundary (pixels)
        maxGap: 2,

        // Maximum breach outside boundary (pixels)
        maxBreach: 0.5,

        // Minimum area coverage ratio (inscribed area / boundary area)
        minCoverageRatio: 0.85
    }
};
