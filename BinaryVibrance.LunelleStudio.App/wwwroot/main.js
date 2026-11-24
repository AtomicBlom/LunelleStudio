// The browser studio's page: Kaldra's boot, on the page's canvas. The runtime is single-threaded, so the page needs no
// cross-origin isolation. Keys the workbench uses (Tab, F1 and the rest) are kept from the browser while in the canvas.
import { runKaldra } from './_content/BinaryVibrance.Kaldra.Hosting.Web/kaldra-boot.js'

await runKaldra({ canvas: '#kaldra-canvas' });
