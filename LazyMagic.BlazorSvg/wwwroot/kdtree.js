// KD-Tree implementation for 2D spatial queries
// Optimized for SVG path processing and spatial operations

class KDNode {
    constructor(point, left = null, right = null, axis = 0) {
        this.point = point;
        this.left = left;
        this.right = right;
        this.axis = axis; // 0 for x, 1 for y
    }
}

class KDTree {
    constructor(points = [], dimensions = 2) {
        this.dimensions = dimensions;
        this.root = null;

        if (points.length > 0) {
            this.root = this.build(points, 0);
        }
    }

    // Build the KD-tree from points
    build(points, depth = 0) {
        if (points.length === 0) return null;
        if (points.length === 1) return new KDNode(points[0], null, null, depth % this.dimensions);

        const axis = depth % this.dimensions;

        // Sort points by the current axis
        const sorted = [...points].sort((a, b) => {
            const aVal = axis === 0 ? a.x : a.y;
            const bVal = axis === 0 ? b.x : b.y;
            return aVal - bVal;
        });

        const medianIdx = Math.floor(sorted.length / 2);
        const median = sorted[medianIdx];

        const leftPoints = sorted.slice(0, medianIdx);
        const rightPoints = sorted.slice(medianIdx + 1);

        return new KDNode(
            median,
            this.build(leftPoints, depth + 1),
            this.build(rightPoints, depth + 1),
            axis
        );
    }

    // Insert a new point
    insert(point) {
        if (!this.root) {
            this.root = new KDNode(point, null, null, 0);
            return;
        }

        this._insertNode(this.root, point, 0);
    }

    _insertNode(node, point, depth) {
        const axis = depth % this.dimensions;
        const nodeVal = axis === 0 ? node.point.x : node.point.y;
        const pointVal = axis === 0 ? point.x : point.y;

        if (pointVal < nodeVal) {
            if (node.left === null) {
                node.left = new KDNode(point, null, null, (depth + 1) % this.dimensions);
            } else {
                this._insertNode(node.left, point, depth + 1);
            }
        } else {
            if (node.right === null) {
                node.right = new KDNode(point, null, null, (depth + 1) % this.dimensions);
            } else {
                this._insertNode(node.right, point, depth + 1);
            }
        }
    }

    // Find nearest neighbor
    nearest(target, maxDistance = Infinity) {
        if (!this.root) return null;

        let best = { point: null, distance: maxDistance };
        this._nearestSearch(this.root, target, best);

        return best.point;
    }

    _nearestSearch(node, target, best) {
        if (!node) return;

        const distance = this._distance(node.point, target);

        if (distance < best.distance) {
            best.point = node.point;
            best.distance = distance;
        }

        const axis = node.axis;
        const diff = axis === 0
            ? target.x - node.point.x
            : target.y - node.point.y;

        // Search the side of the tree that target is on
        const nearChild = diff < 0 ? node.left : node.right;
        const farChild = diff < 0 ? node.right : node.left;

        this._nearestSearch(nearChild, target, best);

        // Check if we need to search the other side
        if (Math.abs(diff) < best.distance) {
            this._nearestSearch(farChild, target, best);
        }
    }

    // Find k nearest neighbors
    kNearest(target, k, maxDistance = Infinity) {
        if (!this.root || k <= 0) return [];

        const neighbors = [];
        this._kNearestSearch(this.root, target, k, neighbors, maxDistance);

        return neighbors
            .sort((a, b) => a.distance - b.distance)
            .slice(0, k)
            .map(n => n.point);
    }

    _kNearestSearch(node, target, k, neighbors, maxDistance) {
        if (!node) return;

        const distance = this._distance(node.point, target);

        if (distance < maxDistance) {
            neighbors.push({ point: node.point, distance });
            neighbors.sort((a, b) => a.distance - b.distance);

            if (neighbors.length > k) {
                neighbors.pop();
            }
        }

        const axis = node.axis;
        const diff = axis === 0
            ? target.x - node.point.x
            : target.y - node.point.y;

        const nearChild = diff < 0 ? node.left : node.right;
        const farChild = diff < 0 ? node.right : node.left;

        this._kNearestSearch(nearChild, target, k, neighbors, maxDistance);

        // Check if we need to search the other side
        const worstDistance = neighbors.length < k
            ? maxDistance
            : neighbors[neighbors.length - 1].distance;

        if (Math.abs(diff) < worstDistance) {
            this._kNearestSearch(farChild, target, k, neighbors, maxDistance);
        }
    }

