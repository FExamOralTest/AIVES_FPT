/**
 * AIVES High-Resolution Diagram Exporter (PNG 4x, PNG 8x & SVG Vector)
 */
function exportDiagram(btn, scale = 4) {
    const container = btn.closest('.section') || 
                      btn.closest('.diagram-container') || 
                      btn.closest('.diagram-wrapper') || 
                      btn.closest('.slide-container') || 
                      btn.parentElement.parentElement;
    const svg = container ? container.querySelector('svg') : document.querySelector('svg');
    if (!svg) {
        alert('Diagram SVG is not ready yet. Please wait a moment.');
        return;
    }

    const origHtml = btn.innerHTML;
    btn.innerHTML = '⏳ Đang xuất...';
    btn.disabled = true;

    try {
        const heading = (container ? container.querySelector('h2') || container.querySelector('.slide-title') : null) || 
                        document.querySelector('.header-title');
        let filename = (heading ? heading.innerText : 'diagram')
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '') || 'aives-diagram';
        filename += `-${scale}x`;

        let width = 0, height = 0;
        if (svg.viewBox && svg.viewBox.baseVal && svg.viewBox.baseVal.width > 0) {
            width = svg.viewBox.baseVal.width;
            height = svg.viewBox.baseVal.height;
        } else if (svg.getAttribute('viewBox')) {
            const parts = svg.getAttribute('viewBox').trim().split(/[\s,]+/);
            if (parts.length === 4) {
                width = parseFloat(parts[2]);
                height = parseFloat(parts[3]);
            }
        }
        if (!width || !height) {
            const rect = svg.getBoundingClientRect();
            width = rect.width || 900;
            height = rect.height || 600;
        }

        const padding = 20;
        const targetW = Math.round((width + padding * 2) * scale);
        const targetH = Math.round((height + padding * 2) * scale);

        const clone = svg.cloneNode(true);
        clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');
        clone.removeAttribute('style');

        // CRUCIAL FOR HIGH-RES: Set explicit pixel dimensions on SVG clone so browser rasterizes at full target resolution!
        clone.setAttribute('width', targetW + 'px');
        clone.setAttribute('height', targetH + 'px');
        clone.style.width = targetW + 'px';
        clone.style.height = targetH + 'px';

        // Clean foreignObject and preserve <br> line breaks
        clone.querySelectorAll('foreignObject').forEach(fo => {
            const w = parseFloat(fo.getAttribute('width')) || 0;
            const h = parseFloat(fo.getAttribute('height')) || 0;
            const x = w / 2;

            const textElem = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            textElem.setAttribute('x', x);
            textElem.setAttribute('text-anchor', 'middle');
            textElem.setAttribute('font-family', '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif');
            textElem.setAttribute('font-size', '13px');
            textElem.setAttribute('font-weight', '600');
            textElem.setAttribute('fill', '#0f172a');

            const labelSpan = fo.querySelector('.nodeLabel') || fo.querySelector('p') || fo.querySelector('span') || fo.firstElementChild;
            if (labelSpan) {
                const style = window.getComputedStyle(labelSpan);
                if (style.color && style.color !== 'rgba(0, 0, 0, 0)') textElem.setAttribute('fill', style.color);
                if (style.fontWeight) textElem.setAttribute('font-weight', style.fontWeight);
            }

            // Replace <br> tags with newline delimiter so lines are preserved
            const htmlWithBreaks = fo.innerHTML.replace(/<br\s*\/?>/gi, '\n');
            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = htmlWithBreaks;
            const plainText = (tempDiv.textContent || '').trim();
            const lines = plainText.split('\n').map(l => l.trim()).filter(l => l.length > 0);

            if (lines.length <= 1) {
                textElem.textContent = lines[0] || '';
                textElem.setAttribute('y', h / 2);
                textElem.setAttribute('dominant-baseline', 'central');
            } else {
                const fontSize = 13;
                const lineHeight = fontSize + 4;
                const startY = (h - (lines.length - 1) * lineHeight) / 2;
                lines.forEach((line, idx) => {
                    const tspan = document.createElementNS('http://www.w3.org/2000/svg', 'tspan');
                    tspan.textContent = line;
                    tspan.setAttribute('x', x);
                    tspan.setAttribute('y', startY + idx * lineHeight);
                    tspan.setAttribute('dominant-baseline', 'central');
                    textElem.appendChild(tspan);
                });
            }
            fo.parentNode.replaceChild(textElem, fo);
        });

        let defs = clone.querySelector('defs');
        if (!defs) {
            defs = document.createElementNS('http://www.w3.org/2000/svg', 'defs');
            clone.insertBefore(defs, clone.firstChild);
        }
        const fontStyle = document.createElementNS('http://www.w3.org/2000/svg', 'style');
        fontStyle.textContent = 'text, tspan { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }';
        defs.appendChild(fontStyle);

        const svgStr = new XMLSerializer().serializeToString(clone);
        const svgBlob = new Blob([svgStr], { type: 'image/svg+xml;charset=utf-8' });
        const blobUrl = URL.createObjectURL(svgBlob);

        const img = new Image();
        img.onload = () => {
            try {
                const canvas = document.createElement('canvas');
                canvas.width = targetW;
                canvas.height = targetH;
                const ctx = canvas.getContext('2d');

                ctx.imageSmoothingEnabled = true;
                ctx.imageSmoothingQuality = 'high';

                ctx.fillStyle = '#ffffff';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                ctx.drawImage(img, 0, 0, targetW, targetH);

                canvas.toBlob((blob) => {
                    if (!blob) {
                        downloadUri(canvas.toDataURL('image/png'), `${filename}.png`);
                    } else {
                        const downloadUrl = URL.createObjectURL(blob);
                        downloadUri(downloadUrl, `${filename}.png`);
                        setTimeout(() => URL.revokeObjectURL(downloadUrl), 2000);
                    }
                    URL.revokeObjectURL(blobUrl);
                    btn.innerHTML = '✅ Đã tải!';
                    setTimeout(() => { btn.innerHTML = origHtml; btn.disabled = false; }, 1500);
                }, 'image/png');
            } catch (err) {
                console.error('Canvas export error:', err);
                alert('Export failed: ' + err.message);
                btn.innerHTML = origHtml;
                btn.disabled = false;
                URL.revokeObjectURL(blobUrl);
            }
        };

        img.onerror = () => {
            alert('Failed to render SVG diagram.');
            btn.innerHTML = origHtml;
            btn.disabled = false;
            URL.revokeObjectURL(blobUrl);
        };

        img.src = blobUrl;
    } catch (err) {
        console.error('Export exception:', err);
        alert('Export error: ' + err.message);
        btn.innerHTML = origHtml;
        btn.disabled = false;
    }
}

