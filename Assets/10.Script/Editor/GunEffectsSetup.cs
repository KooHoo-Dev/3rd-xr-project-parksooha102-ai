using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    // [에디터 도구] 총구(GunEffects)를 한 번에 꾸민다. GunEffects 의 ⋮ 메뉴 → "총구 효과 자동 설정"
    //   ① 자식 MuzzleFlash (Particle System) 를 만들고 총구 화염 모양으로 맞춘다
    //   ② LineRenderer 를 총알 자국(BulletWake) 모양으로 맞춘다
    //   ③ AudioSource 를 총성용으로 맞춘다
    //   ④ GunEffects 의 MuzzleFlash 칸, 부모 Gun 의 Effects 칸을 연결한다
    //   총성 클립(Fire Clip)은 직접 꽂는다. 모양을 바꾸고 싶으면 아래 숫자만 고치고 메뉴를 다시 누른다.
    //
    //   먼저 할 일 : 총구 자리에 빈 오브젝트 Muzzle 을 만들고 (파란 화살표 z = 쏘는 방향) GunEffects 를 붙인다.
    //               AudioSource · LineRenderer 는 GunEffects 를 붙이면 저절로 따라 붙는다.
    public static class GunEffectsSetup
    {
        private const string UndoName = "총구 효과 자동 설정";
        private const string MaterialFolder = "Assets/40.Shader";
        private const string WakeMaterialPath = MaterialFolder + "/BulletWake.mat";
        private const string FlashMaterialPath = MaterialFolder + "/MuzzleFlash.mat";
        private const string WakeShaderName = "Game/BulletWake";
        private const string FlashObjectName = "MuzzleFlash";

        // URP 기본 파티클 머티리얼 (부드러운 동그라미 그림). 이것을 복사해 더하기(Additive) 섞기로 바꿔 쓴다
        private const string ParticleSourcePath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";

        // ---- 총구 화염 (단위 : m, 초) ----
        //   VR 은 1초에 72 ~ 90장을 그린다. 0.1초면 7 ~ 9장 → 이보다 짧으면 눈에 잘 안 남는다
        private const float FlashDuration = 0.1f;                    // 한 번 터지는 길이
        private const short FlashCount = 8;                          // 한 번에 터지는 알갱이 수
        private const float FlashLifetimeMin = 0.06f;                // 알갱이가 사는 시간 (최소 ~ 최대)
        private const float FlashLifetimeMax = 0.12f;
        private const float FlashSpeedMin = 0.2f;                    // 앞으로 튀어 나가는 빠르기 (m/초). 느려야 총구 앞에 뭉쳐 보인다
        private const float FlashSpeedMax = 1.5f;
        private const float FlashSizeMin = 0.12f;                    // 알갱이 크기 (m). 기본 파티클 그림은 가장자리가 흐려서, 보이는 크기는 이것의 절반쯤
        private const float FlashSizeMax = 0.28f;
        private const float FlashConeAngle = 15f;                    // 퍼지는 각도 (도). 작을수록 앞으로 곧게
        private const float FlashConeRadius = 0.005f;                // 총구 구멍 반지름
        private const int FlashRenderQueue = 3100;                   // 그리는 차례. 총알 자국(3000, 뒤 풍경을 다시 칠한다)보다 나중에 그려야 자국에 덮이지 않는다
        private static readonly Color FlashColorBright = new Color(1f, 0.95f, 0.6f);    // 막 터진 색 (흰 노랑)
        private static readonly Color FlashColorMid = new Color(1f, 0.6f, 0.15f);       // 주황
        private static readonly Color FlashColorEnd = new Color(0.9f, 0.25f, 0.05f);    // 사라질 때 (붉은 주황)

        // ---- 총알 자국 ----
        private const float WakeWidth = 0.07f;                       // 자국 굵기 (m). 렌즈가 보이려면 너무 가늘면 안 된다

        // ---- 총성 ----
        private const float SoundMinDistance = 5f;                   // 이 거리까지는 소리가 줄지 않는다 (m). 10m 앞 적 총성도 잘 들리게
        private const float SoundMaxDistance = 60f;                  // 이 거리부터는 더 줄지 않는다

        // ---- 적 프리팹 ----
        private const string EnemyPrefabFolder = "Assets/Resources/Enemies";   // enemies.json 이 부르는 적 프리팹이 모인 곳

        // 적 프리팹마다 EnemyDuelist 의 Gun Effects 칸에, 그 적이 들고 있는 총의 GunEffects 를 꽂는다
        //   먼저 할 일 : 적 총(Resources/Props/SM_Wep_Revolver_01) 프리팹에 Muzzle + GunEffects 를 만들고 "총구 효과 자동 설정" 을 누른다
        //               그 총 프리팹이 적 프리팹 안에 들어 있어서, 적 프리팹을 열면 GunEffects 가 하나 보인다
        //   프리팹 파일을 직접 고쳐 저장하므로 Ctrl+Z 로는 되돌릴 수 없다 (Git 으로 되돌린다)
        [MenuItem("Tools/정오의 결투/적 프리팹 총구 연결")]
        private static void LinkEnemyGunEffects()
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[GunEffectsSetup] Play 를 멈춘 뒤에 눌러 주세요.");

            // ① 폴더 안의 프리팹을 모두 찾는다 (FindAssets 는 이름표(guid) 목록을 돌려준다)
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { EnemyPrefabFolder });
            if (guids.Length == 0)
                throw new Exception($"[GunEffectsSetup] {EnemyPrefabFolder} 에 프리팹이 없습니다. 폴더 이름이 바뀌었으면 EnemyPrefabFolder 를 고쳐 주세요.");

            string report = "";
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                // ② 프리팹을 씬 밖에서 잠깐 연다 (Prefab Mode 를 손으로 여는 것과 같다)
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    report += LinkEnemy(root, path) + "\n";

                    // ③ 고친 내용을 프리팹 파일에 저장한다
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    // 에러가 나도 열어 둔 프리팹은 꼭 닫는다 (안 닫으면 보이지 않는 오브젝트가 메모리에 남는다)
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"[GunEffectsSetup] 적 프리팹 {guids.Length}개 총구 연결 완료\n{report}");
        }

        // 적 프리팹 하나 : 맨 위 EnemyDuelist 의 gunEffects 칸에 자식의 GunEffects 를 넣는다
        private static string LinkEnemy(GameObject root, string path)
        {
            EnemyDuelist enemy = root.GetComponent<EnemyDuelist>();
            if (enemy == null)
                throw new Exception($"[GunEffectsSetup] {path} 맨 위에 EnemyDuelist 가 없습니다. 적 프리팹이 아니면 {EnemyPrefabFolder} 밖으로 옮겨 주세요.");

            // 꺼진 자식까지 찾는다 (true). 총이 홀스터 · 손 어디에 붙어 있어도 하나여야 한다
            GunEffects[] found = root.GetComponentsInChildren<GunEffects>(true);
            if (found.Length == 0)
                throw new Exception($"[GunEffectsSetup] {path} 안에 GunEffects 가 없습니다. 적 총 프리팹(SM_Wep_Revolver_01)에 Muzzle 을 만들고 GunEffects 를 붙여 주세요.");
            if (found.Length > 1)
                throw new Exception($"[GunEffectsSetup] {path} 안에 GunEffects 가 {found.Length}개 있습니다. 적 총의 Muzzle 에 하나만 남겨 주세요.");

            SerializedObject enemyObject = new SerializedObject(enemy);
            enemyObject.FindProperty("gunEffects").objectReferenceValue = found[0];
            enemyObject.ApplyModifiedPropertiesWithoutUndo();   // 프리팹 파일을 직접 고치므로 되돌리기 기록은 남기지 않는다

            return $"  {root.name} → {found[0].name}";
        }

        [MenuItem("CONTEXT/GunEffects/총구 효과 자동 설정")]
        private static void SetupFromContext(MenuCommand command)
        {
            // command.context = ⋮ 를 누른 그 컴포넌트
            Setup((GunEffects)command.context);
        }

        private static void Setup(GunEffects effects)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("[GunEffectsSetup] Play 를 멈춘 뒤에 눌러 주세요. Play 중에 바꾼 것은 멈추면 사라집니다.");

            // 되돌리기(Ctrl+Z) 준비 : 지금 Muzzle 아래 모습을 통째로 기억해 둔다
            Undo.RegisterFullObjectHierarchyUndo(effects.gameObject, UndoName);

            // ① 재료 (머티리얼 두 개. 없으면 만든다)
            Material wakeMaterial = LoadOrCreateWakeMaterial();
            Material flashMaterial = LoadOrCreateFlashMaterial();

            // ② 총구 화염
            ParticleSystem flash = FindOrCreateFlash(effects.transform);
            SetupFlash(flash, flashMaterial);

            // ③ 총알 자국
            SetupWake(effects.GetComponent<LineRenderer>(), wakeMaterial);

            // ④ 총성
            SetupAudio(effects.GetComponent<AudioSource>());

            // ⑤ 칸 연결 (private 칸이라 SerializedObject 로 이름을 적어 넣는다)
            SerializedObject effectsObject = new SerializedObject(effects);
            effectsObject.FindProperty("muzzleFlash").objectReferenceValue = flash;
            effectsObject.ApplyModifiedProperties();

            //    부모에 플레이어 총(Gun)이 있으면 Gun 의 Effects 칸도 이 총구로 (적 총은 Gun 이 없어서 건너뛴다)
            Gun gun = effects.GetComponentInParent<Gun>(true);
            if (gun != null)
            {
                SerializedObject gunObject = new SerializedObject(gun);
                gunObject.FindProperty("effects").objectReferenceValue = effects;
                gunObject.ApplyModifiedProperties();
            }

            string gunMessage = gun != null ? $"{gun.name} 의 Effects 칸도 연결했습니다." : "부모에 Gun 이 없어서 (적 총) Gun 연결은 건너뛰었습니다. 적 프리팹의 EnemyDuelist → Gun Effects 칸에 이 Muzzle 을 꽂아 주세요.";
            Debug.Log($"[GunEffectsSetup] {effects.name} 총구 효과를 맞췄습니다. {gunMessage} 총성 클립(Fire Clip)은 직접 꽂아 주세요.", effects);
        }

        // 자식 MuzzleFlash 를 찾고, 없으면 만든다
        private static ParticleSystem FindOrCreateFlash(Transform muzzle)
        {
            Transform child = muzzle.Find(FlashObjectName);
            if (child == null)
            {
                GameObject flashObject = new GameObject(FlashObjectName);
                Undo.RegisterCreatedObjectUndo(flashObject, UndoName);   // Ctrl+Z 하면 만든 것도 지운다
                child = flashObject.transform;
                child.SetParent(muzzle, false);
            }

            // 총구와 딱 겹치고 같은 쪽을 본다 (메뉴로 만든 Particle System 은 X -90 으로 생겨 위로 뿜는다 → 0 으로)
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            ParticleSystem flash = child.GetComponent<ParticleSystem>();
            if (flash == null)
                flash = Undo.AddComponent<ParticleSystem>(child.gameObject);
            return flash;
        }

        // 총구 화염 모양 : 앞으로 좁게 퍼지는 불꽃 알갱이가 한 번 터지고 0.1초 안에 사라진다
        private static void SetupFlash(ParticleSystem flash, Material material)
        {
            Undo.RecordObject(flash, UndoName);
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // 재생 중이면 Duration 을 못 바꿔서 먼저 멈춘다

            // ① Main : 한 번만, 저절로 켜지지 않게 (GunEffects.Play 가 켠다)
            ParticleSystem.MainModule main = flash.main;
            main.duration = FlashDuration;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(FlashLifetimeMin, FlashLifetimeMax);   // 두 값 사이 무작위
            main.startSpeed = new ParticleSystem.MinMaxCurve(FlashSpeedMin, FlashSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(FlashSizeMin, FlashSizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);   // 0 ~ 360도 (라디안)
            main.startColor = Color.white;                                             // 색은 ④ Color over Lifetime 이 정한다
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;               // 총을 따라 움직인다
            main.scalingMode = ParticleSystemScalingMode.Local;                        // 부모(총 모델)의 크기는 무시한다 → 위 숫자가 그대로 m
            main.maxParticles = FlashCount * 2;

            // ② Emission : 계속 뿜지 않고, 0초에 FlashCount 개를 한 번에 (Burst)
            ParticleSystem.EmissionModule emission = flash.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, FlashCount) });

            // ③ Shape : 총구 구멍에서 앞(z)으로 좁은 원뿔
            ParticleSystem.ShapeModule shape = flash.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = FlashConeAngle;
            shape.radius = FlashConeRadius;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            // ④ Color over Lifetime : 흰 노랑 → 주황 → 붉은 주황, 끝에서 투명
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = flash.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(FlashColorBright, 0f), new GradientColorKey(FlashColorMid, 0.5f), new GradientColorKey(FlashColorEnd, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            // ⑤ Size over Lifetime : 확 커졌다가 조금 줄어든다 (0.7 → 1 → 0.4). 0 까지 줄이면 금방 사라져 보여서 남겨 둔다
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = flash.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.4f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // ⑥ Renderer : 늘 카메라를 보는 동그란 알갱이(Billboard), 그림자 없음
            //    늘인 알갱이(Stretch)를 쓰지 않는 이유 : 플레이어는 총열을 따라 앞을 보므로 알갱이를 "끝에서" 보게 된다.
            //    날아가는 방향으로 늘인 판을 끝에서 보면 거의 선 하나로 납작해져 안 보인다
            ParticleSystemRenderer flashRenderer = flash.GetComponent<ParticleSystemRenderer>();
            Undo.RecordObject(flashRenderer, UndoName);
            flashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            flashRenderer.sharedMaterial = material;
            flashRenderer.shadowCastingMode = ShadowCastingMode.Off;
            flashRenderer.receiveShadows = false;

            // 프리팹 안의 오브젝트면 "바꿨다" 고 프리팹에 알린다 (안 하면 저장할 때 바뀐 값이 빠질 수 있다)
            PrefabUtility.RecordPrefabInstancePropertyModifications(flash);
            PrefabUtility.RecordPrefabInstancePropertyModifications(flashRenderer);
        }

        // 총알 자국 모양 : BulletWake 머티리얼, 카메라를 보는 띠, 그림자 없음. 쏘기 전에는 꺼 둔다
        private static void SetupWake(LineRenderer line, Material material)
        {
            Undo.RecordObject(line, UndoName);
            line.sharedMaterial = material;
            line.widthMultiplier = WakeWidth;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);   // 처음부터 끝까지 같은 굵기
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.zero);
            line.alignment = LineAlignment.View;                     // 늘 카메라 쪽을 본다 (옆에서 봐도 납작하지 않게)
            line.textureMode = LineTextureMode.Tile;                 // 셰이더 고리 간격 (GunEffects 도 Awake 에서 맞춘다)
            line.numCapVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;                                    // 씬에서 원점에 줄이 보이지 않게
            PrefabUtility.RecordPrefabInstancePropertyModifications(line);
        }

        // 총성 : 저절로 안 울리고, 총 자리에서 3D 로 들린다
        private static void SetupAudio(AudioSource audioSource)
        {
            Undo.RecordObject(audioSource, UndoName);
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;                           // 1 = 3D (소리 나는 쪽에서 들린다)
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = SoundMinDistance;
            audioSource.maxDistance = SoundMaxDistance;
            PrefabUtility.RecordPrefabInstancePropertyModifications(audioSource);
        }

        // BulletWake 머티리얼 : 있으면 꺼내고, 없으면 셰이더로 새로 만든다
        private static Material LoadOrCreateWakeMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WakeMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find(WakeShaderName);
            if (shader == null)
                throw new Exception($"[GunEffectsSetup] {WakeShaderName} 셰이더를 못 찾았습니다. {MaterialFolder}/BulletWake.shader 가 Console 에러 없이 컴파일됐는지 확인해 주세요.");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, WakeMaterialPath);
            return material;
        }

        // MuzzleFlash 머티리얼 : 없으면 URP 기본 파티클 머티리얼을 복사해 만들고, 있어도 섞기 · 그리는 차례를 매번 다시 맞춘다
        //   (예전 버전 도구가 만든 더하기 섞기 머티리얼도 메뉴를 다시 누르면 고쳐진다)
        //
        //   보통 섞기(Alpha) 를 쓰는 이유 : 정오의 햇빛 아래 밝은 모래 · 하늘 앞이라서
        //     더하기(Additive) = 뒤 색에 밝기를 더한다 → 뒤가 이미 밝으면 더할 자리가 없어 하얗게 묻힌다
        //     보통 섞기 = 불꽃 색으로 덮는다 → 밝은 배경에서도 주황이 그대로 보인다
        private static Material LoadOrCreateFlashMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
            if (material == null)
            {
                Material source = AssetDatabase.LoadAssetAtPath<Material>(ParticleSourcePath);
                if (source == null)
                    throw new Exception($"[GunEffectsSetup] {ParticleSourcePath} 를 못 찾았습니다. URP 패키지가 설치돼 있는지 확인해 주세요.");

                material = new Material(source);
                AssetDatabase.CreateAsset(material, FlashMaterialPath);
            }

            Undo.RecordObject(material, UndoName);

            // URP 파티클 셰이더의 섞기 칸 : _Blend 0 = Alpha. 실제 섞는 식(_SrcBlend · _DstBlend)도 같이 맞춰야 적용된다
            //   SrcAlpha × 내 색 + OneMinusSrcAlpha × 뒤 색 (URP 기본 파티클 머티리얼과 같은 값)
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);

            // 그리는 차례 : 총알 자국보다 나중에. 자국은 뒤 풍경(불투명만 찍힌 그림)을 다시 칠하므로, 먼저 그린 화염을 지워 버린다
            //   적이 쏘면 화염(10m)이 자국 가운데(5m)보다 멀어서, 차례를 정해 두지 않으면 화염이 먼저 그려지고 자국에 덮인다
            material.renderQueue = FlashRenderQueue;

            EditorUtility.SetDirty(material);   // 바뀐 값을 머티리얼 파일에 저장하게 표시한다
            return material;
        }
    }
}
