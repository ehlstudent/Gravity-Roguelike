using GravityRoguelike.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GravityRoguelike.UI
{
    /// <summary>Self-contained overlay. Builds once, then animates cached views using unscaled time.</summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public class CharacterSelectUI : MonoBehaviour
    {
        [SerializeField] private ClassDefinition[] classes;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite tablet;
        [SerializeField] private Sprite frame;
        [SerializeField] private Sprite rune;
        [SerializeField] private Sprite vignette;
        [SerializeField] private Sprite ember;
        [Header("Layout")]
        [SerializeField] private Vector2 cardSize = new Vector2(400, 600);
        [SerializeField] private float cardGap = 48;
        [SerializeField, Range(0, 1)] private float overlayOpacity = 0.5f;
        [Header("Hover")]
        [SerializeField] private float hoverScale = 1.12f;
        [SerializeField] private float otherScale = 0.94f;
        [SerializeField] private float hoverOffset = 22;
        [SerializeField] private float otherOffset = -18;
        [SerializeField, Range(0, 1)] private float otherDim = 0.48f;
        [SerializeField, Range(0, 1)] private float otherDesaturation = 0.25f;
        [SerializeField, Min(0.1f)] private float animationSpeed = 12;
        [Header("Confirmation")]
        [SerializeField, Min(0.01f)] private float pressDuration = 0.18f;
        [SerializeField] private float pressPulse = 0.055f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.32f;
        [Header("Optional")]
        [SerializeField] private bool keyboardNavigation = true;
        [SerializeField] private AudioSource hoverAudio;
        [SerializeField] private AudioSource confirmAudio;

        private sealed class CardView
        {
            public RectTransform Slot, Visual;
            public Canvas Canvas;
            public CanvasGroup Group;
            public Image Frame, Veil;
        }
        private CardView[] cards;
        private RectTransform[] dust;
        private CanvasGroup rootGroup;
        private TMP_Text title, subtitle, hint;
        private int highlighted = -1;
        private int confirmed = -1;
        private float confirmationTime;
        private bool selecting;
        private Vector2 lastPointer;
        private bool keyboardOwnsHighlight;
        private static readonly Color Gold = new Color(0.9f, 0.76f, 0.47f);
        public bool IsSelecting => selecting && confirmed < 0;

        private void Awake() => Build();

        private void Build()
        {
            if (cards != null) return;
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            rootGroup = gameObject.AddComponent<CanvasGroup>();
            if (EventSystem.current == null)
            {
                var input = new GameObject("Selection EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                input.transform.SetParent(transform, false);
                input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Image overlay = Picture("Dim overlay", transform, null, new Color(0.018f, 0.012f, 0.03f, overlayOpacity));
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;
            Image edge = Picture("Vignette", transform, vignette, new Color(0, 0, 0, 0.55f));
            Stretch(edge.rectTransform);
            title = Text("Choose Your Path", transform, new Vector2(0, 434), new Vector2(1500, 80), 54, Gold);
            title.characterSpacing = 7;
            subtitle = Text("THREE DISCIPLINES  /  ONE DESCENT INTO THE VOID", transform,
                new Vector2(0, 373), new Vector2(1500, 40), 18, new Color(0.7f, 0.65f, 0.54f));
            subtitle.characterSpacing = 3;
            hint = Text("CHOOSE A CLASS TO BEGIN  |  ARROW KEYS + ENTER", transform,
                new Vector2(0, -418), new Vector2(1600, 50), 19, Gold);
            cards = new CardView[classes.Length];
            for (int i = 0; i < cards.Length; i++) BuildCard(i);
            RectTransform dustLayer = Rect("Ember layer", transform, Vector2.zero);
            Canvas dustCanvas = dustLayer.gameObject.AddComponent<Canvas>();
            dustCanvas.overrideSorting = true;
            dustCanvas.sortingOrder = 125;
            dust = new RectTransform[22];
            for (int i = 0; i < dust.Length; i++)
            {
                Image mote = Picture("Ember", dustLayer, ember, new Color(0.9f, 0.67f, 0.3f, 0.16f));
                mote.rectTransform.sizeDelta = Vector2.one * (5 + i % 4 * 2);
                dust[i] = mote.rectTransform;
            }
            if (Mouse.current != null) lastPointer = Mouse.current.position.ReadValue();
        }

        private void BuildCard(int index)
        {
            ClassDefinition data = classes[index];
            var view = new CardView();
            cards[index] = view;
            view.Slot = Rect(data.DisplayName + " slot", transform, cardSize);
            view.Slot.anchoredPosition = new Vector2((index - (cards.Length - 1) * 0.5f) * (cardSize.x + cardGap), -10);
            view.Canvas = view.Slot.gameObject.AddComponent<Canvas>();
            view.Canvas.overrideSorting = true;
            view.Canvas.sortingOrder = 101 + index;
            view.Slot.gameObject.AddComponent<GraphicRaycaster>();
            view.Slot.gameObject.AddComponent<CharacterSelectCard>().Bind(this, index);
            view.Visual = Rect("Animated tablet", view.Slot, cardSize);
            view.Group = view.Visual.gameObject.AddComponent<CanvasGroup>();
            Image stone = Picture("Weathered stone", view.Visual, tablet, Color.white);
            Stretch(stone.rectTransform);
            stone.type = Image.Type.Sliced;
            stone.raycastTarget = true;
            view.Frame = Picture("Bronze rune frame", view.Visual, frame, Gold);
            Stretch(view.Frame.rectTransform);
            view.Frame.type = Image.Type.Sliced;
            Image portrait = Picture("Idle portrait", view.Visual, data.Portrait, Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.sizeDelta = new Vector2(220, 220);
            portrait.rectTransform.anchoredPosition = new Vector2(0, 148);
            view.Veil = Picture("Portrait desaturation", portrait.transform, null, Color.clear);
            Stretch(view.Veil.rectTransform);
            TMP_Text name = Text(data.DisplayName.ToUpperInvariant(), view.Visual, new Vector2(0, -5),
                new Vector2(cardSize.x - 50, 65), 36, data.AccentColor);
            name.characterSpacing = 4;
            Image divider = Picture("Runic divider", view.Visual, rune, Gold);
            divider.rectTransform.anchoredPosition = new Vector2(0, -47);
            divider.rectTransform.sizeDelta = new Vector2(18, 24);
            Text(data.Description, view.Visual, new Vector2(0, -123), new Vector2(cardSize.x - 50, 118), 20,
                new Color(0.89f, 0.87f, 0.81f));
            TMP_Text label = Text("DIFFICULTY", view.Visual, new Vector2(0, -216), new Vector2(300, 26), 15, Gold);
            label.characterSpacing = 4;
            for (int i = 0; i < 5; i++)
            {
                Image gem = Picture("Difficulty " + (i + 1), view.Visual, rune,
                    i < data.Difficulty ? data.AccentColor : new Color(0.25f, 0.23f, 0.22f));
                gem.rectTransform.anchoredPosition = new Vector2((i - 2) * 38, -257);
                gem.rectTransform.sizeDelta = new Vector2(24, 32);
            }
        }

        public void ShowLoading()
        {
            gameObject.SetActive(true);
            Build();
            selecting = false;
            confirmed = highlighted = -1;
            rootGroup.alpha = 1;
            rootGroup.blocksRaycasts = true;
            foreach (CardView view in cards) view.Slot.gameObject.SetActive(false);
            title.text = "Shaping the Void";
            subtitle.text = "GENERATING AND VALIDATING THE ROOM ROUTES";
            hint.text = "The map is created before you choose a class.";
        }

        public void ShowFailure(string message)
        {
            ShowLoading();
            title.text = "The Path Remains Closed";
            hint.text = message;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            Build();
            selecting = true;
            confirmed = highlighted = -1;
            keyboardOwnsHighlight = false;
            rootGroup.alpha = 1;
            rootGroup.blocksRaycasts = true;
            title.text = "Choose Your Path";
            subtitle.text = "THREE DISCIPLINES  /  ONE DESCENT INTO THE VOID";
            hint.text = "CHOOSE A CLASS TO BEGIN  |  ARROW KEYS + ENTER";
            foreach (CardView view in cards)
            {
                view.Slot.gameObject.SetActive(true);
                view.Visual.anchoredPosition = Vector2.zero;
                view.Visual.localScale = Vector3.one;
                view.Group.alpha = 1;
                view.Frame.color = Gold;
                view.Veil.color = Color.clear;
            }
        }

        public void Highlight(int index)
        {
            if (!IsSelecting || index < 0 || index >= cards.Length) return;
            keyboardOwnsHighlight = false;
            SetHighlight(index);
        }

        public void ClearHighlight(int index)
        {
            if (IsSelecting && !keyboardOwnsHighlight && highlighted == index) SetHighlight(-1);
        }

        private void SetHighlight(int index)
        {
            if (highlighted == index) return;
            highlighted = index;
            if (index >= 0 && hoverAudio != null) hoverAudio.Play();
        }

        public void Confirm(int index)
        {
            if (!IsSelecting || index < 0 || index >= cards.Length) return;
            confirmed = highlighted = index;
            confirmationTime = Time.unscaledTime;
            rootGroup.blocksRaycasts = false;
            if (confirmAudio != null) confirmAudio.Play();
        }

        private void Update()
        {
            if (cards == null) return;
            if (IsSelecting && keyboardNavigation && Keyboard.current != null)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    int direction = keyboard.leftArrowKey.wasPressedThisFrame ? -1 : 1;
                    SetHighlight(highlighted < 0 ? 0 : (highlighted + direction + cards.Length) % cards.Length);
                    keyboardOwnsHighlight = true;
                }
                if (highlighted >= 0 && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) Confirm(highlighted);
            }
            if (IsSelecting && Mouse.current != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                if (keyboardOwnsHighlight && (pointer - lastPointer).sqrMagnitude > 1)
                {
                    keyboardOwnsHighlight = false;
                    int underPointer = -1;
                    for (int i = 0; i < cards.Length; i++)
                        if (RectTransformUtility.RectangleContainsScreenPoint(cards[i].Visual, pointer)) underPointer = i;
                    SetHighlight(underPointer);
                }
                lastPointer = pointer;
            }
            float blend = 1 - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
            float elapsed = Time.unscaledTime - confirmationTime;
            float pulse = confirmed >= 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / pressDuration)) : 0;
            for (int i = 0; i < cards.Length; i++)
            {
                CardView view = cards[i];
                bool chosen = highlighted == i;
                bool dim = highlighted >= 0 && !chosen;
                float scale = chosen ? hoverScale + pulse * pressPulse : dim ? otherScale : 1;
                view.Visual.localScale = Vector3.Lerp(view.Visual.localScale, Vector3.one * scale, blend);
                view.Visual.anchoredPosition = Vector2.Lerp(view.Visual.anchoredPosition,
                    new Vector2(0, chosen ? hoverOffset : dim ? otherOffset : 0), blend);
                view.Group.alpha = Mathf.Lerp(view.Group.alpha, dim ? (confirmed >= 0 ? 0.08f : otherDim) : 1, blend);
                view.Veil.color = Color.Lerp(view.Veil.color, new Color(0.35f, 0.35f, 0.35f, dim ? otherDesaturation : 0), blend);
                Color color = chosen ? Color.Lerp(classes[i].AccentColor, Color.white, pulse) : Gold;
                view.Frame.color = Color.Lerp(view.Frame.color, color, blend);
                view.Canvas.sortingOrder = chosen ? 120 : 101 + i;
            }
            for (int i = 0; i < dust.Length; i++)
                dust[i].anchoredPosition = new Vector2(Mathf.Sin(i * 8.3f + Time.unscaledTime * 0.08f) * 675,
                    Mathf.Repeat(i * 113 + Time.unscaledTime * (8 + i % 5), 800) - 400);
            if (confirmed < 0) return;
            rootGroup.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - pressDuration) / fadeDuration));
            if (elapsed < pressDuration + fadeDuration) return;
            ClassDefinition choice = classes[confirmed];
            selecting = false;
            gameObject.SetActive(false);
            SelectedClass.Confirm(choice);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite, Color color)
        {
            Image image = Rect(name, parent, Vector2.zero).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Text(string value, Transform parent, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var text = Rect("Text", parent, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.rectTransform.anchoredPosition = position;
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
