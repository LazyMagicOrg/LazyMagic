// Sample test configuration - just 6 tests
export const testCases = [
  {
    "name": "Combo_0001",
    "paths": ["Ballroom_Room_1"],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Room_1"
  },
  {
    "name": "Combo_0002",
    "paths": ["Ballroom_Room_2"],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Room_2"
  },
  {
    "name": "Combo_0003",
    "paths": ["Ballroom_Room_3"],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Room_3"
  },
  {
    "name": "Combo_0004",
    "paths": ["Ballroom_Room_1", "Ballroom_Room_3"],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Room_1_Ballroom_Room_3"
  },
  {
    "name": "Combo_0005",
    "paths": ["Ballroom_Room_4"],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Room_4"
  },
  {
    "name": "Combo_0006",
    "paths": ["Ballroom_Room_2", "Ballroom_Room_4"],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Room_2_Ballroom_Room_4"
  },
  {
    "name": "Combo_0007",
    "paths": [
      "Ballroom_Room_1",
      "Ballroom_Room_2",
      "Ballroom_Room_3",
      "Ballroom_Room_4",
      "Ballroom_Aisle_12",
      "Ballroom_Aisle_34"
    ],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Aisle_12_Ballroom_Aisle_34_Ballroom_Room_1_Ballroom_Room_2_Ballroom_Room_3_Ballroom_Room_4"
  },
  {
    "name": "Combo_0008",
    "paths": [
      "Ballroom_Room_1",
      "Ballroom_Room_2",
      "Ballroom_Room_3",
      "Ballroom_Aisle_12"
    ],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Aisle_12_Ballroom_Room_1_Ballroom_Room_2_Ballroom_Room_3"
  },
  {
    "name": "Combo_0009",
    "paths": [
      "Ballroom_Room_1",
      "Ballroom_Room_3",
      "Ballroom_Room_4",
      "Ballroom_Aisle_34"
    ],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Aisle_34_Ballroom_Room_1_Ballroom_Room_3_Ballroom_Room_4"
  },
  {
    "name": "Combo_0010",
    "paths": [
      "Ballroom_Room_3",
      "Ballroom_Room_5",
      "Ballroom_Room_Grand",
      "Ballroom_Aisle_35",
      "Ballroom_Aisle_56"
    ],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Aisle_35_Ballroom_Aisle_56_Ballroom_Room_3_Ballroom_Room_5_Ballroom_Room_Grand"
  },
  {
    "name": "Combo_0011",
    "paths": [
      "Ballroom_Room_2",
      "Ballroom_Room_4",
      "Ballroom_Room_5",
      "Ballroom_Room_Grand",
      "Ballroom_Aisle_12",
      "Ballroom_Aisle_34",
      "Ballroom_Aisle_46",
      "Ballroom_Aisle_56",
      "Ballroom_Crossing_3456"
    ],
    "goalRectangle": null,
    "expectedShape": "",
    "description": "Ballroom_Aisle_12_Ballroom_Aisle_34_Ballroom_Aisle_46_Ballroom_Aisle_56_Ballroom_Crossing_3456_Ballroom_Room_2_Ballroom_Room_4_Ballroom_Room_5_Ballroom_Room_Grand"
  }
];

export const config = {
  svgPath: '../../BlazorTest.WASM/wwwroot/Level1.svg'
};