/**
 * Direct SVG Vector Export (Crisp at any resolution, 0 blur)
 */
function exportSvg(btn) {
    const container = btn.closest('.section') || 
                      btn.closest('.diagram-container') || 
                      btn.closest('.diagram-wrapper') || 
                      btn.closest('.slide-container') || 
                      btn.parentElement.parentElement;
    const svg = container ? container.querySelector('svg') : document.querySelector('svg');
    if (!svg) {
        alert('Diagram SVG is not ready yet.');
        return;
    }

    const heading = (container ? container.querySelector('h2') || container.querySelector('.slide-title') : null) || 
                    document.querySelector('.header-title');
    let filename = (heading ? heading.innerText : 'diagram')
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-+|-+$/g, '') || 'aives-diagram';

    const clone = svg.cloneNode(true);
    clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
    clone.setAttribute('xmlns:xlink', 'http://www.w3.org/1999/xlink');

    const svgStr = new XMLSerializer().serializeToString(clone);
    const blob = new Blob([svgStr], { type: 'image/svg+xml;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    downloadUri(url, `${filename}.svg`);
    setTimeout(() => URL.revokeObjectURL(url), 2000);
}

function downloadUri(uri, name) {
    const a = document.createElement('a');
    a.download = name;
    a.href = uri;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}
