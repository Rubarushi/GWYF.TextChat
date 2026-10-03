# Changelog

## 1.1.5

- Translate remaining chat hints and input errors to English; retain Korean IME input.

## 1.1.4

- One Enter commits and sends Korean text after IME events settle; preserve the last syllable.
- Clear pending submission on close/reopen and submit only once.

## 1.1.3

- Show the newest history lines after the panel fades/reopens; clip older lines instead of truncating new messages.
- Use the same Input System Enter handling for opening and sending; submit once after committed text is flushed.
- Reset stale IME composition state when opening/closing.
- Log send/receive events with character counts only for diagnosis.
- Move chat 60 reference pixels upward.

## 1.1.2

- Correct input caret Y alignment by replacing TMP geometry alignment with normal middle alignment and a centered stretch text rect.
- Remove chat title, yellow divider, panel/input backgrounds and all borders.
- Retain game font, input hints, IME and Mirror behavior.

## 1.1.1

- Reuse loaded game UI fonts at runtime; draw plain backgrounds and thin borders without game image assets.
- Compact warm-dark chat panel with cream borders and gold headings, caret and selection.
- Retain local Korean glyph fallback without modifying the game's font assets.
- Simplify Korean control hints; preserve IME and Mirror behavior.

## 1.1.0

- Send chat through the game's existing Mirror/Steam P2P connection.
- Require a modded host and handshake before sending custom packets.
- Relay only to modded participants; validate names and rate-limit at the host.
- Hide and disable chat on the main menu, while loading or disconnected.
- Preserve corrected Korean font and IME handling.

## 1.0.0

- Enter-to-open and Enter-to-send lobby text chat.
- Escape cancels typing and restores game controls.
- Steam display names, Unicode text and Korean font support.
- IME composition guard, bounded history and send cooldown.
- BepInEx 5 plugin and Thunderstore package.






