using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    // [에디터 도구] SoundManager 가 쓰는 Audio Mixer 를 메뉴 한 번으로 만든다.
    //   Tools > 정오의 결투 > 사운드 믹서 만들기
    //
    //   만드는 것 (SoundManager 의 상수와 이름이 같아야 한다)
    //     Assets/Resources/Audio/MainMixer.mixer
    //       Master
    //         BGM · SFX · Ambience
    //     노출 파라미터 : MasterVolume · BGMVolume · SFXVolume · AmbienceVolume (각 그룹의 Volume)
    //   열려 있는 씬에 TitleBGM(스크립트 없는 AudioSource)이 있으면 Output 칸에 BGM 그룹을 꽂는다
    //
    //   유니티는 Mixer 를 코드로 만드는 공개 함수가 없다. Project 창의 Create → Audio Mixer 가 쓰는
    //   에디터 안쪽 함수(AudioMixerController)를 이름으로 찾아 부른다 (리플렉션).
    //   유니티 버전이 바뀌어 이름이 달라지면 무엇을 못 찾았는지 알려 주고 멈춘다 → 그때는 손으로 만든다
    public static class SoundMixerSetup
    {
        private const string Folder = "Assets/Resources/Audio";
        private const string MixerPath = Folder + "/MainMixer.mixer";
        private const string TitleBgmName = "TitleBGM";

        // 그룹 이름 → 노출 파라미터 이름 (SoundManager.GroupNames · ParamNames 와 같은 순서)
        private static readonly string[] GroupNames = { "Master", "BGM", "SFX", "Ambience" };
        private static readonly string[] ParamNames = { "MasterVolume", "BGMVolume", "SFXVolume", "AmbienceVolume" };

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [MenuItem("Tools/정오의 결투/사운드 믹서 만들기")]
        private static void Create()
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[SoundMixerSetup] Play 를 멈춘 뒤에 눌러 주세요.");

            // ① 에디터 안쪽 형식들 (UnityEditor.dll 안에 있다)
            Assembly editor = typeof(EditorApplication).Assembly;
            Type controllerType = FindType(editor, "UnityEditor.Audio.AudioMixerController");
            Type groupType = FindType(editor, "UnityEditor.Audio.AudioMixerGroupController");
            Type pathType = FindType(editor, "UnityEditor.Audio.AudioGroupParameterPath");

            // ② Mixer : 있으면 그대로 쓰고(빠진 그룹 · 파라미터만 채운다), 없으면 만든다
            EnsureFolder(Folder);
            Object controller = AssetDatabase.LoadMainAssetAtPath(MixerPath);
            if (controller == null)
            {
                MethodInfo create = FindMethod(controllerType, "CreateMixerControllerAtPath", AnyStatic);
                controller = (Object)create.Invoke(null, new object[] { MixerPath });
                Debug.Log($"[SoundMixerSetup] {MixerPath} 를 만들었습니다.");
            }
            else if (controllerType.IsInstanceOfType(controller) == false)
            {
                throw new Exception($"[SoundMixerSetup] {MixerPath} 가 Audio Mixer 가 아닙니다. 지우고 다시 눌러 주세요.");
            }

            AudioMixer mixer = (AudioMixer)controller;
            object master = FindProperty(controllerType, "masterGroup").GetValue(controller);

            // ③ 그룹 : Master 아래에 BGM · SFX · Ambience. 이미 있으면 그대로 쓴다
            AudioMixerGroup[] groups = new AudioMixerGroup[GroupNames.Length];
            groups[0] = (AudioMixerGroup)master;
            MethodInfo createGroup = FindMethod(controllerType, "CreateNewGroup", AnyInstance);
            MethodInfo addChild = FindMethod(controllerType, "AddChildToParent", AnyInstance);
            MethodInfo addToView = controllerType.GetMethod("AddGroupToCurrentView", AnyInstance);   // 없어도 된다 (Mixer 창에 보이게만 한다)

            for (int i = 1; i < GroupNames.Length; i++)
            {
                AudioMixerGroup group = FindChildGroup(mixer, GroupNames[i]);
                if (group == null)
                {
                    group = (AudioMixerGroup)createGroup.Invoke(controller, new object[] { GroupNames[i], false });
                    addChild.Invoke(controller, new[] { group, master });
                    addToView?.Invoke(controller, new object[] { group });
                }
                groups[i] = group;
            }

            // ④ 노출 파라미터 : 각 그룹의 Volume 을 노출하고 이름을 SoundManager 가 찾는 이름으로 바꾼다
            MethodInfo volumeGuid = FindMethod(groupType, "GetGUIDForVolume", AnyInstance);
            MethodInfo expose = FindMethod(controllerType, "AddExposedParameter", AnyInstance);
            PropertyInfo exposedProperty = FindProperty(controllerType, "exposedParameters");

            GUID[] guids = new GUID[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                guids[i] = (GUID)volumeGuid.Invoke(groups[i], null);
                object path = Activator.CreateInstance(pathType, groups[i], guids[i]);
                expose.Invoke(controller, new[] { path });   // 이미 노출돼 있으면 유니티가 아무것도 안 한다
            }

            Array exposed = (Array)exposedProperty.GetValue(controller);
            Type paramType = exposed.GetType().GetElementType();
            FieldInfo guidField = FindField(paramType, "guid");
            FieldInfo nameField = FindField(paramType, "name");
            for (int e = 0; e < exposed.Length; e++)
            {
                object parameter = exposed.GetValue(e);   // struct 라서 꺼낸 복사본을 고치고 다시 넣는다
                GUID guid = (GUID)guidField.GetValue(parameter);
                int index = Array.IndexOf(guids, guid);
                if (index < 0) continue;                  // 손으로 따로 노출한 다른 파라미터는 건드리지 않는다
                nameField.SetValue(parameter, ParamNames[index]);
                exposed.SetValue(parameter, e);
            }
            exposedProperty.SetValue(controller, exposed);
            controllerType.GetMethod("OnChangedExposedParameter", AnyInstance)?.Invoke(controller, null);   // 이름 캐시를 새로 고친다 (없는 버전도 있다)

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // ⑤ 확인 : 노출 이름이 다 들어갔나
            Array check = (Array)exposedProperty.GetValue(controller);
            foreach (string paramName in ParamNames)
            {
                bool found = false;
                for (int e = 0; e < check.Length; e++)
                    found |= (string)nameField.GetValue(check.GetValue(e)) == paramName;
                if (found == false)
                    throw new Exception($"[SoundMixerSetup] 노출 파라미터 {paramName} 를 만들지 못했습니다. Audio Mixer 창에서 그룹 Volume 을 우클릭 → Expose 하고 이름을 {paramName} 로 바꿔 주세요.");
            }

            // ⑥ 열려 있는 씬의 TitleBGM → BGM 그룹
            ConnectTitleBgm(groups[1]);

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);
            Debug.Log("[SoundMixerSetup] MainMixer 준비 끝 : Master > BGM · SFX · Ambience, 노출 파라미터 4개. 씬이 바뀌었으면 Ctrl+S 로 저장해 주세요.");
        }

        // Master 아래의 그룹 중 이름이 정확히 같은 것 (FindMatchingGroups 는 이름이 "들어간" 것을 다 준다)
        private static AudioMixerGroup FindChildGroup(AudioMixer mixer, string name)
        {
            foreach (AudioMixerGroup group in mixer.FindMatchingGroups("Master"))
                if (group.name == name)
                    return group;
            return null;
        }

        // 열려 있는 씬에서 이름이 TitleBGM 인 AudioSource 의 Output 을 BGM 그룹으로
        private static void ConnectTitleBgm(AudioMixerGroup bgm)
        {
            foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.gameObject.name != TitleBgmName) continue;

                Undo.RecordObject(source, "TitleBGM 을 BGM 그룹으로");
                source.outputAudioMixerGroup = bgm;
                EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
                Debug.Log($"[SoundMixerSetup] {source.gameObject.scene.name} 씬의 {TitleBgmName} Output → BGM");
                return;
            }
            Debug.Log($"[SoundMixerSetup] 열려 있는 씬에 {TitleBgmName} 이 없어서 건너뜁니다. Title 씬을 열고 한 번 더 누르거나, Output 칸에 BGM 그룹을 직접 꽂아 주세요.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        // ---------------------------------------------------------------- 리플렉션 (못 찾으면 바로 알린다)

        private const string ManualHint = " 유니티 버전이 달라 자동으로 만들 수 없습니다. Project 창 → Create → Audio Mixer 로 Assets/Resources/Audio/MainMixer 를 만들고, Master 아래에 BGM · SFX · Ambience 그룹을 만든 뒤 각 Volume 을 Expose 해서 MasterVolume · BGMVolume · SFXVolume · AmbienceVolume 로 이름을 바꿔 주세요.";

        private static Type FindType(Assembly assembly, string fullName)
        {
            Type type = assembly.GetType(fullName);
            if (type == null)
                throw new Exception($"[SoundMixerSetup] {fullName} 형식을 찾지 못했습니다." + ManualHint);
            return type;
        }

        private static MethodInfo FindMethod(Type type, string name, BindingFlags flags)
        {
            MethodInfo method = type.GetMethod(name, flags);
            if (method == null)
                throw new Exception($"[SoundMixerSetup] {type.Name}.{name} 함수를 찾지 못했습니다." + ManualHint);
            return method;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, AnyInstance);
            if (property == null)
                throw new Exception($"[SoundMixerSetup] {type.Name}.{name} 속성을 찾지 못했습니다." + ManualHint);
            return property;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field == null)
                throw new Exception($"[SoundMixerSetup] {type.Name}.{name} 칸을 찾지 못했습니다." + ManualHint);
            return field;
        }
    }
}
