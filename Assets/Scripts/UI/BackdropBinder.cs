using System;
using System.Collections.Generic;
using UnityEngine;

public class BackdropBinder : MonoBehaviour
{
    [Serializable]
    public class WatchRef
    {
        public GameObject root;        // the UI root you open/close
        public CanvasGroup canvasGroup; // optional; if null we only check activeSelf
    }

    [SerializeField] private List<WatchRef> watch = new();
    [SerializeField] private GameObject backdrop;   // assign UI_Backdrop
    [SerializeField] private bool sendToBack = true;

    bool last;

    void Awake()
    {
        if (backdrop && sendToBack) backdrop.transform.SetAsFirstSibling();
        if (backdrop) backdrop.SetActive(false);
    }

    void LateUpdate()
    {
        bool show = false;
        for (int i = 0; i < watch.Count; i++)
        {
            var w = watch[i];
            if (!w.root) continue;
            bool openByActive = w.root.activeInHierarchy;
            bool openByAlpha  = (!w.canvasGroup || w.canvasGroup.alpha > 0.001f);
            if (openByActive && openByAlpha) { show = true; break; }
        }

        if (backdrop && last != show)
        {
            backdrop.SetActive(show);
            last = show;
        }
    }
}
