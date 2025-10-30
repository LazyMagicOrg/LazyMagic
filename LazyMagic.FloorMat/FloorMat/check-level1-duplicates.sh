#!/bin/bash
cd "C:\Users\noaht\source\repos\_Dev\BCProjects\BCTenancies\bcs-cerulean\FloorMat\Level1\output\Level1-output\ComputedLayouts\Level1-MaxInscribedResults"

for file in MaxInscribed_Ballroom_0020.svg MaxInscribed_Ballroom_0030.svg MaxInscribed_Ballroom_0040.svg MaxInscribed_Ballroom_0048.svg; do
    echo "=== $file ==="
    points=$(grep "polygon points=" "$file" | sed 's/.*points="//' | sed 's/".*//')
    node "C:\Users\noaht\source\repos\_Dev\LazyMagic\LazyMagic\LazyMagic.FloorMat\FloorMat\check-duplicates.js" "$points"
    echo ""
done
