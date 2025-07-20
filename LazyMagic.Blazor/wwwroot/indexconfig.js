/// These functions are used to dynamically add stylesheets and scripts to the HTML document.
/// This is particularly useful for loading styles and scripts for component libraries
//  when we don't want to include them in the index.html file directly.

function addLink(attributes) {
    // Create a new link element
    const link = document.createElement('link');

    // Add each attribute to the link element
    for (const [key, value] of Object.entries(attributes)) {
        link.setAttribute(key, value);
    }

    // Append to document head
    document.head.appendChild(link);

    return link;
}

// Function to dynamically load a script
// Use this to load scripts for component libraries that would usually have you 
// place the script in the index.html file.
function addHeadScript(url) {
    var script = document.createElement('script');
    script.src = url;
    document.head.appendChild(script);
}


// Function to dynamically load a script
// Use this to load scripts for component libraries that would usually have you 
// place the script in the index.html file.
function addBodyScript(url) {
    var script = document.createElement('script');
    script.src = url;
    document.body.appendChild(script);
}

// We need to wait for the DOM to be fully loaded before we can
// show the loading spinner. This is because the spinner depends
// on css that needs to fully load.
// Wait for DOM to fully load and show the main app
document.addEventListener("DOMContentLoaded", function () {
    const main = document.getElementById('main');
    if (main) {
        main.style.visibility = 'visible';
        console.log("Main app container is now visible.");
    }
});
