// Test configuration for inscribed rectangle algorithm testing

export const testCases = [
    // {
    //     name: "Test01",
    //     paths: ["Ballroom_1", "Ballroom_3"],
    //     expectedShape: "rectangle",
    //     description: "Two ballrooms side by side should form a perfect rectangle"
    // },
    // {
    //     name: "Test02",
    //     paths: ["Ballroom_1", "Ballroom_Aisle_12", "Ballroom_2"],
    //     expectedShape: "rectangle",
    //     description: "Two ballrooms connected by an aisle"
    // },
    // {
    //     name: "Test03",
    //     paths: ["Ballroom_1", "Ballroom_Aisle_12", "Ballroom_2", "Ballroom_4"],
    //     expectedShape: "complex",
    //     description: "Complex combination of ballrooms and aisle"
    // },
    // Level1 Joins - Vertical joins
    {
        name: "Test01",
        paths: ["Ballroom_1", "Ballroom_3"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test02",
        paths: ["Ballroom_2", "Ballroom_4"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test03",
        paths: ["Ballroom_Aisle_35", "Ballroom_3"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test04",
        paths: ["Ballroom_Aisle_35", "Ballroom_5"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test05",
        paths: ["Ballroom_Aisle_46", "Ballroom_4"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test06",
        paths: ["Ballroom_Aisle_46", "Ballroom_Grand"],
        expectedShape: "",
        description: ""
    },
    // Level1 Joins - Horizontal joins
    {
        name: "Test07",
        paths: ["Ballroom_Aisle_12", "Ballroom_1"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test08",
        paths: ["Ballroom_Aisle_12", "Ballroom_2"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test09",
        paths: ["Ballroom_Aisle_34", "Ballroom_3"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test10",
        paths: ["Ballroom_Aisle_34", "Ballroom_4"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test11",
        paths: ["Ballroom_Aisle_56", "Ballroom_5"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test12",
        paths: ["Ballroom_Aisle_56", "Ballroom_Grand"],
        expectedShape: "",
        description: ""
    },
    // Level1 Joins - Intersection joins
    {
        name: "Test13",
        paths: ["Ballroom_Aisle_3456", "Ballroom_Aisle_34"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test14",
        paths: ["Ballroom_Aisle_3456", "Ballroom_Aisle_35"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test15",
        paths: ["Ballroom_Aisle_3456", "Ballroom_Aisle_46"],
        expectedShape: "",
        description: ""
    },
    {
        name: "Test16",
        paths: ["Ballroom_Aisle_3456", "Ballroom_Aisle_56"],
        expectedShape: "",
        description: ""
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
        minCoverageRatio: 0.98  // 98% minimum threshold
    }
};
