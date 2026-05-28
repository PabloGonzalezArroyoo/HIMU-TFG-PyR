import { WebSocketServer } from 'ws';

const wss = new WebSocket.Server({ host: '0.0.0.0', port: 8080 });
const rooms = new Map(); // sessionId -> Set<WebSocket>

wss.on('connection', (socket) => {
  let currentRoom = null;

  socket.on('message', (raw) => {
    const msg = JSON.parse(raw);

    // Primer mensaje: el peer se une a una sala
    if (msg.type === 'join') {
      currentRoom = msg.sessionId;
      if (!rooms.has(currentRoom)) rooms.set(currentRoom, new Set());
      rooms.get(currentRoom).add(socket);
      return;
    }

    // Cualquier otro mensaje (offer, answer, ice) se reenvía
    // a todos los demás peers de la misma sala
    const room = rooms.get(currentRoom);
    if (!room) return;
    for (const peer of room) {
      if (peer !== socket && peer.readyState === 1) {
        peer.send(JSON.stringify(msg));
      }
    }
  });

  socket.on('close', () => {
    if (currentRoom) rooms.get(currentRoom)?.delete(socket);
  });
});