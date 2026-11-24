using UnityEngine;
using UnityEngine.EventSystems;

namespace GravityRoguelike.UI
{
    /// <summary>Pointer input stays on the fixed card slot; only its visual child is animated.</summary>
    public class CharacterSelectCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private CharacterSelectUI owner;
        private int index;
        public void Bind(CharacterSelectUI ui, int cardIndex) { owner = ui; index = cardIndex; }
        public void OnPointerEnter(PointerEventData data) => owner.Highlight(index);
        public void OnPointerExit(PointerEventData data) => owner.ClearHighlight(index);
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) owner.Confirm(index);
        }
    }
}
