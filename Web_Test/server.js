const WebSocket = require('ws');

const wss = new WebSocket.Server({ port: 8080 });
console.log("Signaling server en ws://localhost:8080");

let unityClient = null;
let browserClient = null;

// Cola de mensajes pendientes para Unity (por si el browser conecta primero)
let pendingForUnity = [];

wss.on('connection', (ws, req) => {
    const clientType = new URL(req.url, 'http://localhost').searchParams.get('type');

    if (clientType === 'unity') {
        unityClient = ws;
        console.log("Unity conectado");

        // Enviar mensajes que llegaron antes de que Unity conectara
        for (const data of pendingForUnity) {
            ws.send(data);
        }
        pendingForUnity = [];
    } else {
        browserClient = ws;
        console.log("Navegador conectado");
    }

    ws.on('message', (data) => {
        const msg = JSON.parse(data);
        console.log(`[${clientType}] → ${msg.type}`);

        if (clientType === 'unity') {
            // Unity → Browser
            if (browserClient?.readyState === WebSocket.OPEN) {
                browserClient.send(data);
            }
        } else {
            // Browser → Unity (o cola si Unity aún no está)
            if (unityClient?.readyState === WebSocket.OPEN) {
                unityClient.send(data);
            } else {
                console.log(`Unity no disponible, encolando mensaje: ${msg.type}`);
                pendingForUnity.push(data);
            }
        }
    });

    ws.on('close', () => {
        console.log(`${clientType} desconectado`);
        if (clientType === 'unity') unityClient = null;
        else browserClient = null;
    });
});
