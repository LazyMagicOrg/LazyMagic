// This file should be placed at: BlazorUI/wwwroot/js/blazorLoading.js

// Isolated functions for the BlazorLoading component
export function setBlazorLoadingPercentage(percentage) {
    const circle = document.querySelector('.lz-blazor-loading-progress circle:last-child');
    if (!circle) return;

    // Ensure the percentage is between 0 and 100
    percentage = Math.max(0, Math.min(100, percentage));

    // Get the circle's radius and calculate the circumference
    const radius = 45; // matches the r attribute in the SVG
    const circumference = radius * 2 * Math.PI;
    
    // Match Blazor's approach: first value is the arc length, second is the gap
    const arcLength = (percentage / 100) * circumference;
    circle.style.strokeDasharray = `${arcLength} ${circumference}`;
    circle.style.strokeDashoffset = '0';
}

export function animateLoadingToCompletion() {
    setBlazorLoadingPercentage(100);
}