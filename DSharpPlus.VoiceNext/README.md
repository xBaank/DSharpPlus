# Contributing
Most of the logic is held within the VoiceNextConnection folder, which is a bunch of files that pertain to the `VoiceNextConnection` class. The logic flow looks a little like this:

```mermaid
graph TD
    A[ConnectAsync] --> | Logic checks | B(OpCode 4 on normal Gateway)
    B --> | Wait for Voice State Update, then Voice Server Update | C{Open websocket}
    C --> D(Start heartbeat loop when we recieve OpCode 8 Hello payload)
    C --> E(Send OpCode 0 Identify payload on websocket voice gateway)
    C --> L(Connection has been severed, stop heartbeating)
    L --> M{Reconnect, send OpCode 7 Resume}
    M --> C
    M --> N(Invalid Session, Timed out Session)
    N --> B
    E --> F(Receive OpCode 2 Ready payload)
    F --> | Perform IP Discovery | G(Send OpCode 1 Select Protocol payload)
    G --> H(Receive OpCode 4 Session Description)
    H --> |Encrypt audio using Op 4's data | I(Send OpCode 5 Speaking payload)
    I --> J(Send encrypted audio over the UDP connection)
    J --> K(Send 5 frames of silence, stop transmission)
    K --> O(Send OpCode 5 Speaking with no parameters to indicate stop speaking)
    O --> I
```

The methods below are mapped out accordingly:
```
ConnectAsync -> VoiceNextExtension.ConnectAsync(DiscordChannel voiceChannel, bool muted, bool deafened)
Send Voice State Update (OpCode 4) -> VoiceNextExtension.ConnectAsync()
State and Server update -> VoiceNextExtension.ConnectAsync/HandleVoice{State, Server}Update()/VoiceNextExtension.ConnectAsync
Send OpCode 0 Identify payload -> VoiceNextConnection.ConnectAsync()
Connection severed -> VoiceNextConnection.WebsocketClosedAsync()
Reconnect -> VoiceNextConnection.ReconnectAsync()
All OpCode interaction -> VoiceNextConnection.WebsocketMessageAsync()
Send audio -> VoiceNextConnection.SpeakAsync(Stream audioStream, bool reset)
```
