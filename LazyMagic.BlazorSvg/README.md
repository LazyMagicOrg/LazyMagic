# LazyMagic.BlazorSvg

A powerful Blazor component library for interactive SVG rendering with advanced geometric layout capabilities. Built on top of Snap.svg, this component provides path selection, visualization, and sophisticated rectangle inscription algorithms for floor plan and space planning applications.

## Features

### Interactive SVG Rendering
- **Pan and Zoom**: Built-in pan and zoom functionality for navigating large SVG documents
- **Path Selection**: Click to select/deselect individual SVG paths with visual feedback
- **Multi-Path Selection**: Select multiple paths simultaneously with automatic boundary visualization
- **Selection Events**: Real-time event callbacks for path selection changes

### Geometric Layout Algorithms
- **Max Inscribed Rectangle**: Computes the largest axis-aligned or rotated rectangle that fits within irregular polygons
- **Boardroom Layout**: Automatic furniture placement for boardroom seating configurations
- **Hollow Square Layout**: Conference table layout in a hollow square arrangement
- **Precomputed Data**: Build-time computation of layouts embedded directly in SVG for instant runtime access

### Metadata Integration
- **Floor Level Data**: Extract complete floor metadata including rooms, sections, and measurements
- **Polygon Areas**: Automatic calculation of polygon areas for selected paths
- **Layout Measurements**: Real-time area calculations for all layout types
- **Custom Attributes**: Support for custom SVG attributes and metadata

### Visual Features
- **Bounding Box Visualization**: Automatic display of bounding boxes around selected paths
- **Unified Path Outline**: Creates merged outlines for multi-path selections
- **Layout Overlays**: Visual overlays for inscribed rectangles and furniture layouts
- **Transform Support**: Proper handling of SVG transforms including scaling and rotation

## Installation

```bash
dotnet add package LazyMagic.BlazorSvg
```

Add the required script reference to your `index.html` or `_Host.cshtml`:

```html
<script src="_content/LazyMagic.BlazorSvg/SvgViewer.js"></script>
```

## Basic Usage

### Simple SVG Display

```razor
@using LazyMagic.BlazorSvg
@using LazyMagic.BlazorSvg.Models

<SvgViewer SvgUrl="myfloorplan.svg" />
```

### Interactive Selection with Events

```razor
<SvgViewer @ref="svgViewer"
    SvgUrl="floor.svg"
    InitialPaths="@initiallySelectedPaths"
    PathsChanged="OnPathsChanged"
    @bind-AllInsideSelected="@allInsideSelected" />

@code {
    private SvgViewer? svgViewer;
    private List<string> initiallySelectedPaths = new() { "Room_1", "Room_2" };
    private bool allInsideSelected = false;

    private void OnPathsChanged(List<string> selectedPaths)
    {
        Console.WriteLine($"Selected {selectedPaths.Count} paths");
        // Handle selection changes
    }
}
```

### Rectangle Layout Display

```razor
<SvgViewer @ref="svgViewer"
    SvgUrl="floor.svg"
    InitialPaths="@selectedRooms"
    RectangleType="@layoutType"
    PathsChanged="OnSelectionChanged" />

<button @onclick='() => SetLayout("maxinscribed")'>Show Max Rectangle</button>
<button @onclick='() => SetLayout("boardroom")'>Show Boardroom</button>
<button @onclick='() => SetLayout("hollowsquare")'>Show Hollow Square</button>

@code {
    private string layoutType = "none";
    private List<string> selectedRooms = new() { "Conference_A" };

    private async Task SetLayout(string type)
    {
        layoutType = type;
        if (svgViewer != null)
        {
            await svgViewer.SetRectangleTypeAsync(type);
        }
    }

    private async void OnSelectionChanged(List<string> paths)
    {
        // Refresh area data when selection changes
        var areaData = await svgViewer!.GetAreaDataAsync();
        if (areaData != null)
        {
            Console.WriteLine($"Polygon Area: {areaData.PolygonArea:F2} sq units");
            Console.WriteLine($"Rectangle Area: {areaData.RectangleArea:F2} sq units");
        }
    }
}
```

### Accessing Area Data

```razor
<button @onclick="ShowAreaData">Get Area Data</button>

<div>
    @if (areaData != null)
    {
        <p>Polygon Area: @areaData.PolygonArea?.ToString("F2") sq in</p>
        @if (areaData.RectangleArea != null)
        {
            <p>Max Inscribed Rectangle: @areaData.RectangleArea?.ToString("F2") sq in</p>
        }
        @if (areaData.BoardroomArea != null)
        {
            <p>Boardroom Layout: @areaData.BoardroomArea?.ToString("F2") sq in</p>
            <p>Tables: @areaData.BoardroomTables, Sets: @areaData.BoardroomSets</p>
        }
    }
}

@code {
    private AreaData? areaData;

    private async Task ShowAreaData()
    {
        areaData = await svgViewer!.GetAreaDataAsync();
        StateHasChanged();
    }
}
```

### Floor Metadata Extraction

```razor
<button @onclick="LoadFloorData">Load Floor Metadata</button>

@if (floorLevel != null)
{
    <h3>@floorLevel.Name</h3>
    <p>Measurement Units: @floorLevel.Diagram?.MeasureUnits</p>

    @foreach (var room in floorLevel.Rooms)
    {
        <div>
            <h4>@room.Id</h4>
            @foreach (var section in room.RoomSections)
            {
                <p>@section.Name - @section.FloormatArea sq @floorLevel.Diagram?.Unit</p>
            }
        </div>
    }
}

@code {
    private FloorLevel? floorLevel;

    private async Task LoadFloorData()
    {
        floorLevel = await svgViewer!.GetFloorMetadataAsync();
        StateHasChanged();
    }
}
```

