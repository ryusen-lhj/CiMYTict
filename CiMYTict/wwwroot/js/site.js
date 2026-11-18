// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


// CiMYTict appearance preference: dark, light, or follow the device.
(function () {
    const storageKey = "cimytict-theme";
    const media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

    function getPreference() {
        try { return localStorage.getItem(storageKey) || "device"; }
        catch { return "device"; }
    }

    function applyTheme(preference) {
        const resolved = preference === "device"
            ? (media && media.matches ? "dark" : "light")
            : preference;
        document.documentElement.setAttribute("data-theme", resolved);
        document.documentElement.setAttribute("data-theme-preference", preference);
        const selector = document.getElementById("themePreference");
        if (selector && selector.value !== preference) selector.value = preference;
    }

    document.addEventListener("DOMContentLoaded", function () {
        const selector = document.getElementById("themePreference");
        const preference = getPreference();
        applyTheme(preference);
        if (selector) {
            selector.value = preference;
            selector.addEventListener("change", function () {
                const selected = ["dark", "light", "device"].includes(selector.value) ? selector.value : "device";
                try { localStorage.setItem(storageKey, selected); } catch { /* Keep the current-page choice. */ }
                applyTheme(selected);
            });
        }
    });

    if (media) {
        const onDeviceThemeChange = function () {
            if (getPreference() === "device") applyTheme("device");
        };
        if (media.addEventListener) media.addEventListener("change", onDeviceThemeChange);
        else if (media.addListener) media.addListener(onDeviceThemeChange);
    }
})();
