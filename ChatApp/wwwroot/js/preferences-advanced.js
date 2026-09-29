"use strict";

(function () {
    const STORAGE_KEY = "chatapp.appearance";

    function token() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    }

    function resolveTheme(theme) {
        if (theme === "dark" || theme === "light") {
            return theme;
        }

        return window.matchMedia?.("(prefers-color-scheme: dark)").matches
            ? "dark"
            : "light";
    }

    function apply(preferences = {}) {
        const themePreference = preferences.theme || "system";
        const resolvedTheme = resolveTheme(themePreference);
        const root = document.documentElement;

        root.dataset.themePreference = themePreference;
        root.dataset.theme = resolvedTheme;
        root.dataset.density = preferences.uiDensity || "comfortable";
        root.classList.toggle("reduce-motion", preferences.reduceMotion === true);
        root.style.setProperty("--chatapp-accent", preferences.accentColor || "#2563eb");

        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences));
        } catch (_) {}
    }

    async function load() {
        try {
            const response = await fetch("/api/preferences", {
                method: "GET",
                headers: { Accept: "application/json" },
                credentials: "same-origin",
                cache: "no-store",
            });

            if (!response.ok) return;

            const preferences = await response.json();
            apply(preferences);
            window.dispatchEvent(
                new CustomEvent("chatapp:preferences-advanced", {
                    detail: preferences,
                }),
            );
        } catch (error) {
            console.warn("Preferências avançadas:", error);
        }
    }

    async function save(patch) {
        const headers = {
            "Content-Type": "application/json",
            Accept: "application/json",
        };

        const csrf = token();
        if (csrf) headers["RequestVerificationToken"] = csrf;

        const response = await fetch("/api/preferences", {
            method: "PUT",
            headers,
            credentials: "same-origin",
            body: JSON.stringify(patch),
        });

        let data = null;
        try { data = await response.json(); } catch (_) {}

        if (!response.ok) {
            throw new Error(data?.message || "Não foi possível guardar a preferência.");
        }

        await load();
        return data;
    }

    window.ChatAppAdvancedPreferences = Object.freeze({
        load,
        apply,
        save,
    });

    try {
        const cached = JSON.parse(localStorage.getItem(STORAGE_KEY) || "null");
        if (cached) apply(cached);
    } catch (_) {}

    const media = window.matchMedia?.("(prefers-color-scheme: dark)");
    media?.addEventListener?.("change", () => {
        try {
            const cached = JSON.parse(localStorage.getItem(STORAGE_KEY) || "null");
            if (cached?.theme === "system") apply(cached);
        } catch (_) {}
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", load, { once: true });
    } else {
        load();
    }
})();
