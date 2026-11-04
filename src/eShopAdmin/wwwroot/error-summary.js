// Moves focus to the error summary of the draft form after a failed save, whether the page was
// replaced by a normal form post or patched in by an enhanced one.
(function () {
    function focusErrorSummary() {
        var summary = document.querySelector('[data-focus-on-load]');
        if (summary) {
            summary.focus();
        }
    }

    focusErrorSummary();

    if (window.Blazor && window.Blazor.addEventListener) {
        window.Blazor.addEventListener('enhancedload', focusErrorSummary);
    }
})();
