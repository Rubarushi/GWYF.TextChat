# Text Chat

Text chat for **Gamble With Your Friends** (Unity Mono / Steam).

## Controls

- **Enter / Numpad Enter**: open chat.
- Type a message, then **Enter** to send and close.
- **Escape**: discard the draft and close.
- One Enter commits the active IME composition and sends after the committed characters reach the input field.

Chat appears in the lower-left corner. It shows Steam display names and the most recent 30 messages. The panel fades after 12 seconds and opens again with Enter. Messages are limited to 256 UTF-16 code units; send cooldown is 0.8 seconds. Movement, looking, interacting, item use, and game shortcuts are suppressed while typing. Closing chat restores the previous input layer and cursor.

## Multiplayer

Install **1.1.0 or newer on the host and each player who wants to send or see chat**. Messages use the game's existing **Mirror connection** over Steam P2P. Clients send to the host, which validates the sender and relays on the reliable channel only to clients that completed the mod handshake. Unmodded clients receive no custom chat packets. If the host lacks the mod, sending is unavailable and no custom packets are sent to that host.

Chat is enabled only after the game network connects, the local player spawns, and a playable scene is active (HomeScene, CasinoScene, WinStateScene, LoseStateScene, SummaryScene or GameTest). The main menu, loading scenes and disconnected state hide chat and prevent opening, sending or displaying messages. This is session-wide chat, not proximity chat; late joiners do not receive past history.

Steam lobby **member metadata** is used solely to discover host support for protocol 2. Steam lobby chat is not used for delivery. Version 1.0.0 used a different transport and is not compatible with 1.1.0.

No game assemblies are replaced. Chat displays cream text and a gold caret directly over gameplay, with no title, divider, background or borders. It borrows only the loaded game UI font at runtime, without game image or sprite assets. Fonts, materials and atlases are copied for the chat UI so the original assets remain unchanged. A dynamic Windows system font (Malgun Gothic when available) supplies missing Korean glyphs. The package does not redistribute game assets or fonts. Other operating systems and VR interfaces have not been verified.

## Install

### Thunderstore / r2modman

Import the release ZIP as a local mod, or install the published package. The dependency is `BepInEx-BepInExPack-5.4.2305`.

### Manual

1. Install BepInEx 5 for a 64-bit Unity Mono game.
2. Copy `BepInEx/plugins/TextChat/GWYF.TextChat.dll` from the release ZIP into the same path in the game folder.
3. Start the game through Steam.

Configuration: `BepInEx/config/gwyf.textchat.cfg`, `Display.VisibleSeconds` (default 12).

Remove `BepInEx/plugins/TextChat/GWYF.TextChat.dll` to uninstall this mod.

## 한국어

실제 게임 세션에서 **Enter로 채팅창 열기 → 입력 → Enter로 전송**합니다. **Esc**는 입력 취소입니다. **방장과 채팅 참가자는 모두 1.1.0 이상을 설치**해야 합니다. 게임의 Mirror 연결로 방장이 메시지를 중계하며, 모드 없는 참가자에게 채팅 패킷을 보내지 않습니다. 메인 화면과 연결 종료 상태에서는 채팅을 숨깁니다. 한글 조합 중에도 Enter 한 번으로 조합을 확정하고, 확정된 글자가 입력창에 반영된 뒤 자동 전송합니다. 채팅 중에는 이동·시점·상호작용·아이템 단축키를 막습니다.

## Build and package

Source lives in `src/`. Use .NET SDK 8 or newer and the installed game's managed assemblies. From this directory:

```powershell
./build.ps1
```

If BepInEx is elsewhere:

```powershell
./build.ps1 -BepInExDir 'C:\path\to\BepInEx\core'
```

`dist/TextChat-1.1.5.zip` is the Thunderstore upload package. It includes the DLL, manifest, README, changelog and 256×256 icon; it excludes game DLLs, build tools and BepInEx itself. Set `website_url` in `manifest.json` once you have a project page. Choose your own Thunderstore team when uploading.

Before publishing, validate two distinct Steam accounts in one lobby: host-to-client and client-to-host messages, Korean IME, Enter/Escape, gameplay input blocking, scene changes, leaving/rejoining, and mixed modded/unmodded players. See `VALIDATION.md` for the checks actually performed on this build.

## References

- [Mirror network messages](https://mirror-networking.gitbook.io/docs/manual/guides/communications/network-messages)
- [Thunderstore package requirements](https://wiki.thunderstore.io/mods/creating-a-package)







