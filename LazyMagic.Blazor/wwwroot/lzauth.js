// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function showPrompt(message) {
  return prompt(message, 'Type anything here');
}
export function focusElement(id) {
    if(!id) return;
    const element = document.getElementById(id)
    if (element)
        element.focus();
}
export function activeElement() {
    var activeElement = document.activeElement;
    return activeElement ? activeElement.id : null;
}

export function isActiveElementTabStop() {

    const element = document.activeElement;
    if (!element) return false;

    const focusableTagNames = ['A', 'INPUT', 'SELECT', 'TEXTAREA', 'BUTTON'];
    const isFocusableTag = focusableTagNames.includes(element.tagName);

    const hasPositiveTabindex = element.hasAttribute('tabindex') && element.getAttribute('tabindex') >= 0;

    const isContentEditable = element.hasAttribute('contenteditable') && element.getAttribute('contenteditable') === 'true';

    return isFocusableTag || hasPositiveTabindex || isContentEditable;
}