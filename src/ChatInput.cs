using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GWYF.TextChat
{
    // TMP expects a BaseInput for IME even when the game uses the new Input System.
    public sealed class ChatImeInput : BaseInput
    {
        private Keyboard keyboard;
        internal readonly Queue<char> PendingText = new Queue<char>();
        internal string Composition = "";
        internal int LastCompositionFrame = -100;
        internal int LastTextFrame = -100;
        private IMECompositionMode mode;
        private Vector2 position;
        internal bool IsComposing => Composition.Length > 0 || Time.frameCount <= LastCompositionFrame + 1;
        internal void ResetComposition()
        {
            PendingText.Clear(); Composition = ""; LastCompositionFrame = LastTextFrame = -100;
        }
        private void Update()
        {
            if (keyboard == Keyboard.current) return;
            if (keyboard != null) { keyboard.onIMECompositionChange -= OnComposition; keyboard.onTextInput -= OnText; }
            keyboard = Keyboard.current;
            if (keyboard != null) { keyboard.onIMECompositionChange += OnComposition; keyboard.onTextInput += OnText; }
        }
        private void OnText(char character)
        {
            if (!Plugin.IsTyping || char.IsControl(character)) return;
            PendingText.Enqueue(character);
            LastTextFrame = Time.frameCount;
        }
        private void OnComposition(IMECompositionString value)
        {
            if (!Plugin.IsTyping) return;
            Composition = value.ToString();
            LastCompositionFrame = Time.frameCount;
        }
        public override string compositionString => Composition;
        public override IMECompositionMode imeCompositionMode
        {
            get => mode;
            set
            {
                mode = value;
                Keyboard.current?.SetIMEEnabled(value != IMECompositionMode.Off);
                // Keep legacy and new input backends in agreement when both are available.
                try { Input.imeCompositionMode = value; } catch (InvalidOperationException) { }
                if (value == IMECompositionMode.On) WindowsIme.Begin();
            }
        }
        public override Vector2 compositionCursorPos
        {
            get => position;
            set { position = value; Keyboard.current?.SetIMECursorPosition(value); }
        }
        protected override void OnDestroy()
        {
            if (keyboard != null) { keyboard.onIMECompositionChange -= OnComposition; keyboard.onTextInput -= OnText; }
            base.OnDestroy();
        }
    }

    public sealed class ChatInputField : TMP_InputField
    {
        internal ChatImeInput Ime;
        internal Action SubmitChat;
        internal Action CancelChat;
        internal int OpenedFrame;
        private readonly Event keyEvent = new Event();
        private bool submitPending;
        private int submitFrame;
        private float submitTime;

        internal void ResetSubmission() => submitPending = false;

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (!Plugin.IsTyping || !isFocused || Time.frameCount <= OpenedFrame + 1) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            // Same input backend as opening the chat; do not rely on the shared IMGUI queue.
            // Flush this frame's committed characters before taking the message snapshot.
            while (Ime.PendingText.Count > 0) Append(Ime.PendingText.Dequeue());
            if (!submitPending && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                submitPending = true; submitFrame = Time.frameCount; submitTime = Time.unscaledTime;
            }
            if (!submitPending) return;
            // Enter commits Hangul first. Submit once after composition and committed text settle.
            // Do not snapshot the draft in the same frame and lose the last syllable.
            if (!Ime.IsComposing && Time.frameCount > Math.Max(submitFrame, Ime.LastTextFrame) + 1)
            {
                submitPending = false;
                SubmitChat?.Invoke();
            }
            else if (Time.unscaledTime - submitTime > 1f) submitPending = false;
        }

        public override void OnUpdateSelected(BaseEventData data)
        {
            if (!isFocused) return;
            while (Ime.PendingText.Count > 0) Append(Ime.PendingText.Dequeue());
            while (Event.PopEvent(keyEvent))
            {
                if (keyEvent.rawType == EventType.KeyDown)
                {
                    if (keyEvent.keyCode == KeyCode.Return || keyEvent.keyCode == KeyCode.KeypadEnter)
                    {
                        // Submission is handled once in LateUpdate via the new Input System.
                        continue;
                    }
                    if (keyEvent.keyCode == KeyCode.Escape)
                    {
                        CancelChat?.Invoke();
                        data.Use();
                        return;
                    }
                    // Committed Unicode comes from onTextInput, not the legacy IMGUI queue.
                    // Retain key events only for editing and clipboard shortcuts.
                    keyEvent.character = '\0';
                    KeyPressed(keyEvent);
                }
                else if (keyEvent.commandName == "SelectAll") SelectAll();
            }
            UpdateLabel();
            data.Use();
        }
    }
}
