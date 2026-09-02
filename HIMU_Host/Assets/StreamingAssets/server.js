const WebSocket = require('ws');

const PORT = process.env.PORT || 8080;
const wss = new WebSocket.Server({ port: PORT });
console.log(`INIT: Signaling server en ws://localhost:${PORT}`);

// We now support many Unity paralel sessions
// Each session has a unique session ID with 4 digits (p.e: "1234"),
// that Unity musts send as query param when connecting: ?type=unity&id=1234

const unityClients = new Map();       // sessionId -> Unity's websocket
const browserClients = new Map();     // sessionId -> Map<clientId, ws>
const pendingForUnity = new Map();    // sessionId -> pending messages array
let nextClientId = 0;                 // browser clients counter

const SESSION_ID_REGEX = /^\d{4}$/;

// Returns/Creates an entry for the new Unity session in clients map
function getOrCreateBrowserMap(sessionId) {
    if (!browserClients.has(sessionId)) {
        browserClients.set(sessionId, new Map());
    }
    return browserClients.get(sessionId);
}

// Returns pending messages of a session
function getPendingQueue(sessionId) {
    if (!pendingForUnity.has(sessionId)) {
        pendingForUnity.set(sessionId, []);
    }
    return pendingForUnity.get(sessionId);
}

wss.on('connection', (ws, req) => {
    const params = new URL(req.url, 'http://localhost').searchParams;
    const clientType = params.get('type');
    const sessionId = params.get('id');

    // Unity client: indicates its session ID
    if (clientType === 'unity') {
        // Validate session ID 
        if (!sessionId || !SESSION_ID_REGEX.test(sessionId)) {
            console.log(`ERR: Unity session connected with non-valid session ID ("${sessionId}") — a 4-digit number was expected`);
            ws.close(1008, 'Non-valid session ID, must be a 4-digit number');
            return;
        }

        // Non-valid user (is already connected)
        if (unityClients.has(sessionId)) {
            console.log(`ERR: Unity with session ID=${sessionId} is already connected`);
            ws.close(1008, 'session ID in use');
            return;
        }

        // Storing new client
        unityClients.set(sessionId, ws);
        console.log(`CONN: Unity connected (session=${sessionId})`);

        // Send pending messages with new session as destination
        const pending = getPendingQueue(sessionId);
        for (const data of pending) ws.send(data);
        pendingForUnity.set(sessionId, []);

        // Server behaviour when Unity session sends a message to clients
        ws.on('message', (data) => {
            const msg = JSON.parse(data);
            console.log(`MSSG: [unity ${sessionId}] → ${msg.type}`);

            const sessionBrowsers = browserClients.get(sessionId);
            if (!sessionBrowsers) return;

            if (msg.clientId !== undefined) {
                // Message for an specific client of this Unity session
                const target = sessionBrowsers.get(msg.clientId);
                if (target?.readyState === WebSocket.OPEN) {
                    target.send(data);
                }
            } else {
                // Broadcast to every client of this session (initial offer / ICE)
                for (const [, client] of sessionBrowsers) {
                    if (client.readyState === WebSocket.OPEN) {
                        client.send(data);
                    }
                }
            }
        });
        // Server behaviour when a Unity session disconnects
        ws.on('close', () => {
            // Inform and delete structures
            console.log(`CLSE: Unity disconnected (session=${sessionId})`);
            unityClients.delete(sessionId);
            pendingForUnity.delete(sessionId);

            // Inform each client of this session and clean
            const sessionBrowsers = browserClients.get(sessionId);
            if (sessionBrowsers) {
                for (const [, client] of sessionBrowsers) {
                    if (client.readyState === WebSocket.OPEN) {
                        client.close(1001, 'Unity session ended');
                    }
                }
            }
            browserClients.delete(sessionId);
        });

    } 
    else { // Browser Client: musts indicate which Unity session it wants to connect to
        if (!sessionId || !SESSION_ID_REGEX.test(sessionId)) {
            console.log(`ERR: Browser tried to connect with non-valid session ID ("${sessionId}")`);
            ws.close(1008, 'Must indicate ?id=XXXX with Unity session ID');
            return;
        }

        const clientId = nextClientId++;
        const sessionBrowsers = getOrCreateBrowserMap(sessionId);
        sessionBrowsers.set(clientId, ws);
        console.log(`CONN: Browser connected (session=${sessionId}, id=${clientId})`);

        // Inform Unity (if session is connected) that there is a new client
        const unityClient = unityClients.get(sessionId);
        if (unityClient?.readyState === WebSocket.OPEN) {
            unityClient.send(JSON.stringify({ type: 99, clientId }));
        }

        // Server behaviour when browser client sends a message (browser -> Unity)
        ws.on('message', (data) => {
            const msg = JSON.parse(data);
            console.log(`MSSG: [browser ${sessionId}/${clientId}] → ${msg.type}`);

            // Add clientID so Unity knows which client sent the message
            const tagged = JSON.stringify({ ...msg, clientId });

            // Sends message / pending messages
            const unity = unityClients.get(sessionId);
            if (unity?.readyState === WebSocket.OPEN) {
                unity.send(tagged);
            } else {
                getPendingQueue(sessionId).push(tagged);
            }
        });

        // Server behaviour when a browser client disconnects
        ws.on('close', () => {
            console.log(`CLSE: Browser client ${clientId} disconnected from session=${sessionId}`);
            sessionBrowsers.delete(clientId);

            const unity = unityClients.get(sessionId);
            if (unity?.readyState === WebSocket.OPEN) {
                unity.send(JSON.stringify({ type: 2, clientId })); // 4 = ConnectionEvent.DISCONNECT
            }
        });
    }
});
