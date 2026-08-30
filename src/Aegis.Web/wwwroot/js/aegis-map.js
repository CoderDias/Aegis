window.aegisMap = (function () {
    "use strict";

    const instances = {};

    const svgWrap = (paths, color, size, rotation = 0) =>
        `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 24 24" ` +
        `style="display:block" aria-hidden="true">` +
        `<g transform="rotate(${rotation} 12 12)">${paths(color)}</g></svg>`;

    const svgIcons = {
        plane: color =>
            `<path fill="${color}" d="M12 2.2 9.1 11.2 3.2 13.1 3.2 14.8 9.1 14.1 9.1 18.6 7.1 20.8 16.9 20.8 14.9 18.6 14.9 14.1 20.8 14.8 20.8 13.1 14.9 11.2 12 2.2z"/>`,
        lightbulb: color =>
            `<path fill="${color}" d="M12 2C8.13 2 5 5.13 5 9c0 2.38 1.19 4.47 3 5.74V17c0 .55.45 1 1 1h6c.55 0 1-.45 1-1v-2.26c1.81-1.27 3-3.36 3-5.74 0-3.86-3.14-7-7-7zm-1 17v1h2v-1h-2z"/>`,
        desktop: color =>
            `<path fill="${color}" d="M21 2H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h7v2H8v2h8v-2h-2v-2h7c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H3V4h18v12z"/>`,
        exclamation: color =>
            `<path fill="${color}" d="M11 4h2v10h-2V4zm0 12h2v2h-2v-2z"/>`,
        planeDeparture: color =>
            `<circle fill="${color}" cx="12" cy="12" r="5"/><path fill="#0b0e11" stroke="#0b0e11" stroke-width="1.5" d="M12 8v8M8 12h8"/>`,
        planeArrival: color =>
            `<circle fill="${color}" cx="12" cy="12" r="5"/><path fill="#0b0e11" stroke="#0b0e11" stroke-width="1.5" d="M8 12h8"/>`,
        "skull-crossbones": color =>
            `<path fill="${color}" d="M12 2C8.13 2 5 5.13 5 9c0 2.3 1.1 4.34 2.8 5.65L7 16h2l-.5-1h7l-.5 1h2l-.8-1.35C18.9 13.34 20 11.3 20 9c0-3.87-3.13-7-8-7zm-2 8a1 1 0 110-2 1 1 0 010 2zm4 0a1 1 0 110-2 1 1 0 010 2zM8.5 18l-2 3h3l2.5-2 2.5 2h3l-2-3h-7z"/>`,
        ship: color =>
            `<path fill="${color}" d="M4 18c1.5 1 3.5 1 5 0s3.5-1 5 0 3.5 1 5 0V6l-5-2-5 2-5-2-5 2v12z"/>`,
        tower: color =>
            `<path fill="${color}" d="M12 2 8 10h2v10h4V10h2L12 2zm-1 18h2v2h-2v-2z"/>`,
        repeater: color =>
            `<path fill="${color}" d="M12 3C7.03 3 3 7.03 3 12h2a7 7 0 0114 0h2c0-4.97-4.03-9-9-9zm0 4a5 5 0 00-5 5h2a3 3 0 016 0h2a5 5 0 00-5-5zm0 4a1 1 0 100 2 1 1 0 000-2z"/>`,
        erb: color =>
            `<rect fill="${color}" x="9" y="3" width="6" height="16" rx="1"/><path fill="#fff" d="M6 7h12M6 17h12" opacity="0.8"/>`,
        camera: color =>
            `<rect fill="${color}" x="3" y="7" width="14" height="10" rx="2"/><path fill="${color}" d="M17 10l4-2v8l-4-2z"/>`,
        port: color =>
            `<path fill="none" stroke="${color}" stroke-width="2" d="M4 18h16M6 14l3-8h6l3 8"/><circle fill="${color}" cx="12" cy="18" r="2"/>`,
        alert: color =>
            `<path fill="${color}" d="M12 3l10 18H2z"/><path fill="#111" d="M11 9h2v6h-2zm0 8h2v2h-2z"/>`,
        seismic: color =>
            `<circle fill="${color}" cx="12" cy="12" r="4"/><circle fill="none" stroke="${color}" stroke-width="2" cx="12" cy="12" r="8" opacity="0.7"/>`
    };

    const mapMarkerIcon = (iconName, color, size, extraClass, rotation = 0) => {
        const draw = svgIcons[iconName];
        const html = draw ? svgWrap(draw, color, size, rotation) : "";
        const dimension = size + 4;
        return L.divIcon({
            className: ["leaflet-div-icon", "aegis-map-icon", extraClass].filter(Boolean).join(" "),
            html,
            iconSize: [dimension, dimension],
            iconAnchor: [dimension / 2, dimension / 2]
        });
    };

    function parseSeismicMagnitude(title) {
        if (!title) return 3;
        const match = String(title).match(/M(\d+(?:\.\d+)?)/i);
        return match ? Math.max(2, Math.min(10, parseFloat(match[1]))) : 3;
    }

    function seismicPulseIcon(title, opacity) {
        const mag = parseSeismicMagnitude(title);
        const size = Math.round(24 + mag * 4);
        const op = opacity == null ? 1 : Math.max(0, Math.min(1, Number(opacity)));
        return L.divIcon({
            className: "aegis-seismic-marker-wrap",
            html:
                `<div class="aegis-seismic-marker" style="width:${size}px;height:${size}px;--seismic-opacity:${op}">` +
                `<span class="aegis-seismic-marker__ring"></span>` +
                `<span class="aegis-seismic-marker__ring aegis-seismic-marker__ring--delay"></span>` +
                `<span class="aegis-seismic-marker__core"></span></div>`,
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2]
        });
    }

    function inmetPulseIcon(opacity) {
        const size = 32;
        const op = opacity == null ? 1 : Math.max(0, Math.min(1, Number(opacity)));
        return L.divIcon({
            className: "aegis-inmet-marker-wrap",
            html:
                `<div class="aegis-inmet-marker" style="width:${size}px;height:${size}px;--inmet-opacity:${op}">` +
                `<span class="aegis-inmet-marker__ring"></span>` +
                `<span class="aegis-inmet-marker__ring aegis-inmet-marker__ring--delay"></span>` +
                `<span class="aegis-inmet-marker__core"></span></div>`,
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2]
        });
    }

    function aircraftCategoryColor(category) {
        switch ((category || "").toLowerCase()) {
            case "commercial":
                return "#818cf8";
            case "military":
                return "#3fb950";
            case "private":
            default:
                return "#f472b6";
        }
    }

    function aircraftCategoryLabel(category) {
        switch ((category || "").toLowerCase()) {
            case "commercial":
                return "Comercial";
            case "military":
                return "Militar";
            case "private":
            default:
                return "Particular";
        }
    }

    function createAircraftIcon(heading, selected, pulse, category) {
        const color = selected ? "#ffffff" : aircraftCategoryColor(category);
        const rotation = heading != null ? heading : 0;
        const cls = ["aegis-aircraft-icon"];
        if (selected) cls.push("aegis-aircraft-icon--selected");
        if (pulse) cls.push("aegis-aircraft-icon--pulse");
        if (category) cls.push(`aegis-aircraft-icon--${category.toLowerCase()}`);
        return mapMarkerIcon("plane", color, 18, cls.join(" "), rotation);
    }

    function getBounds(map) {
        const b = map.getBounds();
        return {
            south: b.getSouth(),
            west: b.getWest(),
            north: b.getNorth(),
            east: b.getEast(),
            zoom: map.getZoom()
        };
    }

    function currentMapZoom(inst) {
        if (!inst) return 99;
        if (inst.map && typeof inst.map.getZoom === "function") {
            return inst.map.getZoom();
        }
        return inst.mapZoom ?? 99;
    }

    function isFeatureVisibleAtZoom(kind, zoom) {
        if (kind === "radio_tower" || kind === "repeater") return zoom >= 7;
        if (kind === "public_camera") return zoom >= 9;
        if (kind === "erb" || kind === "port" || kind === "poi" || kind === "building" || kind === "road") return zoom >= 8;
        return zoom >= 8;
    }

    function isGeoLayerVisibleAtZoom(layerKey, zoom) {
        if (layerKey === "alerts" || layerKey === "ships") return zoom >= 7;
        return true;
    }

    function notifyViewport(instance) {
        if (!instance.dotNetRef) return;
        if (instance.viewportTimer) {
            clearTimeout(instance.viewportTimer);
        }
        instance.viewportTimer = setTimeout(() => {
            const bounds = getBounds(instance.map);
            instance.mapZoom = bounds.zoom;
            instance.dotNetRef.invokeMethodAsync("OnViewportChanged", bounds);
        }, 350);
    }

    function routeEndpointIcon(kind, label) {
        const isOrigin = kind === "origin";
        const iconName = isOrigin ? "planeDeparture" : "planeArrival";
        const color = isOrigin ? "#3fb950" : "#f85149";
        return L.divIcon({
            className: `leaflet-div-icon aegis-map-icon aegis-route-endpoint aegis-route-endpoint--${kind}`,
            html: `<div class="aegis-route-endpoint__wrap">
                ${svgWrap(svgIcons[iconName], color, 16)}
                <span class="aegis-route-endpoint__code">${escapeHtml(label)}</span>
            </div>`,
            iconSize: [72, 28],
            iconAnchor: [36, 14]
        });
    }

    function clearFlightRoute(instance) {
        if (instance.flightRouteLayer) {
            instance.flightRouteLayer.clearLayers();
        }
    }

    function drawFlightRoute(instance, route) {
        clearFlightRoute(instance);
        if (!route) return;

        const layer = instance.flightRouteLayer;

        if (route.flownTrack && route.flownTrack.length >= 2) {
            L.polyline(
                route.flownTrack.map(p => [p.lat, p.lng]),
                { color: "#58a6ff", weight: 2, opacity: 0.75, lineCap: "round" }
            ).addTo(layer);
        } else if (route.flownTrack && route.flownTrack.length === 1) {
            const p = route.flownTrack[0];
            L.circleMarker([p.lat, p.lng], {
                radius: 8,
                color: "#58a6ff",
                fillColor: "#58a6ff",
                fillOpacity: 0.35,
                weight: 2,
                className: "aegis-pulse-marker"
            }).addTo(layer);
        }

        if (route.path && route.path.length >= 2) {
            L.polyline(
                route.path.map(p => [p.lat, p.lng]),
                {
                    color: route.isEstimated ? "#58a6ff" : "#3ec6e0",
                    weight: 3,
                    opacity: 0.9,
                    dashArray: route.isEstimated ? null : "10 8",
                    lineCap: "round"
                }
            ).addTo(layer);
        }

        if (route.origin) {
            const marker = L.marker(
                [route.origin.lat, route.origin.lng],
                { icon: routeEndpointIcon("origin", route.origin.icao), zIndexOffset: 500 }
            );
            bindCardTooltip(marker, buildTooltipCard("ORIGEM", escapeHtml(route.origin.label), [
                { label: "ICAO", value: escapeHtml(route.origin.icao) }
            ]));
            marker.addTo(layer);
        }

        if (route.destination) {
            const marker = L.marker(
                [route.destination.lat, route.destination.lng],
                { icon: routeEndpointIcon("destination", route.destination.icao), zIndexOffset: 500 }
            );
            bindCardTooltip(marker, buildTooltipCard("DESTINO", escapeHtml(route.destination.label), [
                { label: "ICAO", value: escapeHtml(route.destination.icao) }
            ]));
            marker.addTo(layer);
        }
    }

    function updateAircraftLayer(instance, items) {
        const layer = instance.aircraftLayer;
        const markers = instance.aircraftMarkers;
        const incoming = new Set();

        (items || []).forEach(item => {
            const id = item.icao24 || item.id;
            if (!id) return;
            incoming.add(id);

            const latlng = [item.lat, item.lng];
            const selected = instance.selectedId === id;
            const icon = createAircraftIcon(item.heading, selected, item.pulse, item.category);

            if (markers[id]) {
                markers[id].setLatLng(latlng);
                markers[id].setIcon(icon);
                bindAircraftTooltip(markers[id], item);
            } else {
                const marker = L.marker(latlng, { icon, zIndexOffset: selected ? 1000 : 0 });
                bindAircraftTooltip(marker, item);
                marker.on("click", (e) => {
                    L.DomEvent.stopPropagation(e);
                    instance.dotNetRef.invokeMethodAsync("OnMarkerClick", { kind: "aircraft", id });
                });
                marker.addTo(layer);
                markers[id] = marker;
            }
        });

        Object.keys(markers).forEach(id => {
            if (!incoming.has(id)) {
                layer.removeLayer(markers[id]);
                delete markers[id];
            }
        });
    }

    function clearDrawPreview(instance) {
        if (instance.drawPreview) {
            instance.map.removeLayer(instance.drawPreview);
            instance.drawPreview = null;
        }
    }

    function hideCircleRadiusPrompt(instance) {
        if (instance.circleRadiusOverlay) {
            instance.circleRadiusOverlay.remove();
            instance.circleRadiusOverlay = null;
        }
    }

    function cancelActiveDraw(instance) {
        hideCircleRadiusPrompt(instance);
        instance.drawMode = null;
        instance.drawPoints = [];
        instance.drawAutoComplete = true;
        clearDrawPreview(instance);
        if (instance._drawClickHandler) {
            instance.map.off("click", instance._drawClickHandler);
            instance._drawClickHandler = null;
        }
    }

    function finalizeCircleDraw(instance, center, radiusMeters) {
        hideCircleRadiusPrompt(instance);
        clearDrawPreview(instance);
        instance.drawPreview = L.circle(center, {
            radius: radiusMeters,
            color: "#3ec6e0",
            fillOpacity: 0.15
        }).addTo(instance.map);

        const geometry = {
            type: "Point",
            coordinates: [center.lng, center.lat],
            properties: { radiusMeters }
        };

        if (instance.dotNetRef) {
            instance.dotNetRef.invokeMethodAsync("OnDrawReady", "circle", JSON.stringify(geometry));
        }

        freezeDraw(instance);
    }

    function showCircleRadiusPrompt(instance, center) {
        hideCircleRadiusPrompt(instance);

        const container = instance.map.getContainer();
        const overlay = document.createElement("div");
        overlay.className = "aegis-circle-radius-prompt aegis-circle-radius-prompt--map";
        overlay.innerHTML = `
            <div class="aegis-circle-radius-prompt__card">
                <div class="aegis-circle-radius-prompt__title">Raio do círculo</div>
                <div class="aegis-circle-radius-prompt__hint">Centro: ${center.lat.toFixed(4)}, ${center.lng.toFixed(4)}</div>
                <div class="aegis-circle-radius-prompt__row">
                    <input class="aegis-input aegis-circle-radius-prompt__input" type="number" min="0.1" step="0.1" value="5" />
                    <span class="aegis-circle-radius-prompt__unit">km</span>
                </div>
                <div class="aegis-circle-radius-prompt__actions">
                    <button type="button" class="aegis-btn" data-action="cancel">Cancelar</button>
                    <button type="button" class="aegis-btn aegis-btn--primary" data-action="confirm">Confirmar</button>
                </div>
            </div>`;

        const input = overlay.querySelector("input");
        const confirm = () => {
            const radiusKm = Number.parseFloat(input.value);
            if (!Number.isFinite(radiusKm) || radiusKm <= 0) {
                input.focus();
                return;
            }
            finalizeCircleDraw(instance, center, radiusKm * 1000);
        };

        overlay.querySelector('[data-action="confirm"]').addEventListener("click", confirm);
        overlay.querySelector('[data-action="cancel"]').addEventListener("click", () => {
            cancelActiveDraw(instance);
            if (instance.dotNetRef) {
                instance.dotNetRef.invokeMethodAsync("OnDrawCancelled");
            }
        });
        input.addEventListener("keydown", (ev) => {
            if (ev.key === "Enter") confirm();
            if (ev.key === "Escape") {
                cancelActiveDraw(instance);
                if (instance.dotNetRef) {
                    instance.dotNetRef.invokeMethodAsync("OnDrawCancelled");
                }
            }
        });

        container.appendChild(overlay);
        instance.circleRadiusOverlay = overlay;
        input.focus();
        input.select();
    }

    function isNearDrawVertex(click, vertex, thresholdMeters = 20) {
        return click.distanceTo(vertex) <= thresholdMeters;
    }

    function notifyDrawPointCount(instance) {
        if (instance?.dotNetRef) {
            instance.dotNetRef.invokeMethodAsync("OnDrawPointCountChanged", instance.drawPoints.length);
        }
    }

    function completeShapeDraw(instance) {
        if (!instance?.drawMode) return;

        const built = buildDrawGeometry(instance);
        if (!built) return;

        clearDrawPreview(instance);
        if (instance.drawMode === "polygon" && instance.drawPoints.length >= 3) {
            instance.drawPreview = L.polygon(instance.drawPoints, {
                color: "#3ec6e0",
                fillOpacity: 0.12
            }).addTo(instance.map);
        } else if (instance.drawMode === "line" && instance.drawPoints.length >= 2) {
            instance.drawPreview = L.polyline(instance.drawPoints, { color: "#3ec6e0" }).addTo(instance.map);
        } else {
            return;
        }

        if (instance.drawAutoComplete) {
            finishDraw(instance);
        } else {
            notifyDrawReady(instance);
        }
    }

    function buildDrawGeometry(instance) {
        if (!instance.drawMode || instance.drawPoints.length === 0) return null;

        const kind = instance.drawMode;
        const pts = instance.drawPoints;
        let geometry = null;

        if (kind === "pin" && pts.length >= 1) {
            geometry = { type: "Point", coordinates: [pts[0].lng, pts[0].lat] };
        } else if (kind === "line" && pts.length >= 2) {
            geometry = {
                type: "LineString",
                coordinates: pts.map(p => [p.lng, p.lat])
            };
        } else if (kind === "polygon" && pts.length >= 3) {
            const ring = pts.map(p => [p.lng, p.lat]);
            ring.push(ring[0]);
            geometry = { type: "Polygon", coordinates: [ring] };
        } else if (kind === "circle" && pts.length >= 2) {
            const center = pts[0];
            const edge = pts[1];
            const radius = center.distanceTo(edge);
            geometry = {
                type: "Point",
                coordinates: [center.lng, center.lat],
                properties: { radiusMeters: radius }
            };
        }

        if (!geometry) return null;
        return { kind, geometry };
    }

    function notifyDrawReady(instance) {
        const built = buildDrawGeometry(instance);
        if (!built || !instance.dotNetRef) return;
        instance.dotNetRef.invokeMethodAsync("OnDrawReady", built.kind, JSON.stringify(built.geometry));
        freezeDraw(instance);
    }

    function freezeDraw(instance) {
        if (instance._drawClickHandler) {
            instance.map.off("click", instance._drawClickHandler);
            instance._drawClickHandler = null;
        }
        instance.drawMode = null;
        instance.drawPoints = [];
        instance.drawAutoComplete = true;
    }

    function finishDraw(instance) {
        const built = buildDrawGeometry(instance);
        if (!built) return;

        instance.dotNetRef.invokeMethodAsync("OnDrawCompleted", built.kind, JSON.stringify(built.geometry));

        instance.drawMode = null;
        instance.drawPoints = [];
        instance.drawAutoComplete = true;
        clearDrawPreview(instance);
        instance.map.off("click", instance._drawClickHandler);
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function buildWeatherAlertTooltip(item) {
        const badge = escapeHtml((item.source || "METEO").split("·")[0].trim());
        const rows = [
            { label: "Fonte", value: escapeHtml(item.source || "—") },
            { label: "Severidade", value: escapeHtml(item.severity || "—") },
            { label: "Evento", value: escapeHtml(item.eventType || item.subtitle || "—") },
            { label: "Região", value: escapeHtml(item.region || "—") },
            { label: "Início", value: escapeHtml(item.time || "—") },
            { label: "Fim", value: escapeHtml(item.validUntil || "—") },
            { label: "Riscos", value: escapeHtml(item.risks || "—") },
            { label: "Descrição", value: escapeHtml(item.detail || "—") },
            { label: "Orientações", value: escapeHtml(item.instructions || "—") }
        ].filter(row => row.value !== escapeHtml("—"));
        return buildTooltipCard(badge, escapeHtml(item.title || "—"), rows);
    }

    function buildTooltipCard(badge, title, rows, footer, imageUrl) {
        const imageHtml = imageUrl
            ? `<div class="aegis-tooltip-card__image"><img src="${escapeHtml(imageUrl)}" alt="" loading="lazy" referrerpolicy="no-referrer"/></div>`
            : "";
        const rowsHtml = (rows || []).map(row =>
            `<div class="aegis-tooltip-card__row">
                <span class="aegis-tooltip-card__label">${row.label}</span>
                <span class="aegis-tooltip-card__value">${row.value}</span>
            </div>`).join("");
        return `<div class="aegis-tooltip-card">
            ${imageHtml}
            ${badge ? `<div class="aegis-tooltip-card__badge">${badge}</div>` : ""}
            <div class="aegis-tooltip-card__title">${title}</div>
            <div class="aegis-tooltip-card__body">${rowsHtml}</div>
            ${footer ? `<div class="aegis-tooltip-card__footer">${footer}</div>` : ""}
        </div>`;
    }

    function bindSimpleTooltip(marker, badge, title) {
        const html = `<div class="aegis-tooltip-simple">
            <span class="aegis-tooltip-simple__badge">${escapeHtml(badge)}</span>
            <span class="aegis-tooltip-simple__title">${escapeHtml(title || "—")}</span>
        </div>`;
        marker.bindTooltip(html, {
            direction: "top",
            className: "aegis-tooltip-simple-wrapper",
            opacity: 1
        });
    }

    function bindCardTooltip(marker, html) {
        marker.bindTooltip(html, {
            direction: "top",
            className: "aegis-tooltip-card-wrapper",
            opacity: 1
        });
    }

    function formatAircraftTooltip(item) {
        const callsign = item.callsign || item.icao24 || "—";
        return { badge: "VOO", title: callsign };
    }

    function bindAircraftTooltip(marker, item) {
        const tip = formatAircraftTooltip(item);
        bindSimpleTooltip(marker, tip.badge, tip.title);
    }

    function poiIcon() {
        return mapMarkerIcon("exclamation", "#f0883e", 16, "aegis-poi-icon");
    }

    function featureStyle(feature) {
        const kind = feature.properties?.kind;
        if (kind === "road") {
            return { color: "#9aa0a6", weight: 2, opacity: 0.85 };
        }
        if (kind === "public_building") {
            return {
                color: "#a371f7",
                weight: 2,
                fillColor: "#a371f7",
                fillOpacity: 0.12,
                dashArray: "4 2"
            };
        }
        if (kind === "building") {
            return { color: "#3ec6e0", weight: 1, fillColor: "#3ec6e0", fillOpacity: 0.18 };
        }
        return { color: "#d29922", weight: 1, fillColor: "#d29922", fillOpacity: 0.25 };
    }

    function bindFeatureTooltip(layer, feature) {
        const name = feature.properties?.name || feature.properties?.category || "Feature";
        const badge = featureBadge(feature.properties?.kind);
        bindSimpleTooltip(layer, badge, name);
    }

    function featureBadge(kind) {
        switch (kind) {
            case "public_camera": return "CÂMERA";
            case "erb": return "ERB";
            case "port": return "PORTO";
            case "radio_tower": return "RÁDIO";
            case "repeater": return "REPETIDOR";
            case "road": return "VIA";
            case "building":
            case "public_building": return "EDIFÍCIO";
            case "poi": return "POI";
            default: return "OSM";
        }
    }

    function ensureMarkerLayersOnTop(map) {
        ["overlayPane", "markerPane", "tooltipPane", "popupPane"].forEach(name => {
            const pane = map.getPane(name);
            if (pane) pane.style.zIndex = "650";
        });

        map.getContainer().querySelectorAll(".maplibregl-map, .leaflet-maplibregl-container").forEach(el => {
            el.style.zIndex = "200";
        });
    }

    function refreshOverlayLayers(inst) {
        if (!inst?.map) return;
        ensureMarkerLayersOnTop(inst.map);
    }

    function addRasterBaseLayer(map, el, options, tileOpts) {
        if (el) {
            el.classList.add("aegis-map--raster-dark");
        }

        const primaryLayer = L.tileLayer(options.tileUrl, tileOpts).addTo(map);

        if (options.fallbackTileUrl && options.fallbackTileUrl !== options.tileUrl) {
            let switched = false;
            primaryLayer.on("tileerror", () => {
                if (switched) return;
                switched = true;
                map.removeLayer(primaryLayer);
                L.tileLayer(options.fallbackTileUrl, tileOpts).addTo(map);
            });
        }
    }

    function addBaseLayer(map, el, options) {
        const tileOpts = {
            attribution: options.attribution || "",
            maxZoom: options.maxZoom || 20,
            minZoom: options.minZoom || 2,
            subdomains: "abc"
        };

        const useVector = Boolean(options.styleUrl) && typeof L.maplibreGL === "function";
        if (!useVector) {
            addRasterBaseLayer(map, el, options, tileOpts);
            return;
        }

        try {
            const vectorLayer = L.maplibreGL({ style: options.styleUrl });
            let usingRasterFallback = false;

            const applyRasterFallback = (reason) => {
                if (usingRasterFallback) {
                    return;
                }

                usingRasterFallback = true;
                console.warn("Aegis map: falling back to raster tiles.", reason || "");
                try {
                    map.removeLayer(vectorLayer);
                } catch {
                    // ignore cleanup errors
                }

                addRasterBaseLayer(map, el, options, tileOpts);
                setTimeout(() => map.invalidateSize(), 0);
            };

            vectorLayer.addTo(map);

            const glMap = vectorLayer.getMaplibreMap?.();
            if (!glMap) {
                applyRasterFallback("maplibre map unavailable");
                return;
            }

            glMap.on("error", event => applyRasterFallback(event?.error || "maplibre error"));
            glMap.once("idle", () => ensureMarkerLayersOnTop(map));
            setTimeout(() => {
                if (!usingRasterFallback && !glMap.isStyleLoaded?.()) {
                    applyRasterFallback("style load timeout");
                }
            }, 5000);
        } catch (error) {
            console.warn("Aegis map: maplibre init failed, using raster tiles.", error);
            addRasterBaseLayer(map, el, options, tileOpts);
        }
    }

    return {
        init(dotNetRef, elementId, options) {
            const el = document.getElementById(elementId);
            if (!el) return null;

            const map = L.map(el, {
                zoomControl: false,
                attributionControl: true,
                minZoom: options.minZoom || 2,
                maxZoom: options.maxZoom || 20
            }).setView(
                [options.defaultLat, options.defaultLng],
                options.defaultZoom || 5
            );

            addBaseLayer(map, el, options);

            L.control.scale({ imperial: false, metric: true }).addTo(map);

            const instance = {
                map,
                dotNetRef,
                shodanLayer: L.layerGroup().addTo(map),
                newsLayer: L.layerGroup().addTo(map),
                ransomwareLayer: L.layerGroup().addTo(map),
                seismicLayer: L.layerGroup().addTo(map),
                alertsLayer: L.layerGroup().addTo(map),
                shipsLayer: L.layerGroup().addTo(map),
                aircraftLayer: L.layerGroup().addTo(map),
                flightRouteLayer: L.layerGroup().addTo(map),
                inpeOverlayLayer: null,
                featuresLayer: L.geoJSON(null, {
                    style(feature) {
                        return featureStyle(feature);
                    },
                    pointToLayer(feature, latlng) {
                        const kind = feature.properties?.kind;
                        if (kind === "radio_tower") {
                            return L.marker(latlng, { icon: mapMarkerIcon("tower", "#58a6ff", 16, "aegis-tower-icon") });
                        }
                        if (kind === "repeater") {
                            return L.marker(latlng, { icon: mapMarkerIcon("repeater", "#a371f7", 16, "aegis-repeater-icon") });
                        }
                        if (kind === "erb") {
                            return L.marker(latlng, { icon: mapMarkerIcon("erb", "#3fb950", 16, "aegis-erb-icon") });
                        }
                        if (kind === "public_camera") {
                            return L.marker(latlng, { icon: mapMarkerIcon("camera", "#f778ba", 16, "aegis-camera-icon") });
                        }
                        if (kind === "port") {
                            return L.marker(latlng, { icon: mapMarkerIcon("port", "#79c0ff", 16, "aegis-port-icon") });
                        }
                        if (kind === "poi") {
                            return L.marker(latlng, {
                                icon: poiIcon()
                            });
                        }
                        return L.circleMarker(latlng, {
                            radius: 5,
                            color: "#d29922",
                            fillColor: "#d29922",
                            fillOpacity: 0.85,
                            weight: 1
                        });
                    },
                    onEachFeature(feature, layer) {
                        bindFeatureTooltip(layer, feature);
                        layer.on("click", (e) => {
                            L.DomEvent.stopPropagation(e);
                            const props = feature.properties || {};
                            const id = String(props.id || feature.id || "");
                            const kind = props.kind || "feature";
                            const clickKind = kind === "public_camera" ? "public_camera"
                                : kind === "erb" ? "erb"
                                : kind === "port" ? "port"
                                : id.startsWith("brazuca-camera/") ? "public_camera"
                                : id.startsWith("anatel-erb/") ? "erb"
                                : id.startsWith("brazuca-port/") ? "port"
                                : kind;
                            const coords = feature.geometry?.coordinates;
                            instance.dotNetRef.invokeMethodAsync("OnMarkerClick", {
                                kind: clickKind,
                                id,
                                name: props.name,
                                category: props.category,
                                url: props.url,
                                operator: props.operator,
                                technology: props.technology,
                                city: props["addr:city"],
                                state: props["addr:state"],
                                lat: Array.isArray(coords) ? coords[1] : null,
                                lng: Array.isArray(coords) ? coords[0] : null
                            });
                        });
                    }
                }).addTo(map),
                annotationsLayer: L.layerGroup().addTo(map),
                geofencesLayer: L.layerGroup().addTo(map),
                heatLayer: null,
                aircraftMarkers: {},
                shodanMarkers: {},
                lastAircraftItems: [],
                drawMode: null,
                drawPoints: [],
                drawPreview: null,
                selectedId: null,
                mapZoom: options.defaultZoom || 5,
                _drawClickHandler: null,
                viewportTimer: null
            };

            map.on("moveend", () => notifyViewport(instance));
            map.on("zoomend", () => notifyViewport(instance));
            map.on("click", (e) => {
                if (instance.drawMode) return;
                dotNetRef.invokeMethodAsync("OnMapClick", { lat: e.latlng.lat, lng: e.latlng.lng });
            });

            instances[elementId] = instance;

            ensureMarkerLayersOnTop(map);

            setTimeout(() => {
                map.invalidateSize();
                ensureMarkerLayersOnTop(map);
                dotNetRef.invokeMethodAsync("OnReady");
                notifyViewport(instance);
            }, 50);

            return elementId;
        },

        setView(mapId, lat, lng, zoom) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.map.setView([lat, lng], zoom);
        },

        fitBounds(mapId, south, west, north, east) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.map.fitBounds([[south, west], [north, east]]);
        },

        setAircraft(mapId, items) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.lastAircraftItems = items || [];
            updateAircraftLayer(inst, inst.lastAircraftItems);
            refreshOverlayLayers(inst);
        },

        setFeatures(mapId, items) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.featuresLayer.clearLayers();
            const zoom = currentMapZoom(inst);
            const visibleItems = (items || []).filter(f => isFeatureVisibleAtZoom(f.kind, zoom));
            if (visibleItems.length === 0) return;

            const fc = {
                type: "FeatureCollection",
                features: visibleItems.map(f => ({
                    type: "Feature",
                    id: f.id,
                    properties: {
                        id: f.id,
                        name: f.name,
                        category: f.category,
                        kind: f.kind,
                        url: f.url,
                        operator: f.operatorName,
                        technology: f.technology,
                        "addr:city": f.city,
                        "addr:state": f.state
                    },
                    geometry: typeof f.geometry === "string" ? JSON.parse(f.geometry) : f.geometry
                }))
            };
            inst.featuresLayer.addData(fc);
        },

        setAnnotations(mapId, items) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.annotationsLayer.clearLayers();

            (items || []).forEach(a => {
                let geom = typeof a.geometry === "string" ? JSON.parse(a.geometry) : a.geometry;
                if (!geom) return;

                if (geom.type === "Point") {
                    const [lng, lat] = geom.coordinates;
                    const radius = geom.properties?.radiusMeters;
                    if (radius) {
                        L.circle([lat, lng], { radius, color: a.color || "#3ec6e0", fillOpacity: 0.1 }).addTo(inst.annotationsLayer);
                    } else {
                        L.circleMarker([lat, lng], { radius: 6, color: a.color || "#3ec6e0" }).addTo(inst.annotationsLayer);
                    }
                } else {
                    L.geoJSON(geom, { style: { color: a.color || "#3ec6e0", weight: 2 } }).addTo(inst.annotationsLayer);
                }
            });
        },

        setGeofences(mapId, items) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.geofencesLayer.clearLayers();

            (items || []).forEach(g => {
                if (!g.geometry) return;
                let geom = typeof g.geometry === "string" ? JSON.parse(g.geometry) : g.geometry;
                const color = g.enabled ? "#f85149" : "#666";
                const name = escapeHtml(g.name || "Cerca");

                if (geom.type === "Circle" && geom.center && geom.radiusMeters) {
                    const [lng, lat] = geom.center;
                    const circle = L.circle([lat, lng], {
                        radius: geom.radiusMeters,
                        color,
                        weight: 2,
                        fillColor: color,
                        fillOpacity: 0.12,
                        dashArray: g.enabled ? null : "6"
                    });
                    circle.bindTooltip(buildTooltipCard("CERCA", name, [
                        { label: "Status", value: g.enabled ? "Ativa" : "Inativa" },
                        { label: "Raio", value: `${Math.round(geom.radiusMeters)} m` }
                    ]));
                    circle.addTo(inst.geofencesLayer);
                    return;
                }

                if (geom.type === "Polygon" && geom.coordinates) {
                    const polygon = L.geoJSON({
                        type: "Feature",
                        geometry: { type: "Polygon", coordinates: geom.coordinates }
                    }, {
                        style: {
                            color,
                            weight: 2,
                            fillColor: color,
                            fillOpacity: 0.12,
                            dashArray: g.enabled ? null : "6"
                        }
                    });
                    polygon.bindTooltip(buildTooltipCard("CERCA", name, [
                        { label: "Status", value: g.enabled ? "Ativa" : "Inativa" }
                    ]));
                    polygon.addTo(inst.geofencesLayer);
                }
            });
        },

        setHeatmap(mapId, points) {
            const inst = instances[mapId];
            if (!inst) return;

            if (inst.heatLayer) {
                inst.map.removeLayer(inst.heatLayer);
                inst.heatLayer = null;
            }

            if (!points || points.length < 3) return;

            const heatPoints = points.map(p => [p.lat, p.lng, p.weight || 1]);
            if (typeof L.heatLayer === "function") {
                inst.heatLayer = L.heatLayer(heatPoints, { radius: 25, blur: 15, maxZoom: 17 }).addTo(inst.map);
            }
        },

        setShodan(mapId, items) {
            const inst = instances[mapId];
            if (!inst) return;

            const layer = inst.shodanLayer;
            const markers = inst.shodanMarkers;
            const incoming = new Set();

            (items || []).forEach(h => {
                const id = h.ip || h.id;
                if (!id || h.lat == null || h.lng == null) return;
                incoming.add(id);

                const latlng = [h.lat, h.lng];
                const color = h.vulnerable ? "#22d3ee" : "#14b8a6";
                const iconClass = h.vulnerable ? "aegis-host-icon aegis-host-icon--vuln" : "aegis-host-icon";
                const icon = mapMarkerIcon("desktop", color, 16, iconClass);
                const ip = h.ip || "—";
                const port = h.port ? `:${h.port}` : "";
                const title = `${ip}${port}`;

                if (markers[id]) {
                    markers[id].setLatLng(latlng);
                    markers[id].setIcon(icon);
                    bindSimpleTooltip(markers[id], h.vulnerable ? "HOST VULN" : "HOST", title);
                } else {
                    const marker = L.marker(latlng, { icon });
                    bindSimpleTooltip(marker, h.vulnerable ? "HOST VULN" : "HOST", title);
                    marker.on("click", (e) => {
                        L.DomEvent.stopPropagation(e);
                        inst.dotNetRef.invokeMethodAsync("OnMarkerClick", { kind: "shodan", id: h.ip });
                    });
                    marker.addTo(layer);
                    markers[id] = marker;
                }
            });

            Object.keys(markers).forEach(id => {
                if (!incoming.has(id)) {
                    layer.removeLayer(markers[id]);
                    delete markers[id];
                }
            });
            refreshOverlayLayers(inst);
        },

        setNews(mapId, items) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.newsLayer.clearLayers();
            (items || []).forEach(n => {
                const icon = mapMarkerIcon("lightbulb", "#ffd43b", 16, "aegis-news-icon");
                const marker = L.marker([n.lat, n.lng], { icon });
                bindSimpleTooltip(marker, "NOTÍCIA", n.title || "Notícia");
                marker.on("click", (e) => {
                    L.DomEvent.stopPropagation(e);
                    inst.dotNetRef.invokeMethodAsync("OnMarkerClick", { kind: "news", id: n.id });
                });
                marker.addTo(inst.newsLayer);
            });
            refreshOverlayLayers(inst);
        },

        setRansomware(mapId, items) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.ransomwareLayer.clearLayers();
            (items || []).forEach(v => {
                const icon = mapMarkerIcon("skull-crossbones", "#e879f9", 18, "aegis-ransomware-icon");
                const marker = L.marker([v.lat, v.lng], { icon, zIndexOffset: 400 });
                bindSimpleTooltip(marker, "RANSOMWARE", v.victim || "Vítima");
                marker.on("click", (e) => {
                    L.DomEvent.stopPropagation(e);
                    inst.dotNetRef.invokeMethodAsync("OnMarkerClick", { kind: "ransomware", id: v.id || v.url });
                });
                marker.addTo(inst.ransomwareLayer);
            });
            refreshOverlayLayers(inst);
        },

        setGeoMarkers(mapId, layerKey, items) {
            const inst = instances[mapId];
            if (!inst) return;
            const layer = layerKey === "ships"
                ? inst.shipsLayer
                : layerKey === "alerts"
                    ? inst.alertsLayer
                    : inst.seismicLayer;
            if (!layer) return;
            layer.clearLayers();

            const zoom = currentMapZoom(inst);
            if (!isGeoLayerVisibleAtZoom(layerKey, zoom)) {
                refreshOverlayLayers(inst);
                return;
            }

            const config = {
                ships: { icon: "ship", color: "#38bdf8", badge: "NAVIO" },
                alerts: { icon: "alert", color: "#ef4444", badge: "METEO" },
                seismic: { icon: "seismic", color: "#f0883e", badge: "SISMO" }
            };
            const cfg = config[layerKey] || config.seismic;

            (items || []).forEach(item => {
                if (item.lat == null || item.lng == null) return;
                const icon = layerKey === "seismic"
                    ? seismicPulseIcon(item.title, item.opacity)
                    : layerKey === "alerts"
                        ? inmetPulseIcon(item.opacity)
                        : mapMarkerIcon(cfg.icon, cfg.color, 16);
                const marker = L.marker([item.lat, item.lng], {
                    icon,
                    zIndexOffset: layerKey === "seismic" || layerKey === "alerts" ? 350 : 300
                });
                const badge = layerKey === "alerts"
                    ? escapeHtml((item.source || "METEO").split("·")[0].trim())
                    : cfg.badge;
                bindSimpleTooltip(marker, badge, item.title || "—");
                marker.on("click", (e) => {
                    L.DomEvent.stopPropagation(e);
                    inst.dotNetRef.invokeMethodAsync("OnMarkerClick", {
                        kind: layerKey,
                        id: item.id
                    });
                });
                marker.addTo(layer);
            });
            refreshOverlayLayers(inst);
        },

        setSelection(mapId, id) {
            const inst = instances[mapId];
            if (!inst) return;
            inst.selectedId = id;
            updateAircraftLayer(inst, inst.lastAircraftItems || []);
        },

        setFlightRoute(mapId, route) {
            const inst = instances[mapId];
            if (!inst) return;
            drawFlightRoute(inst, route);
        },

        enterDrawMode(mapId, kind, autoComplete = true) {
            const inst = instances[mapId];
            if (!inst) return;

            hideCircleRadiusPrompt(inst);
            inst.drawMode = kind;
            inst.drawAutoComplete = autoComplete !== false;
            inst.drawPoints = [];
            clearDrawPreview(inst);
            notifyDrawPointCount(inst);

            if (inst._drawClickHandler) {
                inst.map.off("click", inst._drawClickHandler);
            }

            inst._drawClickHandler = (e) => {
                const click = e.latlng;

                if (kind === "pin") {
                    inst.drawPoints.push(click);
                    notifyDrawPointCount(inst);
                    finishDraw(inst);
                    return;
                }

                if (kind === "circle") {
                    if (inst.drawPoints.length === 0) {
                        inst.drawPoints.push(click);
                        notifyDrawPointCount(inst);
                        inst.drawPreview = L.circle(click, { radius: 100, color: "#3ec6e0", dashArray: "4" }).addTo(inst.map);
                        showCircleRadiusPrompt(inst, click);
                    }
                    return;
                }

                if (kind === "polygon") {
                    inst.drawPoints.push(click);
                    notifyDrawPointCount(inst);
                    clearDrawPreview(inst);
                    if (inst.drawPoints.length >= 2) {
                        inst.drawPreview = L.polygon(inst.drawPoints, { color: "#3ec6e0", dashArray: "4" }).addTo(inst.map);
                    }
                    return;
                }

                if (kind === "line") {
                    inst.drawPoints.push(click);
                    notifyDrawPointCount(inst);
                    clearDrawPreview(inst);
                    if (inst.drawPoints.length >= 1) {
                        inst.drawPreview = L.polyline(inst.drawPoints, { color: "#3ec6e0", dashArray: "4" }).addTo(inst.map);
                    }
                }
            };

            inst.map.on("click", inst._drawClickHandler);
        },

        completePolygonDraw(mapId) {
            const inst = instances[mapId];
            if (!inst || inst.drawMode !== "polygon") return;
            completeShapeDraw(inst);
        },

        completeLineDraw(mapId) {
            const inst = instances[mapId];
            if (!inst || inst.drawMode !== "line") return;
            completeShapeDraw(inst);
        },

        cancelDraw(mapId) {
            const inst = instances[mapId];
            if (!inst) return;
            cancelActiveDraw(inst);
        },

        applyDrawPreview(mapId, kind, geometryGeoJson) {
            const inst = instances[mapId];
            if (!inst) return;

            inst.drawMode = null;
            inst.drawPoints = [];
            inst.drawAutoComplete = true;
            if (inst._drawClickHandler) {
                inst.map.off("click", inst._drawClickHandler);
                inst._drawClickHandler = null;
            }

            clearDrawPreview(inst);

            let geom;
            try {
                geom = typeof geometryGeoJson === "string" ? JSON.parse(geometryGeoJson) : geometryGeoJson;
            } catch {
                return;
            }

            if (kind === "circle" && geom.type === "Point" && geom.coordinates?.length >= 2) {
                const lng = geom.coordinates[0];
                const lat = geom.coordinates[1];
                const radius = geom.properties?.radiusMeters ?? 1000;
                inst.drawPreview = L.circle([lat, lng], {
                    radius,
                    color: "#3ec6e0",
                    fillOpacity: 0.15
                }).addTo(inst.map);
            } else if (kind === "polygon" && geom.type === "Polygon" && geom.coordinates?.[0]) {
                const ring = geom.coordinates[0].map(c => [c[1], c[0]]);
                inst.drawPreview = L.polygon(ring, { color: "#3ec6e0", fillOpacity: 0.12 }).addTo(inst.map);
            } else if (kind === "line" && geom.type === "LineString" && geom.coordinates?.length >= 2) {
                const pts = geom.coordinates.map(c => [c[1], c[0]]);
                inst.drawPreview = L.polyline(pts, { color: "#3ec6e0" }).addTo(inst.map);
            } else if (kind === "pin" && geom.type === "Point" && geom.coordinates?.length >= 2) {
                const lng = geom.coordinates[0];
                const lat = geom.coordinates[1];
                inst.drawPreview = L.circleMarker([lat, lng], { radius: 6, color: "#3ec6e0" }).addTo(inst.map);
            }
        },

        destroy(mapId) {
            const inst = instances[mapId];
            if (!inst) return;
            if (inst.viewportTimer) clearTimeout(inst.viewportTimer);
            inst.map.remove();
            delete instances[mapId];
        },

        setInpeOverlay(mapId, enabled) {
            const inst = instances[mapId];
            if (!inst) return;

            if (inst.inpeOverlayLayer) {
                inst.map.removeLayer(inst.inpeOverlayLayer);
                inst.inpeOverlayLayer = null;
            }

            const zoom = currentMapZoom(inst);
            if (!enabled || zoom < 7) {
                refreshOverlayLayers(inst);
                return;
            }

            inst.inpeOverlayLayer = L.tileLayer.wms("https://terrabrasilis.dpi.inpe.br/queimadas/geoserver/wms", {
                layers: "1_24hrs",
                format: "image/png",
                transparent: true,
                opacity: 0.55,
                version: "1.1.1",
                attribution: "© INPE/TerraBrasilis — focos 24h"
            });
            inst.inpeOverlayLayer.addTo(inst.map);
            if (typeof inst.inpeOverlayLayer.bringToBack === "function") {
                inst.inpeOverlayLayer.bringToBack();
            }
            refreshOverlayLayers(inst);
        },

        invalidateSize(mapId) {
            const inst = instances[mapId];
            if (inst) inst.map.invalidateSize();
        }
    };
})();
