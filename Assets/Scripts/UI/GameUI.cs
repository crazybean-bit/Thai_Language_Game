using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThaiMystery
{
    // Builds the whole HUD in code and routes clicks between the dialogue box,
    // the journal and the guess popup.
    public class GameUI : MonoBehaviour
    {
        public DialogueUI DialogueBox { get; private set; }
        public JournalUI Journal { get; private set; }
        public GuessPopup Popup { get; private set; }

        public bool ModalOpen => Journal.IsOpen || Popup.IsOpen;
        public bool IsFading { get; private set; }

        private TextMeshProUGUI toast;
        private CanvasGroup toastGroup;
        private Image fade;
        private Coroutine toastRoutine;

        public void Build()
        {
            EnsureEventSystem();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var help = UIFactory.Text("Controls", transform,
                "Move: WASD / Arrows   Inspect: Space / Z   Journal: J / Tab   Click a Thai word to guess it",
                16, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.TopLeft);
            UIFactory.Place(help.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -10), new Vector2(1000, 24));

            DialogueBox = new DialogueUI(transform, () => Game.Dialogue.OnBoxClicked(), OpenGuess);

            toast = UIFactory.Text("Toast", transform, "", 26, Theme.Paper);
            toast.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(900, 80));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0;

            Journal = new JournalUI(transform, OpenGuess);
            Popup = new GuessPopup(transform);

            fade = UIFactory.Panel("Fade", transform, Color.black, false);
            UIFactory.Stretch(fade.rectTransform);

            Game.Words.Changed += OnWordsChanged;
            Game.Words.GroupConfirmed += OnGroupConfirmed;
        }

        private void OnDestroy()
        {
            if (Game.Words == null) return;
            Game.Words.Changed -= OnWordsChanged;
            Game.Words.GroupConfirmed -= OnGroupConfirmed;
        }

        private void Update()
        {
            if (Popup.IsOpen)
            {
                if (GameInput.Escape()) Popup.Close();
                return;
            }
            if (Game.InputConsumed || IsFading) return;

            if (GameInput.Journal() || (Journal.IsOpen && GameInput.Cancel()))
            {
                Journal.Toggle();
                Game.ConsumeInputThisFrame();
            }
        }

        private void OpenGuess(string thai) => Popup.Open(thai);

        private void OnWordsChanged()
        {
            DialogueBox.RefreshGuesses();
            Journal.Rebuild();
        }

        private void OnGroupConfirmed(List<string> words)
        {
            var parts = new List<string>();
            foreach (var w in words) parts.Add($"{w} = {Game.Words.GetGuess(w)}");
            ShowToast("<color=#9BE3A8>Something clicks.</color>  " + string.Join("   ", parts));
        }

        public void ShowToast(string message)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            toast.text = message;
            for (float t = 0; t < 1; t += Time.deltaTime * 4) { toastGroup.alpha = t; yield return null; }
            toastGroup.alpha = 1;
            yield return new WaitForSeconds(3.5f);
            for (float t = 1; t > 0; t -= Time.deltaTime * 2) { toastGroup.alpha = t; yield return null; }
            toastGroup.alpha = 0;
        }

        public IEnumerator FadeIn(float seconds)
        {
            IsFading = true;
            fade.gameObject.SetActive(true);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                fade.color = new Color(0, 0, 0, 1 - t / seconds);
                yield return null;
            }
            fade.gameObject.SetActive(false);
            IsFading = false;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
