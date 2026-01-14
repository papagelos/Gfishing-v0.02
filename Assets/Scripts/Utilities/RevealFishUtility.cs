using UnityEngine;
using UnityEngine.UI;

namespace GalacticFishing.UI
{
    public static class RevealFishUtility
    {
        public static void RevealNow()
        {
            var fx = Object.FindObjectOfType<BeepFishFX>(true);
            if (!fx) return;

            fx.StopAllCoroutines();

            foreach (var g in fx.GetComponentsInChildren<Graphic>(true))
            {
                var c = g.color; c.a = 1f; g.color = c;
            }

            foreach (var sr in fx.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var c = sr.color; c.a = 1f; sr.color = c;
            }
        }
    }
}