## Component Parameters

### Input Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `SvgUrl` | string | required | URL to the SVG file to display |
| `InitialPaths` | List\<string\> | empty list | List of path IDs to select on initialization |
| `Style` | string | "width: 100%; height: 100%;" | CSS style for the container div |
| `DisableSelection` | bool | false | Disables interactive path selection when true |
| `AllInsideSelected` | bool | false | Two-way binding indicating if all paths in bounding box are selected |
| `RectangleType` | string | "none" | Layout type: "none", "maxinscribed", "boardroom", or "hollowsquare" |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `PathSelected` | EventCallback\<string\> | Fired when a path is selected |
| `PathUnselected` | EventCallback\<string\> | Fired when a path is unselected |
| `PathsChanged` | EventCallback\<List\<string\>\> | Fired when the selection changes, provides all selected paths |
| `AllInsideSelectedChanged` | EventCallback\<bool\> | Fired when the "all inside" state changes |

## Public Methods

### Selection Methods

```csharp
// Select a single path by ID
await svgViewer.SelectPath("Room_A");

// Unselect a single path
await svgViewer.UnselectPath("Room_B");

// Clear all selections
await svgViewer.UnselectAllPaths();

// Select multiple paths (replaces current selection)
await svgViewer.SelectPaths(new List<string> { "Room_1", "Room_2", "Room_3" });
```

### Layout Methods

```csharp
// Set the rectangle layout type
await svgViewer.SetRectangleTypeAsync("maxinscribed");
// Options: "none", "maxinscribed", "boardroom", "hollowsquare"
```

### Data Access Methods

```csharp
// Get area data for current selection
AreaData? areaData = await svgViewer.GetAreaDataAsync();

// Extract floor metadata from SVG
FloorLevel? floorData = await svgViewer.GetFloorMetadataAsync();
```

## Data Models

### AreaData

Contains area measurements for the currently selected paths:

```csharp
public class AreaData
{
    public double? PolygonArea { get; set; }          // Area of combined polygon
    public double? RectangleArea { get; set; }        // Max inscribed rectangle area
    public double? BoardroomArea { get; set; }        // Boardroom layout area
    public int? BoardroomSets { get; set; }           // Number of boardroom table sets
    public int? BoardroomTables { get; set; }         // Number of boardroom tables
    public double? HollowSquareArea { get; set; }     // Hollow square layout area
    public double? ComputationTimeMs { get; set; }    // Computation time in milliseconds
}
```

### FloorLevel

Represents the complete floor metadata:

```csharp
public class FloorLevel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DiagramInfo? Diagram { get; set; }
    public List<Room> Rooms { get; set; }
}

public class Room
{
    public string Id { get; set; }
    public List<RoomSection> RoomSections { get; set; }
    public List<SectionJoin> Joins { get; set; }
}

public class RoomSection
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public double? FloormatArea { get; set; }
    public double? PolygonArea { get; set; }
    public double? Width { get; set; }
    public double? Depth { get; set; }
    public string? SectionType { get; set; }
    public string? LayoutRestriction { get; set; }
}
```

## Advanced Features

### SVG Transform Support

The component correctly handles SVG transforms including:
- Parent group transforms
- Path-level transforms
- Nested transform hierarchies
- Scale, rotation, and translation

All layout visualizations (rectangles, boardroom, hollow square) automatically inherit and apply the correct transforms from their source paths.

### Precomputed Layout Data

For optimal performance, layout data can be precomputed at build time and embedded in the SVG:

1. Use the FloorMat build pipeline to compute layouts
2. Data is embedded in SVG as custom attributes
3. Runtime lookup is instant (no computation required)
4. Falls back to runtime computation if data not found

### Layout Types Explained

**Max Inscribed Rectangle** (`maxinscribed`)
- Computes the largest rectangle that fits entirely within a polygon
- Supports both axis-aligned and rotated rectangles
- Uses binary search expansion with multiple centroid sampling
- Optimal for maximizing usable floor space

**Boardroom Layout** (`boardroom`)
- Arranges conference tables in a boardroom configuration
- Automatically places tables and chairs
- Validates furniture placement within boundaries
- Returns table count and set count

**Hollow Square Layout** (`hollowsquare`)
- Arranges tables in a hollow square formation
- Suitable for collaborative meeting setups
- Maximizes participant face-to-face interaction
- Validates placement within room boundaries

## Complete Example

See the full example in the BlazorTest.WASM project at:
- Component: `BlazorUI/Pages/SVGTestPage.razor`
- Demonstrates all features including selection, layouts, and metadata extraction

## Dependencies

- Snap.svg (included)
- .NET 9.0 or higher
- Blazor WebAssembly or Blazor Server

## Related Documentation

- [Max Inscribed Rectangle Algorithm Guide](../LazyMagic.FloorMat/Documentation/InscribedRectangle-Guide.md)
- [Algorithm Implementation Details](../LazyMagic.FloorMat/Documentation/Algorithms-Implementation.md)
- [FloorMat Build Pipeline](../LazyMagic.FloorMat/Documentation/BoardroomLayoutPipeline.md)

## License

Part of the LazyMagic framework. See LICENSE file for details.
