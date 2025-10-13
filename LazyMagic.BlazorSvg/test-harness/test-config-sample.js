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
  }
];

export const config = {
  svgPath: '../BlazorTest.WASM/wwwroot/Level1.svg'
};
