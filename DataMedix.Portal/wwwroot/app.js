window.downloadFileFromBase64 = function (fileName, base64, contentType) {
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', fileName);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

// Scrollbar espejo sincronizada para CronogramaMedicacion
// Muestra la barra de scroll horizontal en la parte superior de la tabla,
// evitando que el usuario tenga que ir al pie de la grilla para desplazarse.
window.initCronoScrollSync = function () {
    const mirror = document.getElementById('cronoScrollMirror');
    const wrap   = document.getElementById('cronoTableWrap');
    const inner  = document.getElementById('cronoScrollMirrorInner');
    if (!mirror || !wrap || !inner) return;

    inner.style.width = wrap.scrollWidth + 'px';

    // onscroll assignment reemplaza el handler anterior sin acumular listeners
    mirror.onscroll = function () { wrap.scrollLeft = mirror.scrollLeft; };
    wrap.onscroll   = function () { mirror.scrollLeft = wrap.scrollLeft; };
};
