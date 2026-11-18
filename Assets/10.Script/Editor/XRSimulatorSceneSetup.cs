using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// 헤드셋 없이 XR Interaction Simulator로 바로 Play 테스트할 수 있게
/// 씬과 프로젝트 설정을 교안(00. Panorama Night Lake)과 같은 구성으로 맞춰 준다.
///
/// 메뉴: Tools > XR 테스트 환경
///  - 현재 씬 구성하기 : 열려 있는 씬에 빠진 것만 추가한다 (이미 있는 것은 건드리지 않음).
///  - 새 테스트 씬 만들기 : 빈 씬을 만들고 같은 구성을 넣은 뒤 저장한다.
///
/// 구성 내용 (교안 씬과 동일)
///  - XR Origin (XR Rig)            : Starter Assets 프리팹
///  - XR Interaction Manager
///  - Event System + XR UI Input Module
///  - 바닥 (XR Origin에 중력이 있어서 바닥이 없으면 계속 떨어짐)
///  - Project Settings: XR Interaction Simulator 자동 생성 켜기
/// </summary>
public static class XRSimulatorSceneSetup
{
    const string k_Menu = "Tools/XR 테스트 환경/";
    const string k_Title = "XR 테스트 환경";
    const string k_XROriginPrefabName = "XR Origin (XR Rig)";
    const string k_SimulatorPrefabName = "XR Interaction Simulator";
    const string k_XRRootName = "XR";

    const string k_ControlsHelp =
        "[시뮬레이터 조작키]\n" +
        "W A S D : 이동   Q / E : 아래 / 위\n" +
        "마우스 오른쪽 버튼 누른 채 이동 : 시점 회전\n" +
        "Tab : 조작 대상 전환 (머리 / 왼손 / 오른손)\n" +
        "[ / ] : 왼손 / 오른손 조작   H : 머리 조작\n" +
        "G : 그립(잡기)   T : 트리거   R : 위치 초기화";

    [MenuItem(k_Menu + "현재 씬 구성하기 (기기 없이 테스트)", false, 0)]
    static void SetupActiveSceneMenu()
    {
        if (CanRun() == false)
            return;

        var scene = SceneManager.GetActiveScene();
        var log = SetupScene(scene);

        var message = log.Count == 0
            ? "이미 모두 구성되어 있어요. Play를 눌러 바로 테스트하세요."
            : "다음 항목을 구성했어요.\n\n- " + string.Join("\n- ", log) +
              "\n\n씬을 저장(Ctrl+S)한 뒤 Play를 눌러 테스트하세요.";

        Debug.Log($"[{k_Title}] {scene.name}\n{message}");
        EditorUtility.DisplayDialog(k_Title, message + "\n\n" + k_ControlsHelp, "확인");
    }

    [MenuItem(k_Menu + "새 테스트 씬 만들기", false, 1)]
    static void CreateTestSceneMenu()
    {
        if (CanRun() == false || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            return;

        var path = EditorUtility.SaveFilePanelInProject(
            k_Title, "XR Test", "unity", "새 테스트 씬을 저장할 위치를 고르세요.", "Assets/01.Scene");
        if (string.IsNullOrEmpty(path))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        light.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f), Quaternion.Euler(50f, -30f, 0f));

        SetupScene(scene);
        EditorSceneManager.SaveScene(scene, path);

