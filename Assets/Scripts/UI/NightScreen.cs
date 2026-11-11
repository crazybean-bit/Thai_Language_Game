using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ThaiMystery
{
    // The screen between one day and the next: black, with the end-of-day card (what he
    // heard today, which words faded) and, during a memory flash, a pale washed-out light.
    // It sits under the dialogue box, so night dialogues and flashes play on top of it.
    public class NightScreen
    {
        private static readonly Color FlashColor = new Color32(0xD8, 0xCF, 0xB8, 0xFF);

        private readonly GameObject root;
        private readonly Image flash;
        private readonly GameObject card;
        private readonly TextMeshProUGUI title;
        private readonly TextMeshProUGUI body;
        private readonly TextMeshProUGUI hint;
        private readonly MonoBehaviour host;
        private Coroutine flashFade;

        public bool IsOpen => root.activeSelf;

        public NightScreen(Transform canvas, MonoBehaviour coroutineHost)
        {
            host = coroutineHost;
            var bg = UIFactory.Panel("Night", canvas, Color.black);
            UIFactory.Stretch(bg.rectTransform);
            root = bg.gameObject;

            flash = UIFactory.Panel("Memory Flash", root.transform, FlashColor, false);
            UIFactory.Stretch(flash.rectTransform);
            flash.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0f);

            card = UIFactory.Rect("Card", root.transform).gameObject;
            var cardRt = (RectTransform)card.transform;
            UIFactory.Place(cardRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 420));

            title = UIFactory.Text("Title", cardRt, "", 44, Theme.Paper);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(900, 60));

            body = UIFactory.Text("Body", cardRt, "", 26, new Color(Theme.Paper.r, Theme.Paper.g, Theme.Paper.b, 0.85f), TextAlignmentOptions.Top);
            body.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Place(body.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(860, 300));

            hint = UIFactory.Text("Hint", cardRt, "Space to continue", 18, new Color(1, 1, 1, 0.4f));
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -40), new Vector2(600, 30));

            card.SetActive(false);
            root.SetActive(false);
        }

        public void Show()
        {
            root.SetActive(true);
            card.SetActive(false);
            SetFlashImmediate(0f);
        }

        public void Hide()
        {
            card.SetActive(false);
            SetFlashImmediate(0f);
            root.SetActive(false);
        }

        public void ShowCard(string heading, string text)
        {
            title.text = heading;
            body.text = text;
            card.SetActive(true);
        }

        public void HideCard() => card.SetActive(false);

        // A memory flash washes the black out to a pale, overexposed light.
        public void SetFlash(bool on)
        {
            if (!IsOpen) return;
            if (flashFade != null) host.StopCoroutine(flashFade);
            flashFade = host.StartCoroutine(FadeFlash(on ? 0.92f : 0f, on ? 1.2f : 0.6f));
        }

        private IEnumerator FadeFlash(float target, float seconds)
        {
            float start = flash.color.a;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                SetFlashImmediate(Mathf.Lerp(start, target, t / seconds));
                yield return null;
            }
            SetFlashImmediate(target);
            flashFade = null;
        }

        private void SetFlashImmediate(float alpha) =>
            flash.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, alpha);
    }
}
