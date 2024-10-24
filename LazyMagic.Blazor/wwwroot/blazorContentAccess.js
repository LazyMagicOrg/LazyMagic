// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export async function getBlazorContent(path) {
    try {
        const response = await fetch(path);
        if (!response.ok) {
            throw new Error('Network response was not ok.');
        }
        return await response.text();
    } catch (error) {
        console.error('There has been a problem with your fetch operation:', error);
        throw error; // Rethrowing the error is important if you want to handle it on the .NET side.
    }
}
