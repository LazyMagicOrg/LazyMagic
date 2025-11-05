# Level Data JSON Schema Documentation

This document describes the structure and properties of the level data JSON file (e.g., `Level1-data.json`) used by the FloorMat pipeline.

**Last Updated:** 2025-11-05

## Overview

The level data JSON file (formerly called `Rooms.json`, now named `[VenueName]-data.json`) defines the venue layout structure, including rooms, sections, joins, and their connectivity. It is used by `compute-all-combinations.js` to generate valid section combinations for layout testing.

## File Structure

```json
[
  {
    "Id": "Level1",
    "Name": "Level 1",
    "Diagram": { ... },
    "Rooms": [ ... ]
  }
]
```

## Root Level

The root is an **array of levels**. Each level represents a floor or area of the venue.

### Level Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier for the level (e.g., "Level1", "Level2") |
| `Name` | string | Yes | Display name for the level |
| `Diagram` | object | Yes | SVG diagram configuration |
| `Rooms` | array | Yes | Array of room definitions |

### Diagram Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Image` | string | Yes | Path to the SVG file |
| `MeasureUnits` | string | Yes | Measurement system: "Imperial" or "Metric" |
| `Unit` | string | Yes | Unit of measurement: "ft", "m", etc. |

## Room Structure

Each room contains sections that can be combined.

### Room Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | string | Yes | Unique identifier for the room |
| `RoomSections` | array | Yes | Array of section definitions |
| `Joins` | array | No | Array of connectivity rules between sections |

## Section Structure

Sections are the individual areas that can be combined for layout generation.

### Section Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Id` | string | Yes | - | Unique identifier matching SVG path ID |
| `Name` | string | Yes | - | Display name for the section |
| `Description` | string | No | - | Description of the section |
| **`Area`** | number | No | calculated | **OPTIONAL OVERRIDE:** Area in square units. If specified, overrides calculated polygon area |
| **`Width`** | number | No | calculated | **OPTIONAL OVERRIDE:** Width in units. If specified, overrides calculated dimension |
| **`Depth`** | number | No | calculated | **OPTIONAL OVERRIDE:** Depth/height in units. If specified, overrides calculated dimension |
| `SectionType` | string | No | "Room" | Type: "Room", "Aisle", or "Crossing" |
| **`LayoutRestriction`** | string | No | "allowed" | **Layout generation restriction** |
| `LayoutRestrictionReason` | string | No | - | Explanation for the restriction |

### Measurement Overrides (Area, Width, Depth)

By default, the FloorMat pipeline **calculates measurements directly from SVG geometry**. However, you can optionally specify `Area`, `Width`, and `Depth` properties to **override the calculated values**.

**When to use overrides:**
- **Client specifications:** Client measures the room differently than the CAD dimensions
- **Marketing requirements:** Round numbers preferred for marketing materials (e.g., 1400 sq ft instead of 1395.7)
- **Insurance/compliance:** Official measurements differ from architectural drawings
- **Usable space adjustments:** Account for permanent fixtures not reflected in SVG outline

**How it works:**
1. If `Area`/`Width`/`Depth` are **omitted**: Pipeline uses values calculated from SVG polygon geometry
2. If `Area`/`Width`/`Depth` are **specified**: Pipeline uses your provided values instead

**Example without overrides (most common):**
```json
{
  "Id": "Ballroom_Room_1",
  "Name": "Ballroom 1",
  "LayoutRestriction": "allowed"
}
```
_Area, width, and depth calculated automatically from SVG_

**Example with overrides (special cases):**
```json
{
  "Id": "Ballroom_Room_1",
  "Name": "Ballroom 1",
  "Area": 2700,
  "Width": 60.0,
  "Depth": 45.0,
  "LayoutRestriction": "allowed"
}
```
_Override values will be used instead of calculated measurements_

### LayoutRestriction Values

The `LayoutRestriction` property controls whether layouts are generated for combinations including this section:

#### "allowed" (Default)
- Full layout generation with no warnings
- Use for standard, unobstructed spaces
- **Example:** Regular ballroom sections

```json
{
  "Id": "Ballroom_Room_1",
  "Name": "Ballroom 1",
  "Area": 2668,
  "Width": 58.0,
  "Depth": 46.0,
  "LayoutRestriction": "allowed"
}
```

#### "warning"
- Layouts are generated but include a warning banner
- Use for spaces with obstacles, structural features, or constraints
- Generated SVG files will display a yellow warning banner
- **Example:** Areas with central pools, columns, or irregular features

```json
{
  "Id": "PoolArea_Room_Main",
  "Name": "Pool Area",
  "Area": 5000,
  "Width": 100.0,
  "Depth": 50.0,
  "LayoutRestriction": "warning",
  "LayoutRestrictionReason": "Central pool feature may interfere with table placement"
}
```

#### "restricted"
- Layouts are generated but marked as restricted (for reference only)
- Generated SVG files will display a red restriction banner
- JSON output includes `"layoutRestriction": "restricted"` metadata
- Use for spaces unsuitable for table arrangements but where reference data may be useful
- **Example:** Aisles, narrow corridors, equipment areas

```json
{
  "Id": "Ballroom_Aisle_12",
  "SectionType": "Aisle",
  "Name": "Aisle 12",
  "Area": 580,
  "Width": 58.0,
  "Depth": 10.0,
  "LayoutRestriction": "restricted",
  "LayoutRestrictionReason": "Aisle too narrow for table placement"
}
```

