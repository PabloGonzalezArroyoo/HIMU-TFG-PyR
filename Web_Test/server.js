const WebSocket = require('ws');

const wss = new WebSocket.Server({ port: 8080 });

console.log("WebSocket server running on ws://localhost:8080");

wss.on('connection', function connection(ws) {

    console.log("Client connected");

    ws.on('message', function incoming(data) {

        // reenviar el frame a todos los clientes conectados
        wss.clients.forEach(client => {
            if (client.readyState === WebSocket.OPEN) {
                client.send(data);
            }
        });

    });

});