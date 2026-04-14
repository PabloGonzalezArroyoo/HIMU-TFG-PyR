const WebSocket = require('ws');

const wss = new WebSocket.Server({ port: 8080 });
console.log("Signaling server en ws://localhost:8080");

// Guardamos las dos partes: Unity y el navegador
let unityClient = null;
let browserClient = null;

wss.on('connection', (ws, req) => {
    const clientType = new URL(req.url, 'http://localhost').searchParams.get('type');

    if (clientType === 'unity') {
        unityClient = ws;
        console.log("Unity conectado");
    } else {
        browserClient = ws;
        console.log("Navegador conectado");
    }

    ws.on('message', (data) => {
        const msg = JSON.parse(data);
        console.log(`[${clientType}] → ${msg.type}`);

        // Reenvía al otro extremo
        const target = clientType === 'unity' ? browserClient : unityClient;
        if (target?.readyState === WebSocket.OPEN) {
            target.send(data);
        }
    });

    ws.on('close', () => {
        console.log(`${clientType} desconectado`);
        if (clientType === 'unity') unityClient = null;
        else browserClient = null;
    });
});