window.aegisLayout = (function () {
    "use strict";

    let active = false;

    function initInspectorResize(handleId) {
        const handle = document.getElementById(handleId);
        if (!handle || handle.dataset.bound === "1") {
            return;
        }

        handle.dataset.bound = "1";

        handle.addEventListener("mousedown", (event) => {
            event.preventDefault();
            active = true;
            document.body.classList.add("aegis-resizing-inspector");
        });

        window.addEventListener("mousemove", (event) => {
            if (!active) return;
            const min = 280;
            const max = Math.min(window.innerWidth * 0.65, 720);
            const width = Math.round(Math.min(max, Math.max(min, window.innerWidth - event.clientX)));
            document.documentElement.style.setProperty("--inspector-w", `${width}px`);
        });

        window.addEventListener("mouseup", () => {
            if (!active) return;
            active = false;
            document.body.classList.remove("aegis-resizing-inspector");
        });
    }

    function downloadTextFile(filename, text, mimeType) {
        const blob = new Blob([text], { type: mimeType || "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = filename;
        anchor.click();
        URL.revokeObjectURL(url);
    }

    return { initInspectorResize, downloadTextFile };
})();

window.aegisDownload = {
    json(filename, payload) {
        const text = typeof payload === "string" ? payload : JSON.stringify(payload, null, 2);
        window.aegisLayout.downloadTextFile(filename, text, "application/json");
    }
};
