const video = document.getElementById('video');
const btn = document.getElementById('button');
const status = document.getElementById('status');

let pc = null;
let ws = null;

btn.onclick = () => connect();

function setStatus(msg) {
    status.textContent = msg;
    console.log(msg);
}

async function connect() {
    btn.disabled = true;
    let pendingCandidates = [];
    setStatus('Conectando a señalización...');

    ws = new WebSocket('ws://192.168.1.45:8080');
    ws.binaryType = "arraybuffer";

    ws.onopen = async () => {
        setStatus('Conectado a señalización. Creando oferta...');

        pc = new RTCPeerConnection({
            iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
        });

        pc.ontrack = (event) => {
            const stream = (event.streams && event.streams[0])
                ? event.streams[0]
                : new MediaStream([event.track]);

            video.srcObject = stream;
            video.play().catch(e => console.error('play() bloqueado:', e));
            setStatus('Stream recibido ✓');
        };

        pc.onicecandidate = (event) => {
            if (event.candidate) {
                ws.send(JSON.stringify({
                    type: 'ice',
                    candidate: event.candidate.candidate,
                    sdpMid: event.candidate.sdpMid,
                    sdpMLineIndex: event.candidate.sdpMLineIndex
                }));
            }
        };

        pc.onconnectionstatechange = () => {
            setStatus(`WebRTC: ${pc.connectionState}`);
            if (pc.connectionState === 'failed') btn.disabled = false;
        };

        pc.addTransceiver('video', { direction: 'recvonly' });

        const offer = await pc.createOffer();
        await pc.setLocalDescription(offer);
        ws.send(JSON.stringify({ type: 'offer', sdp: offer.sdp }));
        setStatus('Oferta enviada, esperando respuesta...');
    };

    ws.onmessage = async (event) => {
        let json;
        if (event.data instanceof ArrayBuffer) {
            json = new TextDecoder().decode(event.data);
        } else {
            json = event.data;
        }

        const msg = JSON.parse(json);

        if (msg.type === 'answer') {
            await pc.setRemoteDescription({ type: 'answer', sdp: msg.sdp });
            setStatus('Respuesta recibida, negociando ICE...');

            for (const c of pendingCandidates) {
                await pc.addIceCandidate(c).catch(e => console.warn("ICE error:", e));
            }
            pendingCandidates = [];
        }
        else if (msg.type === 'ice') {
            const candidate = {
                candidate: msg.candidate,
                sdpMid: msg.sdpMid,
                sdpMLineIndex: msg.sdpMLineIndex
            };

            if (!pc.remoteDescription) {
                pendingCandidates.push(candidate);
            } else {
                await pc.addIceCandidate(candidate).catch(e => console.warn("ICE error:", e));
            }
        }
    };

    ws.onerror = () => {
        setStatus('Error de señalización');
        btn.disabled = false;
    };
}