    // Range search - find all points within a rectangle
    rangeSearch(minX, minY, maxX, maxY) {
        const results = [];
        this._rangeSearchNode(this.root, minX, minY, maxX, maxY, results);
        return results;
    }

    _rangeSearchNode(node, minX, minY, maxX, maxY, results) {
        if (!node) return;

        const point = node.point;

        // Check if point is in range
        if (point.x >= minX && point.x <= maxX &&
            point.y >= minY && point.y <= maxY) {
            results.push(point);
        }

        // Check which subtrees to search
        const axis = node.axis;
        const splitVal = axis === 0 ? point.x : point.y;
        const rangeMin = axis === 0 ? minX : minY;
        const rangeMax = axis === 0 ? maxX : maxY;

        if (rangeMin <= splitVal) {
            this._rangeSearchNode(node.left, minX, minY, maxX, maxY, results);
        }

        if (rangeMax >= splitVal) {
            this._rangeSearchNode(node.right, minX, minY, maxX, maxY, results);
        }
    }

    // Find all points within radius
    radiusSearch(center, radius) {
        const results = [];
        const radiusSq = radius * radius;
        this._radiusSearchNode(this.root, center, radiusSq, results);
        return results;
    }

    _radiusSearchNode(node, center, radiusSq, results) {
        if (!node) return;

        const distSq = this._distanceSquared(node.point, center);

        if (distSq <= radiusSq) {
            results.push({ point: node.point, distance: Math.sqrt(distSq) });
        }

        // Check which subtrees to search
        const axis = node.axis;
        const diff = axis === 0
            ? center.x - node.point.x
            : center.y - node.point.y;

        const nearChild = diff < 0 ? node.left : node.right;
        const farChild = diff < 0 ? node.right : node.left;

        this._radiusSearchNode(nearChild, center, radiusSq, results);

        // Check if we need to search the other side
        if (diff * diff <= radiusSq) {
            this._radiusSearchNode(farChild, center, radiusSq, results);
        }
    }

    // Helper functions
    _distance(a, b) {
        return Math.sqrt(this._distanceSquared(a, b));
    }

    _distanceSquared(a, b) {
        const dx = a.x - b.x;
        const dy = a.y - b.y;
        return dx * dx + dy * dy;
    }

    // Get tree depth
    depth() {
        return this._getDepth(this.root);
    }

    _getDepth(node) {
        if (!node) return 0;
        return 1 + Math.max(this._getDepth(node.left), this._getDepth(node.right));
    }

    // Get number of nodes
    size() {
        return this._countNodes(this.root);
    }

    _countNodes(node) {
        if (!node) return 0;
        return 1 + this._countNodes(node.left) + this._countNodes(node.right);
    }
}

// Spatial grid for accelerated point-in-polygon tests
class SpatialGrid {
    constructor(polygon, cellSize = 10) {
        this.polygon = polygon;
        this.cellSize = cellSize;

        // Calculate bounding box
        this.bounds = this._calculateBounds(polygon);

        // Calculate grid dimensions
        this.cols = Math.ceil((this.bounds.maxX - this.bounds.minX) / cellSize);
        this.rows = Math.ceil((this.bounds.maxY - this.bounds.minY) / cellSize);

        // Initialize grid
        this.grid = new Array(this.rows);
        for (let i = 0; i < this.rows; i++) {
            this.grid[i] = new Array(this.cols);
        }

        // Precompute cell classifications
        this._classifyCells();
    }

    _calculateBounds(polygon) {
        let minX = Infinity, minY = Infinity;
        let maxX = -Infinity, maxY = -Infinity;

        for (const point of polygon) {
            minX = Math.min(minX, point.x);
            minY = Math.min(minY, point.y);
            maxX = Math.max(maxX, point.x);
            maxY = Math.max(maxY, point.y);
        }

        return { minX, minY, maxX, maxY };
    }

    _classifyCells() {
        for (let row = 0; row < this.rows; row++) {
            for (let col = 0; col < this.cols; col++) {
                const cellBounds = this._getCellBounds(row, col);
                this.grid[row][col] = this._classifyCell(cellBounds);
            }
        }
    }

    _getCellBounds(row, col) {
        const minX = this.bounds.minX + col * this.cellSize;
        const minY = this.bounds.minY + row * this.cellSize;
        const maxX = Math.min(minX + this.cellSize, this.bounds.maxX);
        const maxY = Math.min(minY + this.cellSize, this.bounds.maxY);

        return { minX, minY, maxX, maxY };
    }

