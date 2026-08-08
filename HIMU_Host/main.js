const video = document.getElementById('video');
const btn = document.getElementById('button');
const status = document.getElementById('status');
const sessionInput = document.getElementById('session-id');

const SESSION_ID_REGEX = /^\d{4}$/;

let pc = null;
let ws = null;

btn.onclick = () => connect();

function setStatus(msg) {
    status.textContent = msg;
    console.log(msg);
}

function cleanup(reason) {
    // Close WebRTC and clean frozen frame
    if (pc) {
        pc.close();
        pc = null;
    }
    video.srcObject = null;

    // Close WebSocket clearing callbacks first to avoid re-entry
    if (ws) {
        ws.onclose = null;
        ws.onerror = null;
        ws.onmessage = null;
        ws.close();
        ws = null;
    }

    btn.disabled = false;
    setStatus(reason);
}

async function connect() {
    const sessionId = sessionInput.value.trim();

    if (!SESSION_ID_REGEX.test(sessionId)) {
        setStatus('Enter valid session ID (4 digits)');
        sessionInput.focus();
        return;
    }

    cleanup('Connecting to signaling...');
    btn.disabled = true;

    let pendingCandidates = [];

    ws = new WebSocket(`ws://192.168.1.12:8080?type=browser&id=${sessionId}`);

    ws.onopen = () => {
        ws.binaryType = "arraybuffer"; // Force arraybuffer instead of Blob
        setStatus('Esperando offer de Unity...');
    };

    ws.onmessage = async (event) => {
        let json;
    
        // Handle arraybuffer and string
        if (event.data instanceof ArrayBuffer) {
            json = new TextDecoder().decode(event.data);
        } else if (event.data instanceof Blob) {
            json = await event.data.text();
        } else {
            json = event.data;
        }
        // Showing real json received
        console.log("RAW received:", json); 
        const msg = JSON.parse(json);
        
        if (msg.type === 4)
            cleanup('Unity disconnected');
        
        // Unity manda la offer dentro de msg.body como JSON string
        else if (msg.type === 5) { // ConnectionEvent.SDP
            const sdpData = JSON.parse(msg.body);

            pc = new RTCPeerConnection({
                iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
            });

            pc.ontrack = (event) => {
                const stream = event.streams?.[0] ?? new MediaStream([event.track]);
                video.srcObject = stream;
                video.play().catch(e => console.error('play() blocked:', e));
                setStatus('Receiving stream');
            };

            pc.onicecandidate = (event) => {
                if (event.candidate) {
                    ws?.send(JSON.stringify({
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
                if (pc.connectionState === 'failed') cleanup('Conexión WebRTC fallida');
            };

            // Apply offer sent by Unity
            await pc.setRemoteDescription({ type: 'offer', sdp: sdpData.sdp });

            // Apply ICE candidates received before offer
            for (const c of pendingCandidates) {
                await pc.addIceCandidate(c).catch(e => console.warn('ICE error:', e));
            }
            pendingCandidates = [];

            // Create and send answer
            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);

            ws?.send(JSON.stringify({
                type: 5, // ConnectionEvent.SDP
                body: JSON.stringify({ type: 'answer', sdp: answer.sdp })
            }));

            setStatus('Answer sent, negotiating ICE...');
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

    // Set callbacks for different situations
    ws.onerror = () => cleanup('Signaling error');
    ws.onclose = () => cleanup('Signaling closed');
}