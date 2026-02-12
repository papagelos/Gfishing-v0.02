#if UNITY_EDITOR
using GalacticFishing.Minigames.HexWorld;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProceduralSceneSetupUtility
{
    [MenuItem("Tools/Galactic Fishing/Bootstrap Procedural Scene")]
    public static void Bootstrap()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[Bootstrap] No active scene to configure.");
            return;
        }

        bool changed = false;

        Camera camera = EnsureMainCamera(scene, ref changed);
        EnsureDirectionalLight(scene, ref changed);
        EnsureNamedRoot(scene, "Tiles", ref changed);
        EnsureNamedRoot(scene, "Props", ref changed);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Bootstrap] ProcGen Scene configuration verified and updated.");
        }
        else
        {
            Debug.Log("[Bootstrap] ProcGen Scene already configured.");
        }

        Selection.activeObject = camera != null ? camera.gameObject : null;
    }

    private static Camera EnsureMainCamera(Scene scene, ref bool changed)
    {
        Camera camera = FindTaggedMainCamera(scene);

        if (camera == null)
        {
            GameObject cameraGo = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
            Undo.RegisterCreatedObjectUndo(cameraGo, "Create Main Camera");
            cameraGo.tag = "MainCamera";

            cameraGo.transform.position = new Vector3(0f, 10f, -10f);
            cameraGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            camera = Undo.AddComponent<Camera>(cameraGo);
            Undo.AddComponent<AudioListener>(cameraGo);
            changed = true;
        }

        if (!camera.orthographic)
        {
            Undo.RecordObject(camera, "Set Camera Orthographic");
            camera.orthographic = true;
            changed = true;
        }

        if (!Mathf.Approximately(camera.orthographicSize, 10f))
        {
            Undo.RecordObject(camera, "Set Camera Orthographic Size");
            camera.orthographicSize = 10f;
            changed = true;
        }

        if (!camera.CompareTag("MainCamera"))
        {
            Undo.RecordObject(camera.gameObject, "Tag Main Camera");
            camera.gameObject.tag = "MainCamera";
            changed = true;
        }

        if (camera.GetComponent<HexCameraPanZoom3D>() == null)
        {
            Undo.AddComponent<HexCameraPanZoom3D>(camera.gameObject);
            changed = true;
        }

        return camera;
    }

    private static void EnsureDirectionalLight(Scene scene, ref bool changed)
    {
        Light directional = FindDirectionalLight(scene);

        if (directional == null)
        {
            GameObject lightGo = new GameObject("Directional Light");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            Undo.RegisterCreatedObjectUndo(lightGo, "Create Directional Light");
            directional = Undo.AddComponent<Light>(lightGo);
            directional.type = LightType.Directional;
            changed = true;
        }

        Transform tr = directional.transform;
        Quaternion targetRot = Quaternion.Euler(50f, -30f, 0f);
        if (tr.rotation != targetRot)
        {
            Undo.RecordObject(tr, "Set Directional Light Rotation");
            tr.rotation = targetRot;
            changed = true;
        }

        if (directional.type != LightType.Directional)
        {
            Undo.RecordObject(directional, "Set Directional Light Type");
            directional.type = LightType.Directional;
            changed = true;
        }

        if (directional.shadows != LightShadows.Soft)
        {
            Undo.RecordObject(directional, "Enable Soft Shadows");
            directional.shadows = LightShadows.Soft;
            changed = true;
        }
    }

    private static void EnsureNamedRoot(Scene scene, string name, ref bool changed)
    {
        if (FindObjectByName(scene, name) != null)
            return;

        GameObject go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name} Root");
        changed = true;
    }

    private static Camera FindTaggedMainCamera(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Camera[] cameras = roots[i].GetComponentsInChildren<Camera>(true);
            for (int j = 0; j < cameras.Length; j++)
            {
                Camera cam = cameras[j];
                if (cam != null && cam.CompareTag("MainCamera"))
                    return cam;
            }
        }

        return null;
    }

    private static Light FindDirectionalLight(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Light[] lights = roots[i].GetComponentsInChildren<Light>(true);
            for (int j = 0; j < lights.Length; j++)
            {
                Light light = lights[j];
                if (light != null && light.type == LightType.Directional)
                    return light;
            }
        }

        return null;
    }

    private static GameObject FindObjectByName(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindByNameRecursive(roots[i].transform, name);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    private static Transform FindByNameRecursive(Transform current, string name)
    {
        if (current.name == name)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindByNameRecursive(current.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
