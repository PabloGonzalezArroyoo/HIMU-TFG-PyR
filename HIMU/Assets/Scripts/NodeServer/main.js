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

    ws = new WebSocket('ws://192.168.1.21:8080?type=browser');

    ws.onopen = () => {
        ws.binaryType = "arraybuffer"; // forzar arraybuffer en vez de Blob
        setStatus('Esperando offer de Unity...');
    };

    ws.onmessage = async (event) => {
        let json;
    
    // Manejar tanto arraybuffer como string
    if (event.data instanceof ArrayBuffer) {
        json = new TextDecoder().decode(event.data);
    } else if (event.data instanceof Blob) {
        json = await event.data.text();
    } else {
        json = event.data;
    }

    console.log("RAW recibido:", json); // ahora debería mostrar el JSON real
        const msg = JSON.parse(json);

        // Unity manda la offer dentro de msg.body como JSON string
        if (msg.type === 5) { // ConnectionEvent.SDP
            const sdpData = JSON.parse(msg.body);

            pc = new RTCPeerConnection({
                iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
            });

            pc.ontrack = (event) => {
                const stream = event.streams?.[0] ?? new MediaStream([event.track]);
                video.srcObject = stream;
                video.play().catch(e => console.error('play() bloqueado:', e));
                setStatus('Stream recibido ✓');
            };

            pc.onicecandidate = (event) => {
                if (event.candidate) {
                    ws.send(JSON.stringify({
                        type: 6, // ConnectionEvent.ICE
                        body: JSON.stringify({
                            candidate: event.candidate.candidate,
                            sdpMid: event.candidate.sdpMid,
                            sdpMLineIndex: event.candidate.sdpMLineIndex
                        })
                    }));
                }
            };

            pc.onconnectionstatechange = () => {
                setStatus(`WebRTC: ${pc.connectionState}`);
                if (pc.connectionState === 'failed') btn.disabled = false;
            };

            // Aplicar la offer de Unity
            await pc.setRemoteDescription({ type: 'offer', sdp: sdpData.sdp });

            // Aplicar ICE candidates que llegaron antes que la offer
            for (const c of pendingCandidates) {
                await pc.addIceCandidate(c).catch(e => console.warn('ICE error:', e));
            }
            pendingCandidates = [];

            // Crear y enviar answer
            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);

            ws.send(JSON.stringify({
                type: 5, // ConnectionEvent.SDP
                body: JSON.stringify({ type: 'answer', sdp: answer.sdp })
            }));

            setStatus('Answer enviado, negociando ICE...');
        }
        else if (msg.type === 6) { // ConnectionEvent.ICE
            const iceData = JSON.parse(msg.body);
            const candidate = {
                candidate: iceData.candidate,
                sdpMid: iceData.sdpMid,
                sdpMLineIndex: iceData.sdpMLineIndex
            };

            if (!pc?.remoteDescription) {
                pendingCandidates.push(candidate);
            } else {
                await pc.addIceCandidate(candidate)
                    .catch(e => console.warn('ICE error:', e));
            }
        }
    };

    ws.onerror = () => {
        setStatus('Error de señalización');
        btn.disabled = false;
    };

    ws.onclose = () => {
        setStatus('Señalización cerrada');
        btn.disabled = false;
    };
}