        Debug.Log($"[{k_Title}] 새 테스트 씬을 만들었어요: {path}");
        EditorUtility.DisplayDialog(k_Title,
            $"새 테스트 씬을 만들었어요.\n{path}\n\nPlay를 눌러 바로 테스트하세요.\n\n{k_ControlsHelp}", "확인");
    }

    static bool CanRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode == false)
            return true;

        EditorUtility.DisplayDialog(k_Title, "Play 모드를 끈 다음 다시 실행해 주세요.", "확인");
        return false;
    }

    /// <summary>씬에 빠진 구성만 추가하고, 무엇을 했는지 목록으로 돌려준다.</summary>
    static List<string> SetupScene(Scene scene)
    {
        var log = new List<string>();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(k_Title + " 구성");
        var undoGroup = Undo.GetCurrentGroup();

        EnsureSimulatorSettings(log);
        var origin = EnsureXROrigin(scene, log);
        EnsureInteractionManager(scene, log);
        EnsureEventSystem(scene, log);
        if (origin != null)
        {
            DisableOtherMainCameras(scene, origin, log);
            EnsureGround(origin, log);
        }

        Undo.CollapseUndoOperations(undoGroup);
        if (log.Count > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return log;
    }

    // ---------------------------------------------------------------- Project Settings

    static void EnsureSimulatorSettings(List<string> log)
    {
        // XRDeviceSimulatorSettings 클래스는 internal이라 SerializedObject로 다룬다.
        var path = AssetDatabase.FindAssets("XRDeviceSimulatorSettings")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileName(p) == "XRDeviceSimulatorSettings.asset");
        if (path == null)
        {
            log.Add("[확인 필요] 시뮬레이터 설정 에셋이 없어요. Project Settings > XR Plug-in Management > " +
                    "XR Interaction Toolkit 페이지를 한 번 연 뒤 다시 실행하세요.");
            return;
        }

        var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(path));
        var autoCreate = settings.FindProperty("m_AutomaticallyInstantiateSimulatorPrefab");
        var useClassic = settings.FindProperty("m_UseClassic");
        var prefab = settings.FindProperty("m_SimulatorPrefab");

        if (autoCreate.boolValue == false)
        {
            autoCreate.boolValue = true;
            log.Add("Project Settings: XR Interaction Simulator 자동 생성 켬");
        }

        if (prefab.objectReferenceValue == null)
        {
            var simulator = FindPrefab(k_SimulatorPrefabName, "Samples/XR Interaction Toolkit");
            if (simulator != null)
            {
                prefab.objectReferenceValue = simulator;
                useClassic.boolValue = false;
                log.Add("Project Settings: 시뮬레이터 프리팹 연결");
            }
            else
            {
                log.Add("[확인 필요] XR Interaction Simulator 프리팹이 없어요. Package Manager > XR Interaction Toolkit > " +
                        "Samples에서 'XR Interaction Simulator'를 Import 하세요.");
            }
        }

        if (settings.ApplyModifiedProperties())
            AssetDatabase.SaveAssetIfDirty(settings.targetObject);
    }

    // ---------------------------------------------------------------- Scene objects

    static XROrigin EnsureXROrigin(Scene scene, List<string> log)
    {
        var existing = FindInScene<XROrigin>(scene);
        if (existing != null)
            return existing;

        var prefab = FindPrefab(k_XROriginPrefabName, "Starter Assets");
        if (prefab == null)
        {
            log.Add("[확인 필요] XR Origin (XR Rig) 프리팹이 없어요. Package Manager > XR Interaction Toolkit > " +
                    "Samples에서 'Starter Assets'를 Import 하세요.");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create XR Origin");
        go.transform.SetParent(GetOrCreateXRRoot(scene), false);
        log.Add("XR Origin (XR Rig) 추가");
        return go.GetComponent<XROrigin>();
    }

    static void EnsureInteractionManager(Scene scene, List<string> log)
    {
        if (FindInScene<XRInteractionManager>(scene) != null)
            return;

        var go = new GameObject("XR Interaction Manager", typeof(XRInteractionManager));
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create XR Interaction Manager");
        go.transform.SetParent(GetOrCreateXRRoot(scene), false);
        log.Add("XR Interaction Manager 추가");
    }

    static void EnsureEventSystem(Scene scene, List<string> log)
    {
        var eventSystem = FindInScene<EventSystem>(scene);
        if (eventSystem == null)
        {
            var go = new GameObject("Event System", typeof(EventSystem), typeof(XRUIInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create Event System");
            go.transform.SetParent(GetOrCreateXRRoot(scene), false);
            log.Add("Event System (XR UI Input Module) 추가 — 컨트롤러 레이로 UI를 누를 수 있게 함");
            return;
        }

        if (eventSystem.GetComponent<XRUIInputModule>() != null)
            return;

        // 일반 UI 입력 모듈은 XR 컨트롤러 레이를 처리하지 못하므로 교체한다. (XR UI Input Module은 마우스도 처리함)
        foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            Undo.DestroyObjectImmediate(module);
        Undo.AddComponent<XRUIInputModule>(eventSystem.gameObject);
        log.Add($"'{eventSystem.name}'의 UI 입력 모듈을 XR UI Input Module로 교체");
    }

    /// <summary>XR Origin 밖에 있는 Main Camera가 켜져 있으면 화면과 오디오 리스너가 겹치므로 끈다.</summary>
    static void DisableOtherMainCameras(Scene scene, XROrigin origin, List<string> log)
    {
        foreach (var cam in FindAllInScene<Camera>(scene))
        {
            if (cam.gameObject.activeSelf == false || cam.CompareTag("MainCamera") == false || cam.transform.IsChildOf(origin.transform))
                continue;

            Undo.RecordObject(cam.gameObject, "Disable Main Camera");
            cam.gameObject.SetActive(false);
            log.Add($"XR Origin 밖의 카메라 '{cam.name}' 비활성화 (XR Origin 카메라와 겹침)");
        }
    }

    /// <summary>XR Origin에는 중력(Gravity Provider)이 있어서 발밑에 콜라이더가 없으면 계속 떨어진다.</summary>
    static void EnsureGround(XROrigin origin, List<string> log)
    {
        var originTransform = origin.transform;
        Physics.SyncTransforms();
        var hits = Physics.RaycastAll(originTransform.position + Vector3.up * 2f, Vector3.down, 100f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits.Any(hit => hit.collider.transform.IsChildOf(originTransform) == false))
            return;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        SceneManager.MoveGameObjectToScene(ground, originTransform.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        ground.transform.position = originTransform.position;
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        log.Add("XR Origin 발밑에 바닥이 없어서 Ground(50m x 50m) 추가 — 맵을 만든 뒤 지워도 돼요");
    }

    // ---------------------------------------------------------------- Helpers

    static Transform GetOrCreateXRRoot(Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == k_XRRootName);
        if (root != null)
            return root.transform;

        root = new GameObject(k_XRRootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Create XR Root");
        return root.transform;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    static IEnumerable<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    /// <summary>이름이 정확히 같은 프리팹을 찾는다. 경로에 pathHint가 있는 것, 그중 버전 폴더가 높은 것을 우선한다.</summary>
    static GameObject FindPrefab(string prefabName, string pathHint)
    {
        var path = AssetDatabase.FindAssets("t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileNameWithoutExtension(p) == prefabName)
            .OrderByDescending(p => p.Contains(pathHint))
            .ThenByDescending(p => p, System.StringComparer.Ordinal)
            .FirstOrDefault();

        return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
