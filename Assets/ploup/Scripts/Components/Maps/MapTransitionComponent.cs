using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
// using DG.Tweening;
using UnityEngine;

namespace Ploup.Components.Maps
{
    public class MapTransitionComponent : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        public static MapTransitionComponent Instance { get; private set; }

        public void Awake()
        {
            Instance = this;
        }

        // Start is called before the first frame update
        void Start()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Update is called once per frame
        void Update()
        {
        }

        public async UniTask StartTransition()
        {
            // var tween = DOTween.To(() => _spriteRenderer.color.a, x => _spriteRenderer.color = new Color(_spriteRenderer.color.r, _spriteRenderer.color.g, _spriteRenderer.color.b, x), 1f, 0.25f);
            // await UniTask.WaitUntil(() => tween.IsComplete());
            _spriteRenderer.color = new Color(_spriteRenderer.color.r, _spriteRenderer.color.g, _spriteRenderer.color.b, 1f);
            await UniTask.Delay(250); // 0.25 seconds
        }

        public async UniTask EndTransition()
        {
            // var tween = DOTween.To(() => _spriteRenderer.color.a, x => _spriteRenderer.color = new Color(_spriteRenderer.color.r, _spriteRenderer.color.g, _spriteRenderer.color.b, x), 0f, 0.5f);
            // await UniTask.WaitUntil(() => tween.IsComplete());
            _spriteRenderer.color = new Color(_spriteRenderer.color.r, _spriteRenderer.color.g, _spriteRenderer.color.b, 0f);
            await UniTask.Delay(500); // 0.5 seconds
        }
    }
}