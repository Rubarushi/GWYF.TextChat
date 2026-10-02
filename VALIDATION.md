# Validation — Text Chat 1.1.4

1.1.4: Release build passed with zero errors/warnings. Single Enter schedules one send after composition and committed text events settle; pending submission clears on close/reopen. Actual Korean keyboard/IME verification remains pending; no runtime claim.

## Prior 1.1.3 checks

1.1.3: Release build passed with zero errors/warnings. Existing 11 protocol checks passed. UI changes address truncation of latest messages, reliance on shared IMGUI Enter events and stale IME state. In-game fade/reopen and long-history regression checks still need runtime verification. No claim that the reported intermittent symptom was reproduced or fully confirmed fixed.

## Prior 1.1.2 checks

1.1.2: Release build passed with zero errors/warnings; installed DLL and packaged DLL updated. Caret fix verified against this game's TMP_InputField.GenerateCaret code: Geometry alignment centers caret at local Y=0; normal Middle alignment uses actual character descender/ascender. Removed title/divider/backgrounds/borders. On-screen appearance of this revision awaits a game restart; no visual runtime pass claimed.

## Prior 1.1.1 checks

UI revision 1.1.1: Release build passed with zero warnings/errors. Installed DLL and packaged DLL hashes match. This revision changes only presentation and runtime theme asset selection; networking/IME code is retained. The approved restart loaded 1.1.1 and selected the game font DangrekOutline. Computer Use was then stopped by the user. The final revision removes all game sprite selection and draws plain borders/backgrounds. Its Release build passed with zero warnings/errors; final on-screen appearance has not been inspected.

## Prior 1.1.0 checks

Environment: Windows x64, game 1.0.33 / Unity 6000.3.6f1, BepInEx 5.4.23.5.

- Release build: passed, zero errors and warnings.
- Protocol tests: 11 passed (Korean/emoji round trips, length-boundary surrogate handling, control filtering, empty/foreign/oversized/malformed packets).
- Actual game startup: BepInEx loaded Text Chat 1.1.0 and completed chainloader startup.
- Korean input repair from 1.0.0: user confirmed working before this transport change; IME/font handling retained.
- Actual UI observation: no chat panel on the main menu; HomeScene host session created the chat UI and logged Gameplay chat enabled. Automated keyboard input did not trigger chat or normal gameplay actions, so Enter/send behavior was not verified in this run.
- Main-menu gating requires a connected and ready Mirror client, a spawned local player and an explicit playable-scene allowlist. Sending and receiving also enforce this gate.

Remote host/client delivery with two Steam accounts, mixed modded/unmodded clients, reconnect and full scene-transition behavior still require multiplayer runtime testing. Startup and protocol tests alone do not establish remote delivery.

## Reproduce protocol checks

```powershell
dotnet restore ./tests/ProtocolTests.csproj --configfile ./NuGet.Config
dotnet run --project ./tests/ProtocolTests.csproj --no-restore
```

## Multiplayer release checks

1. Host and one remote client install 1.1.0: send in both directions and verify a single copy with the correct sender name.
2. Leave the main menu open and press Enter: no chat UI or chat send.
3. Start a session, enter HomeScene/CasinoScene: Enter opens chat; Korean/English input and Escape work.
4. Type movement/interact/item keys while chatting: no corresponding gameplay actions.
5. Return to main menu or disconnect: draft closes and panel disappears immediately.
6. Rejoin another host: handshake and chat recover; messages from the previous session are absent.
7. Unmodded host: no custom Mirror packet is sent and sending reports that the host needs the mod.
8. Modded host with an unmodded client: broadcast only to handshaken clients; verify the unmodded player stays connected.






