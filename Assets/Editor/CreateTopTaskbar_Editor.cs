#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CreateTopTaskbar_Editor
{
    [MenuItem("Galactic Fishing/UI/Create Top Taskbar (Auto-Wired)")]
    public static void Create()
    {
        // Find a Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (!canvas)
        {
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");
        }

        // Create TopTaskbarPanel root
        var root = new GameObject("TopTaskbarPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(TopTaskbarController));
        Undo.RegisterCreatedObjectUndo(root, "Create Top Taskbar");
        root.transform.SetParent(canvas.transform, false);

        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(1f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.sizeDelta = new Vector2(0f, 96f);
        rootRt.anchoredPosition = Vector2.zero;

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.35f);

        // Horizontal layout container
        var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        Undo.RegisterCreatedObjectUndo(row, "Create Row");
        row.transform.SetParent(root.transform, false);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;
        rowRt.offsetMin = new Vector2(16f, 10f);
        rowRt.offsetMax = new Vector2(-16f, -10f);

        var hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.spacing = 12f;

        // LeftInfoBlock
        var left = CreateBlock(row.transform, "LeftInfoBlock");
        AddVLG(left, TextAnchor.UpperLeft);

        CreateTMP(left.transform, "LakeNameText", "Lake: —");
        CreateTMP(left.transform, "CreditsText", "Credits: —");
        CreateTMP(left.transform, "ExtraInfoText", "—");

        // CenterButtons
        var center = CreateBlock(row.transform, "CenterButtons");
        var centerHLG = center.gameObject.AddComponent<HorizontalLayoutGroup>();
        centerHLG.childControlHeight = true;
        centerHLG.childControlWidth = true;
        centerHLG.childForceExpandWidth = false;
        centerHLG.childForceExpandHeight = false;
        centerHLG.spacing = 10f;
        centerHLG.childAlignment = TextAnchor.MiddleCenter;

        // RightInfoBlock
        var right = CreateBlock(row.transform, "RightInfoBlock");
        AddVLG(right, TextAnchor.UpperRight);
        CreateTMP(right.transform, "InfoRight1", "—");
        CreateTMP(right.transform, "InfoRight2", "—");

        // PinToggle (small)
        var pin = CreateBlock(root.transform, "PinToggle");
        var pinRt = pin.GetComponent<RectTransform>();
        pinRt.anchorMin = new Vector2(1f, 1f);
        pinRt.anchorMax = new Vector2(1f, 1f);
        pinRt.pivot = new Vector2(1f, 1f);
        pinRt.sizeDelta = new Vector2(140f, 34f);
        pinRt.anchoredPosition = new Vector2(-18f, -12f);

        var toggle = CreateToggle(pin.transform, "PinToggle", "Pin");

        // Buttons
        var mainHubBtn = CreateButton(center.transform, "MainHubButton", "Main Hub");
        var questionBtn = CreateButton(center.transform, "QuestionButton", "?");

        // Dropdown
        var dropdownGO = new GameObject("DropdownPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(TaskbarMenuDropdown));
        Undo.RegisterCreatedObjectUndo(dropdownGO, "Create Dropdown");
        dropdownGO.transform.SetParent(root.transform, false);
        var ddRt = dropdownGO.GetComponent<RectTransform>();
        ddRt.anchorMin = new Vector2(0.5f, 1f);
        ddRt.anchorMax = new Vector2(0.5f, 1f);
        ddRt.pivot = new Vector2(0.5f, 1f);
        ddRt.sizeDelta = new Vector2(320f, 10f);
        ddRt.anchoredPosition = new Vector2(0f, -96f);

        var ddImg = dropdownGO.GetComponent<Image>();
        ddImg.color = new Color(0f, 0f, 0f, 0.5f);

        var ddVlg = dropdownGO.GetComponent<VerticalLayoutGroup>();
        ddVlg.childControlWidth = true;
        ddVlg.childControlHeight = true;
        ddVlg.childForceExpandWidth = true;
        ddVlg.childForceExpandHeight = false;
        ddVlg.spacing = 6f;
        ddVlg.padding = new RectOffset(10, 10, 10, 10);

        var ddFitter = dropdownGO.GetComponent<ContentSizeFitter>();
        ddFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ddFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        dropdownGO.SetActive(false);

        // Create a simple entryButtonPrefab inside dropdown (as disabled template)
        var prefabBtn = CreateButton(dropdownGO.transform, "EntryButtonPrefab", "Menu Item");
        prefabBtn.gameObject.SetActive(false);

        // Wire controllers
        var controller = root.GetComponent<TopTaskbarController>();
        var dropdown = dropdownGO.GetComponent<TaskbarMenuDropdown>();

        // Try auto-find MenuRouter & FullscreenHubController in the scene
        var menuRouter = Object.FindFirstObjectByType<MenuRouter>();
        var hubController = Object.FindFirstObjectByType<GalacticFishing.UI.FullscreenHubController>();

        // Assign fields via SerializedObject (safe in editor)
        var so = new SerializedObject(controller);
        so.FindProperty("barRect").objectReferenceValue = rootRt;
        so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        so.FindProperty("mainHubButton").objectReferenceValue = mainHubBtn;
        so.FindProperty("questionButton").objectReferenceValue = questionBtn;
        so.FindProperty("pinToggle").objectReferenceValue = toggle;
        so.FindProperty("dropdownRoot").objectReferenceValue = ddRt;
        so.FindProperty("dropdown").objectReferenceValue = dropdown;
        so.FindProperty("menuRouter").objectReferenceValue = menuRouter;
        so.FindProperty("hubController").objectReferenceValue = hubController;
        so.ApplyModifiedPropertiesWithoutUndo();

        var soDd = new SerializedObject(dropdown);
        soDd.FindProperty("contentRoot").objectReferenceValue = ddRt;
        soDd.FindProperty("entryButtonPrefab").objectReferenceValue = prefabBtn;
        soDd.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[GF] Top taskbar created and auto-wired. Style it (fonts/sprites/padding) to match your UI.");
    }

    private static RectTransform CreateBlock(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create UI Block");
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static void AddVLG(RectTransform rt, TextAnchor align)
    {
        var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2f;
        vlg.childAlignment = align;
    }

    private static TMP_Text CreateTMP(Transform parent, string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create TMP");
        go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22;
        tmp.color = Color.white;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 26f);
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(go, "Create Button");
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.08f);

        var btn = go.GetComponent<Button>();

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150f, 48f);

        // Text
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textGo, "Create Button Text");
        textGo.transform.SetParent(go.transform, false);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 26;
        tmp.color = Color.white;

        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return btn;
    }

    private static Toggle CreateToggle(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        Undo.RegisterCreatedObjectUndo(go, "Create Toggle");
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(140f, 34f);

        // Background
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(bgGo, "Create Toggle Background");
        bgGo.transform.SetParent(go.transform, false);
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.10f);

        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.sizeDelta = new Vector2(22f, 22f);
        bgRt.anchoredPosition = new Vector2(0f, 0f);

        // Checkmark
        var ckGo = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(ckGo, "Create Toggle Checkmark");
        ckGo.transform.SetParent(bgGo.transform, false);
        var ckImg = ckGo.GetComponent<Image>();
        ckImg.color = new Color(1f, 1f, 1f, 0.85f);

        var ckRt = ckGo.GetComponent<RectTransform>();
        ckRt.anchorMin = Vector2.zero;
        ckRt.anchorMax = Vector2.one;
        ckRt.offsetMin = new Vector2(4f, 4f);
        ckRt.offsetMax = new Vector2(-4f, -4f);

        // Label
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(labelGo, "Create Toggle Label");
        labelGo.transform.SetParent(go.transform, false);
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;

        var lr = labelGo.GetComponent<RectTransform>();
        lr.anchorMin = new Vector2(0f, 0f);
        lr.anchorMax = new Vector2(1f, 1f);
        lr.offsetMin = new Vector2(28f, 0f);
        lr.offsetMax = new Vector2(0f, 0f);

        // Wire toggle graphics
        var t = go.GetComponent<Toggle>();
        t.targetGraphic = bgImg;
        t.graphic = ckImg;

        return t;
    }
}
#endif
