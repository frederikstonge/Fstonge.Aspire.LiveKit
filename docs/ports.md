1. LiveKit Server (livekit-server)

| Endpoint Name | Scheme / Protocol | Port (Default) | Target Port | Status | Purpose |
| --- | --- | --- | --- | --- | --- |
| http | http | 7880 | 7880 | Registered | REST API, Webhooks, and /metrics |
| ws | ws | 7880 | 7880 | Registered | Client WebRTC signaling & Agent connections |
| rtc-tcp | tcp | 7881 | 7881 | Registered | ICE/TCP WebRTC fallback transport |
| rtc-udp | udp | 7882 | 7882 | Registered | ICE/UDP WebRTC media multiplexer |
| turn-udp | udp | Configurable (e.g. 3478) | Matches port | Registered via .WithLiveKitServerTurn(...) | Standard TURN over UDP |
| turn-tls | tcp | Configurable (e.g. 5349) | Matches port | Registered via .WithLiveKitServerTurnTls(...) | Secure TURNS over TLS/TCP |

(Note: LiveKit serves /metrics directly on port 7880 unless an explicit prometheus_port override is supplied in YAML).

2. LiveKit Ingress (livekit/ingress)

| Endpoint Name | Scheme / Protocol | Port (Default) | Target Port | Status | Purpose |
| --- | --- | --- | --- | --- | --- |
| rtmp | tcp | 1935 | 1935 | Registered | RTMP video/audio ingestion (e.g. OBS Studio) |
| whip | http | 8080 | 8080 | Registered | WebRTC HTTP Ingestion Protocol (WHIP) |
| whip-udp | udp | 7885 | 7885 | Registered | WHIP UDP media transport |

3. LiveKit Egress (livekit/egress)

| Endpoint Name | Scheme / Protocol | Port | Target Port | Status | Purpose |
| --- | --- | --- | --- | --- | --- |
| health | http | Configurable | Matches port | Registered when healthPort specified | Health check endpoint |
| prometheus | http | Configurable | Matches port | Registered when prometheusPort specified | Prometheus metrics |

4. LiveKit SIP (livekit/sip)

| Endpoint Name | Scheme / Protocol | Port (Default) | Target Port | Status | Purpose |
| --- | --- | --- | --- | --- | --- |
| sip-udp | udp | 5060 | 5060 | Registered | SIP signaling over UDP |
| sip-tcp | tcp | 5060 | 5060 | Registered | SIP signaling over TCP |
| sip-tls | tcp | Configurable (e.g. 5061) | Matches port | Registered via .WithSipTls(...) | Encrypted SIP over TLS |
| rtp | udp | Range (e.g. 10000-20000) | Matches range | Registered when port range specified | Audio/RTP media transport |
| health | http | Configurable | Matches port | Registered when healthPort specified | Health check endpoint |
| prometheus | http | Configurable | Matches port | Registered when prometheusPort specified | Prometheus metrics |