### When to Use Each Restriction Level

**Use "allowed" for:**
- Standard ballrooms and meeting rooms
- Open spaces without obstacles
- Areas designed specifically for table arrangements

**Use "warning" for:**
- Spaces with central features (pools, fountains, stages)
- Areas with numerous structural columns
- Rooms with irregular shapes that may reduce usable space
- Outdoor areas with natural features (trees, gardens)
- Spaces with limited ceiling heights in portions of the area

**Use "restricted" for:**
- Narrow aisles and corridors
- Equipment rooms and storage areas
- Spaces primarily for circulation
- Areas with permanent fixed seating
- Very small spaces (< 200 sq ft for most use cases)
- Note: Data will still be generated for analysis/reference purposes

## Join Structure

Joins define adjacency relationships between sections.

### Join Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Orientation` | string | Yes | Connection type: "Vertical", "Horizontal", or "Intersection" |
| `Section1Id` | string | Yes | ID of first section |
| `Section2Id` | string | Yes | ID of second section |

### Orientation Types

- **"Vertical"**: Sections are stacked top-to-bottom
- **"Horizontal"**: Sections are side-by-side left-to-right
- **"Intersection"**: Crossing/aisle intersection point

## Example: Complete Room Definition

```json
{
  "Id": "Ballroom",
  "RoomSections": [
    {
      "Id": "Ballroom_Room_1",
      "Name": "Ballroom 1",
      "Description": "Main ballroom section",
      "Area": 2668,
      "Width": 58.0,
      "Depth": 46.0,
      "LayoutRestriction": "allowed"
    },
    {
      "Id": "Ballroom_Aisle_12",
      "SectionType": "Aisle",
      "Name": "Aisle 12",
      "Description": "Connects Ballroom 1 and Ballroom 2",
      "Area": 580,
      "Width": 58.0,
      "Depth": 10.0,
      "LayoutRestriction": "restricted",
      "LayoutRestrictionReason": "Aisle too narrow for table placement"
    },
    {
      "Id": "Ballroom_Room_Pool",
      "Name": "Ballroom with Pool",
      "Description": "Ballroom section with central pool",
      "Area": 3500,
      "Width": 70.0,
      "Depth": 50.0,
      "LayoutRestriction": "warning",
      "LayoutRestrictionReason": "Central pool feature reduces usable table space"
    }
  ],
  "Joins": [
    {
      "Orientation": "Horizontal",
      "Section1Id": "Ballroom_Aisle_12",
      "Section2Id": "Ballroom_Room_1"
    },
    {
      "Orientation": "Vertical",
      "Section1Id": "Ballroom_Room_1",
      "Section2Id": "Ballroom_Room_Pool"
    }
  ]
}
```

## Validation Rules

The FloorMat pipeline validates combinations using these rules:

1. **Single section must be Room** (not Aisle/Crossing)
2. **Shared aisles must be included** when both connected rooms are selected
3. **Aisles can be included with single rooms**
4. **Crossings require multiple aisles**
5. **All sections must form connected components**
6. **At least one room must be included**
7. **Aisles must have at least one connected room**
8. **Layout restrictions are tracked as metadata** (all combinations are generated, restriction level determines display treatment)

## Pipeline Behavior

### With LayoutRestriction

1. **Combination Generation** (`compute-all-combinations.js`):
   - **All valid combinations are generated**, regardless of restriction level
   - Each combination includes `layoutRestriction` metadata: "allowed", "warning", or "restricted"
   - Restriction level is determined by the most restrictive section in the combination

2. **Layout Generation** (`run-tests.js`):
   - **All combinations are processed** and layouts are computed
   - Restriction metadata is passed through to output files
   - SVG generation includes appropriate banners based on restriction level

3. **Output Files**:
   - **"allowed" combinations**: Standard SVG with layout, no banner
   - **"warning" combinations**: SVG with yellow warning banner at top
   - **"restricted" combinations**: SVG with red restriction banner at top
   - **All combinations**: Include `layoutRestriction` field in JSON output

4. **JSON Output Structure**:
```json
{
  "results": [
    {
      "id": "0001",
      "sections": ["Ballroom_Aisle_12"],
      "layoutRestriction": "restricted",
      "passed": true,
      "layout": {
        "width": 58.0,
        "height": 10.0,
        "area": 580.0,
        "angle": 0.0
      },
      "polygonArea": 580.0,
      "fillRatio": 100.0,
      "runtimeMs": 125
    }
  ]
}
```

## Adding LayoutRestriction to Existing Files

Use the provided utility script to add the property to all sections:

```bash
node add-layout-restriction.js <path-to-Level1-data.json>
```

This will add `"LayoutRestriction": "allowed"` to all sections that don't already have the property.

After running the script, manually change values to "warning" or "restricted" as needed.

## Best Practices

1. **Default to "allowed"** for most sections
2. **Document restrictions** using `LayoutRestrictionReason` for clarity
3. **Test warning combinations** to ensure the warning is appropriate
4. **Review restrictions regularly** as venue configurations change
5. **Coordinate with venue staff** to identify problematic areas
6. **Consider user expectations** when applying warnings vs restrictions

---

**Last Updated:** 2025-10-30
