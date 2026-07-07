const WebSocket = require('ws');
const PORT = 8080;
const wss = new WebSocket.Server({ port: PORT });
console.log("INIT: Signaling server en ws://localhost:8080");

let unityClient = null;
let browserClients = new Map();
let nextClientId = 0;
let pendingForUnity = [];

wss.on('connection', (ws, req) => {
    const clientType = new URL(req.url, 'http://localhost').searchParams.get('type');

    if (clientType === 'unity') {
        unityClient = ws;
        console.log("CONN: Unity conectado");

        for (const data of pendingForUnity) ws.send(data);
        pendingForUnity = [];

    } else {
        // Asignar ID único a cada móvil
        const clientId = nextClientId++;
        browserClients.set(clientId, ws);
        console.log(`CONN: Navegador conectado (id=${clientId})`);

        // Avisar a Unity que hay un cliente nuevo
        if (unityClient?.readyState === WebSocket.OPEN) {
            unityClient.send(JSON.stringify({ type: 99, clientId }));
        }

        ws.on('message', (data) => {
            const msg = JSON.parse(data);
            console.log(`MSSG: [browser ${clientId}] → ${msg.type}`);

            // Añadir clientId para que Unity sepa de qué móvil viene
            const tagged = JSON.stringify({ ...msg, clientId });

            if (unityClient?.readyState === WebSocket.OPEN) {
                unityClient.send(tagged);
            } else {
                pendingForUnity.push(tagged);
            }
        });

        ws.on('close', () => {
            console.log(`CLSE: Navegador ${clientId} desconectado`);
            browserClients.delete(clientId);
            if (unityClient?.readyState === WebSocket.OPEN) {
                unityClient.send(JSON.stringify({ type: 4, clientId })); // 4 = ConnectionEvent.DISCONNECT
            }
        });

        return; // el listener de mensajes ya está puesto arriba
    }

    ws.on('message', (data) => {
        const msg = JSON.parse(data);
        console.log(`MSSG: [unity] → ${msg.type}`);

        if (clientType === 'unity') {
            // Si el mensaje va dirigido a un cliente concreto
            if (msg.clientId !== undefined) {
                const target = browserClients.get(msg.clientId);
                if (target?.readyState === WebSocket.OPEN) {
                    target.send(data);
                }
            } else {
                // Broadcast a todos los móviles (offer e ICE iniciales)
                for (const [id, client] of browserClients) {
                    if (client.readyState === WebSocket.OPEN) {
                        client.send(data);
                    }
                }
            }
        }
    });

    ws.on('close', () => {
    console.log(`CLSE: Unity desconectado`);
    unityClient = null;
    pendingForUnity = [];
    cachedOffer = null;
    nextClientId = 0;      // ← añade esto
    browserClients.clear(); // ← y limpia browsers huérfanos
});
});