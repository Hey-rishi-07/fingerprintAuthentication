/**
 * CentralMFA Client-Side Scanner Integration
 * Communicates with local Mantra MFS100 Scanner Agent on http://127.0.0.1:8034
 * 100% Offline Intranet Friendly (Zero External Dependencies)
 */

const MfaClient = (function () {
    const DEFAULT_PORT = 8034;
    const SERVICE_URL = "http://127.0.0.1:" + DEFAULT_PORT;

    let _isOnline = false;

    async function checkStatus(callback) {
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 2000);

            const res = await fetch(SERVICE_URL + "/mfs100/info", {
                method: "GET",
                signal: controller.signal
            });
            clearTimeout(timeoutId);

            if (res.ok) {
                const data = await res.json();
                _isOnline = true;
                if (callback) callback(true, data);
                return;
            }
        } catch (e) {
            _isOnline = false;
        }

        if (callback) callback(false, null);
    }

    async function captureFingerprint(options, onSuccess, onError) {
        const quality = (options && options.quality) || 60;
        const timeout = (options && options.timeout) || 10;

        try {
            const res = await fetch(SERVICE_URL + "/mfs100/capture", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ Quality: quality, TimeOut: timeout })
            });

            let data = null;
            try { data = await res.json(); } catch (e) { }

            if (res.ok && data && data.Status && data.IsoTemplate) {
                if (onSuccess) onSuccess(data.IsoTemplate, data.Quality, data.BitmapData);
            } else {
                const errMsg = (data && data.ErrorMessage) || "Scanner did not return valid fingerprint data.";
                if (onError) onError(errMsg, data);
            }
        } catch (e) {
            if (onError) onError("Could not connect to local scanner on port " + DEFAULT_PORT + ". Ensure MfaScannerAgent is running.");
        }
    }

    async function simulateFingerprint(onSuccess, onError) {
        try {
            const res = await fetch(SERVICE_URL + "/mfs100/simulate", { method: "POST" });
            const data = await res.json();
            if (data && data.Status && data.IsoTemplate) {
                if (onSuccess) onSuccess(data.IsoTemplate, data.Quality);
            } else {
                if (onError) onError("Simulation failed on agent.");
            }
        } catch (e) {
            if (onError) onError("Could not connect to local scanner agent.");
        }
    }

        async function matchFingerprints(probeBase64, galleryBase64, onSuccess, onError) {
            try {
                const res = await fetch(SERVICE_URL + "/mfs100/match", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ Probe: probeBase64, Gallery: galleryBase64 })
                });
                const data = await res.json();
                if (data && data.Status) {
                    if (onSuccess) onSuccess(data.Score, data.Matched, data.IsRealHardware);
                } else {
                    if (onError) onError(data.ErrorMessage || "Matching failed on agent.");
                }
            } catch (e) {
                if (onError) onError("Could not connect to local scanner agent for matching: " + e.message);
            }
        }

        return {
            checkStatus: checkStatus,
            captureFingerprint: captureFingerprint,
            simulateFingerprint: simulateFingerprint,
            matchFingerprints: matchFingerprints,
            isOnline: function () { return _isOnline; }
        };
    })();
