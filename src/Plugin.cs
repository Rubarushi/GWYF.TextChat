using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GWYF.TextChat
{
    [BepInPlugin(Id, "Text Chat", "1.1.4")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "gwyf.textchat";
        internal static Plugin Instance;
        internal static bool IsTyping => Instance != null && Instance.open;
        private Harmony harmony;
        internal NetworkChat Chat;
        private bool sessionActive;
        private GameObject canvasObject, panel, inputObject, ownEventSystem;
        private CanvasGroup panelGroup;
        private TextMeshProUGUI history, hint;
        private ChatInputField field;
        private ChatImeInput ime;
        private TMP_FontAsset font;
        private TMP_FontAsset hangulFont;
        private bool ownsFont, open;
        private InputLayer oldLayer;
        private CursorLockMode oldLock;
        private bool oldCursorVisible, oldNavigation;
        private EventSystem editingSystem;
        private BaseInputModule editingModule;
        private BaseInput oldInput;
        private GameObject oldSelection;
        private float lastMessage = -100, lastSend = -100;
        private readonly Queue<string> lines = new Queue<string>();
        private ConfigEntry<float> displaySeconds;

        private void Awake()
        {
            Instance = this;
            Chat = new NetworkChat(Receive);
            displaySeconds = Config.Bind("Display", "VisibleSeconds", 12f, "Seconds to show recent messages when chat is closed.");
            harmony = new Harmony(Id);
            harmony.PatchAll();
            SceneManager.activeSceneChanged += SceneChanged;
            Logger.LogInfo("Text Chat 1.1.4 loaded (Mirror). Enter: chat, Enter: send, Escape: cancel.");
        }

        private void Update()
        {
            Chat.Tick();
            bool playing = NetworkChat.InSession;
            if (playing != sessionActive)
            {
                Close();
                sessionActive = playing;
                Logger.LogInfo(playing ? "Gameplay chat enabled." : "Gameplay chat disabled.");
                lines.Clear();
                if (history != null) history.text = "";
                if (playing) AddLine("Enter 키를 눌러 채팅하세요");
            }
            if (!playing)
            {
                Close();
                if (canvasObject != null) canvasObject.SetActive(false);
                return;
            }
            if (canvasObject == null) BuildUi();
            canvasObject.SetActive(true);
            var keyboard = Keyboard.current;
            if (!open && keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                GameObject selected = EventSystem.current?.currentSelectedGameObject;
                if (selected == null || (selected.GetComponent<TMP_InputField>() == null && selected.GetComponent<InputField>() == null)) Open();
            }
            if (open && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
            panelGroup.alpha = open || Time.unscaledTime - lastMessage < Mathf.Clamp(displaySeconds.Value, 1, 120) ? 1 : 0;
        }

        private void LateUpdate()
        {
            if (!open) return;
            // Scene transitions and forced game UI take precedence over an unfinished draft.
            if (EventSystem.current != editingSystem || (InputEvents.ActiveLayer != InputLayer.UI)) { Close(); return; }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Open()
        {
            if (!NetworkChat.InSession || InputEvents.ActiveLayer == InputLayer.Cutscene) return;
            EnsureEventSystem();
            editingSystem = EventSystem.current;
            editingModule = editingSystem.currentInputModule ?? editingSystem.GetComponent<BaseInputModule>();
            if (editingModule == null) return;
            oldInput = editingModule.inputOverride;
            editingModule.inputOverride = ime;
            oldSelection = editingSystem.currentSelectedGameObject;
            oldNavigation = editingSystem.sendNavigationEvents;
            editingSystem.sendNavigationEvents = false;
            oldLayer = InputEvents.ActiveLayer;
            oldLock = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            ResetGameplayInput();
            InputEvents.ActiveLayer = InputLayer.UI;
            open = true;
            inputObject.SetActive(true);
            hint.text = "ENTER  전송       ESC  취소";
            field.text = "";
            field.ResetSubmission();
            ime.ResetComposition();
            field.OpenedFrame = Time.frameCount;
            editingSystem.SetSelectedGameObject(inputObject);
            field.ActivateInputField();
            ime.imeCompositionMode = IMECompositionMode.On;
            Logger.LogInfo("Chat input opened.");
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close()
        {
            if (!open) return;
            open = false;
            field.ResetSubmission();
            field.DeactivateInputField();
            ime.ResetComposition();
            ime.imeCompositionMode = IMECompositionMode.Auto;
            WindowsIme.End();
            if (editingModule != null && editingModule.inputOverride == ime) editingModule.inputOverride = oldInput;
            if (editingSystem != null)
            {
                editingSystem.sendNavigationEvents = oldNavigation;
                if (editingSystem.currentSelectedGameObject == inputObject) editingSystem.SetSelectedGameObject(oldSelection);
            }
            if (InputEvents.ActiveLayer == InputLayer.UI) InputEvents.ActiveLayer = oldLayer;
            Cursor.lockState = oldLock;
            Cursor.visible = oldCursorVisible;
            inputObject.SetActive(false);
            Logger.LogInfo("Chat input closed.");
            hint.text = "ENTER  채팅";
        }

        private static void ResetGameplayInput()
        {
            InputEvents.OnMoveEvent?.Invoke(Vector2.zero);
            InputEvents.OnAimEvent?.Invoke(Vector2.zero);
            InputEvents.UpdateJump(false); InputEvents.UpdateCrouch(false); InputEvents.UpdateSprint(false);
            InputEvents.UpdateInteract(false); InputEvents.UpdateUseItem(false); InputEvents.UpdateThrowItem(false);
            InputEvents.UpdateZoom(false); InputEvents.UpdateEmoteWheel(false);
        }

        private void Send()
        {
            string text = ChatProtocol.Clean(field.text);
            if (text.Length == 0) { Close(); return; }
            if (Time.unscaledTime - lastSend < 0.8f) { hint.text = "잠시 기다려 주세요"; return; }
            try
            {
                if (!Chat.Send(text)) { hint.text = "방장도 Text Chat 1.1 이상이 필요합니다"; return; }
            }
            catch (System.Text.EncoderFallbackException) { hint.text = "입력을 확인해 주세요"; return; }
            lastSend = Time.unscaledTime;
            Logger.LogInfo("Chat message queued via Mirror (" + text.Length + " characters).");
            // Display only the host's broadcast, including on the sending client.
            Close();
        }

        private void Receive(string name, string text)
        {
            if (!NetworkChat.InSession) return;
            Logger.LogInfo("Chat broadcast received (" + text.Length + " characters).");
            AddLine((name.Length == 0 ? "Player" : name) + ": " + text);
        }

        private void AddLine(string line)
        {
            if (canvasObject == null) BuildUi();
            lines.Enqueue(line);
            while (lines.Count > 30) lines.Dequeue();
            history.text = string.Join("\n", lines);
            history.ForceMeshUpdate();
            lastMessage = Time.unscaledTime;
            panelGroup.alpha = 1;
        }

        private void BuildUi()
        {
            if (font == null)
            {
                var gameFont = GameChatTheme.FindFont();
                // Keep the game's font as the primary face. The local font only fills missing glyphs.
                string fontPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "malgun.ttf");
                if (System.IO.File.Exists(fontPath))
                {
                    hangulFont = TMP_FontAsset.CreateFontAsset(fontPath, 0, 32, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
                }
                if (hangulFont != null) hangulFont.isMultiAtlasTexturesEnabled = true;
                if (gameFont != null)
                {
                    // TMP destroys its atlas and material on disposal, so copy both as well.
                    // A static copy also prevents adding glyphs to the game's dynamic tables.
                    font = Instantiate(gameFont);
                    font.name = "TextChat_" + gameFont.name;
                    font.atlasPopulationMode = AtlasPopulationMode.Static;
                    font.atlasTextures = gameFont.atlasTextures.Select(t => t != null ? Instantiate(t) : null).ToArray();
                    font.material = Instantiate(gameFont.material);
                    font.material.mainTexture = font.atlasTextures[0];
                    font.fallbackFontAssetTable = new List<TMP_FontAsset>(gameFont.fallbackFontAssetTable ?? new List<TMP_FontAsset>());
                    if (hangulFont != null) font.fallbackFontAssetTable.Add(hangulFont);
                    font.ReadFontAssetDefinition();
                    ownsFont = true;
                }
                else font = hangulFont;
                if (font == null) throw new InvalidOperationException("No usable TextMeshPro font found.");
                Logger.LogInfo("Chat theme: game font=" + (gameFont != null ? gameFont.name : "unavailable") + ", plain panel geometry");
            }
            canvasObject = new GameObject("TextChatCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            panel = Rect("ChatPanel", canvasObject.transform);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(32, 92); rect.sizeDelta = new Vector2(540, 280);
            panel.GetComponent<Image>().color = Color.clear;
            panelGroup = panel.AddComponent<CanvasGroup>();
            var historyViewport = new GameObject("HistoryViewport", typeof(RectTransform), typeof(RectMask2D));
            historyViewport.transform.SetParent(panel.transform, false);
            var historyRect = historyViewport.GetComponent<RectTransform>();
            historyRect.anchorMin = historyRect.anchorMax = historyRect.pivot = Vector2.zero;
            historyRect.anchoredPosition = new Vector2(18, 76); historyRect.sizeDelta = new Vector2(504, 190);
            history = Text("History", historyViewport.transform, Vector2.zero, new Vector2(504, 190), 20);
            history.alignment = TextAlignmentOptions.BottomLeft;
            // Keep the newest lines at the bottom; the viewport clips the older lines above it.
            // Truncate instead retained the oldest lines and hid newly received messages.
            history.overflowMode = TextOverflowModes.Overflow;
            hint = Text("Hint", panel.transform, new Vector2(18, 12), new Vector2(504, 22), 14);
            hint.color = GameChatTheme.Paper; hint.text = "ENTER  채팅";
            inputObject = Rect("Input", panel.transform);
            var ir = inputObject.GetComponent<RectTransform>();
            ir.anchorMin = ir.anchorMax = ir.pivot = Vector2.zero;
            ir.anchoredPosition = new Vector2(16, 37); ir.sizeDelta = new Vector2(508, 34);
            inputObject.GetComponent<Image>().color = Color.clear;
            // Configure while inactive so TMP never enables with an incomplete viewport.
            inputObject.SetActive(false);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(inputObject.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = new Vector2(10, 0); vr.offsetMax = new Vector2(-10, 0);
            var inputText = Text("Text", viewport.transform, Vector2.zero, new Vector2(488, 34), 20);
            // Geometry/Midline alignment makes TMP's caret center around local Y=0.
            // Use normal vertical centering so it follows the actual glyph baseline.
            inputText.alignment = TextAlignmentOptions.Left;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            var inputTextRect = inputText.rectTransform;
            inputTextRect.anchorMin = Vector2.zero; inputTextRect.anchorMax = Vector2.one;
            inputTextRect.pivot = new Vector2(0.5f, 0.5f);
            inputTextRect.offsetMin = inputTextRect.offsetMax = Vector2.zero;
            field = inputObject.AddComponent<ChatInputField>();
            ime = canvasObject.AddComponent<ChatImeInput>();
            field.textViewport = vr; field.textComponent = inputText;
            field.targetGraphic = inputObject.GetComponent<Image>();
            field.fontAsset = font; field.richText = false;
            field.customCaretColor = true; field.caretColor = GameChatTheme.Gold;
            field.selectionColor = new Color(1, 0.86f, 0.32f, 0.3f);
            field.transition = Selectable.Transition.None;
            field.characterLimit = ChatProtocol.MaxCharacters;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.Ime = ime; field.SubmitChat = Send; field.CancelChat = Close;
            var navigation = field.navigation; navigation.mode = Navigation.Mode.None; field.navigation = navigation;
            Logger.LogInfo("Chat UI created.");
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<Image>().raycastTarget = false;
            return obj;
        }

        private TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = fontSize; text.color = GameChatTheme.Paper;
            text.richText = false; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            ownEventSystem = new GameObject("TextChatEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(ownEventSystem);
            ownEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void SceneChanged(Scene before, Scene after)
        {
            Close();
            if (canvasObject != null) canvasObject.SetActive(false);
            if (ownEventSystem != null) Destroy(ownEventSystem);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Close(); }
        private void OnDisable() { Close(); }
        private void OnDestroy()
        {
            Close();
            SceneManager.activeSceneChanged -= SceneChanged;
            Chat?.Dispose(); harmony?.UnpatchSelf();
            if (canvasObject != null) Destroy(canvasObject);
            if (ownEventSystem != null) Destroy(ownEventSystem);
            if (ownsFont && font != null) Destroy(font);
            if (hangulFont != null) Destroy(hangulFont);
            Instance = null;
        }
    }

    [HarmonyPatch]
    internal static class BlockGameplayWhileTyping
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(InputReader)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name.StartsWith("On") && method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType == typeof(InputAction.CallbackContext));
        private static bool Prefix() => !Plugin.IsTyping;
    }
}