    _classifyCell(cellBounds) {
        // Check all four corners of the cell
        const corners = [
            { x: cellBounds.minX, y: cellBounds.minY },
            { x: cellBounds.maxX, y: cellBounds.minY },
            { x: cellBounds.maxX, y: cellBounds.maxY },
            { x: cellBounds.minX, y: cellBounds.maxY }
        ];

        let insideCount = 0;
        for (const corner of corners) {
            if (this._slowPointInPolygon(corner)) {
                insideCount++;
            }
        }

        if (insideCount === 4) {
            return 'inside';  // Fully inside
        } else if (insideCount === 0) {
            return 'outside'; // Fully outside
        } else {
            return 'boundary'; // Partially inside
        }
    }

    _slowPointInPolygon(point) {
        // Ray casting algorithm
        let inside = false;
        const { x, y } = point;
        const polygon = this.polygon;

        for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
            const xi = polygon[i].x, yi = polygon[i].y;
            const xj = polygon[j].x, yj = polygon[j].y;

            const intersect = ((yi > y) !== (yj > y))
                && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);

            if (intersect) inside = !inside;
        }

        return inside;
    }

    // Fast point-in-polygon test using grid
    containsPoint(point) {
        // Quick bounds check
        if (point.x < this.bounds.minX || point.x > this.bounds.maxX ||
            point.y < this.bounds.minY || point.y > this.bounds.maxY) {
            return false;
        }

        // Get cell coordinates
        const col = Math.floor((point.x - this.bounds.minX) / this.cellSize);
        const row = Math.floor((point.y - this.bounds.minY) / this.cellSize);

        // Check if cell indices are valid
        if (row < 0 || row >= this.rows || col < 0 || col >= this.cols) {
            return false;
        }

        const cellClass = this.grid[row][col];

        if (cellClass === 'inside') {
            return true;
        } else if (cellClass === 'outside') {
            return false;
        } else {
            // Boundary cell - need exact test
            return this._slowPointInPolygon(point);
        }
    }

    // Test if a rectangle is fully contained
    containsRectangle(minX, minY, maxX, maxY) {
        // Quick bounds check
        if (minX < this.bounds.minX || maxX > this.bounds.maxX ||
            minY < this.bounds.minY || maxY > this.bounds.maxY) {
            return false;
        }

        // Get cell range
        const colMin = Math.floor((minX - this.bounds.minX) / this.cellSize);
        const colMax = Math.floor((maxX - this.bounds.minX) / this.cellSize);
        const rowMin = Math.floor((minY - this.bounds.minY) / this.cellSize);
        const rowMax = Math.floor((maxY - this.bounds.minY) / this.cellSize);

        // Check all cells in range
        for (let row = rowMin; row <= rowMax; row++) {
            for (let col = colMin; col <= colMax; col++) {
                if (row < 0 || row >= this.rows || col < 0 || col >= this.cols) {
                    return false;
                }

                const cellClass = this.grid[row][col];
                if (cellClass === 'outside') {
                    return false;
                } else if (cellClass === 'boundary') {
                    // Need to check actual rectangle corners and edges
                    return this._slowRectangleInPolygon(minX, minY, maxX, maxY);
                }
            }
        }

        return true; // All cells are 'inside'
    }

    _slowRectangleInPolygon(minX, minY, maxX, maxY) {
        // Check corners
        const corners = [
            { x: minX, y: minY },
            { x: maxX, y: minY },
            { x: maxX, y: maxY },
            { x: minX, y: maxY }
        ];

        for (const corner of corners) {
            if (!this._slowPointInPolygon(corner)) {
                return false;
            }
        }

        // Check some edge points
        const edgeTests = 3;
        for (let i = 1; i <= edgeTests; i++) {
            const t = i / (edgeTests + 1);

            // Top and bottom edges
            if (!this._slowPointInPolygon({ x: minX + t * (maxX - minX), y: minY })) return false;
            if (!this._slowPointInPolygon({ x: minX + t * (maxX - minX), y: maxY })) return false;

            // Left and right edges
            if (!this._slowPointInPolygon({ x: minX, y: minY + t * (maxY - minY) })) return false;
            if (!this._slowPointInPolygon({ x: maxX, y: minY + t * (maxY - minY) })) return false;
        }

        return true;
    }
}

// Export for use in SvgViewer
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { KDTree, SpatialGrid };
}