using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
// using DG.Tweening;
using Ploup.Managers.Player;
using Ploup.Models.Maps;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ploup.Components.Maps
{
    public class BorderComponent : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private SpriteRenderer _spriteRenderer;
        [SerializeField] private Direction direction;
        
        void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Update is called once per frame
        void Update()
        {
        
        }
        
        public void OnPointerEnter(PointerEventData eventData)
        {
            PlayedCharacterManager.Instance.SetHandCursor();

            var currentColor = _spriteRenderer.color;
            // DOTween.To(() => _spriteRenderer.color, x => _spriteRenderer.color = x, new Color(currentColor.r, currentColor.g, currentColor.b, 0.5f), 0.5f);
            _spriteRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0.5f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var currentColor = _spriteRenderer.color;
            // DOTween.To(() => _spriteRenderer.color, x => _spriteRenderer.color = x, new Color(currentColor.r, currentColor.g, currentColor.b, 0f), 0.5f);
            _spriteRenderer.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                PlayedCharacterManager.Instance.MoveToBorder(direction).Forget();
            }
        }
    }
}
