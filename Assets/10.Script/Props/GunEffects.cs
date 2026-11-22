using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game
{
    // 총 한 발의 피드백 (총성 · 총구 화염 · 총알 자국). 총구(Muzzle) 오브젝트에 붙인다.
    //   판정은 하지 않는다. "여기서 저기로 쐈다" 를 받아서 보여 주고 들려주기만 한다.
    //   플레이어 총(Gun)과 적(EnemyDuelist)이 같이 쓴다. 어디를 향해 쏠지는 부르는 쪽이 정한다.
    //
    //   총알 자국 : 총알이 지나간 길에 공기가 밀려난 자국(BulletWake 셰이더)이 남았다가 가라앉는다
    //     ① 자국의 머리가 총구 → 끝점으로 날아간다 (꼬리는 총구에 남아, 지나간 길 전체가 자국이 된다)
    //     ② 끝점에 닿으면 wakeLifetime 초 동안 서서히 옅어지다 사라진다
    //     한 발마다 자국이 따로 생긴다 (Muzzle 의 LineRenderer 를 본떠 자식 BulletWake 를 만든다).
    //     앞 자국이 사라지기 전에 연달아 쏴도 앞 자국은 지워지지 않고 끝까지 가라앉는다
    //
    //   오브젝트 구성 (총구 = 이 오브젝트의 위치, 파란 화살표 z = 쏘는 방향)
    //     Muzzle         ← GunEffects + AudioSource + LineRenderer (Material = BulletWake)
    //       MuzzleFlash  ← Particle System (한 오브젝트에 Renderer 는 하나뿐이라 자식으로 뺀다)
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(LineRenderer))]
    public class GunEffects : MonoBehaviour
    {
        private const string WakeShaderName = "Game/BulletWake";

        [Header("연결")]
        [SerializeField] private ParticleSystem muzzleFlash;   // 자식 MuzzleFlash
        [SerializeField] private AudioClip fireClip;           // 총성

        [Header("총알 자국")]
        [SerializeField] private float bulletSpeed = 120f;     // 자국 머리가 날아가는 빠르기 (m/초). 진짜 총알(약 300m/초)은 너무 빨라 안 보여서 줄였다
        [SerializeField] private float wakeLifetime = 0.8f;    // 끝점에 닿은 뒤 자국이 가라앉는 시간 (초)

        // 지금 화면에 보이는 자국 수 (플레이어 것 + 적 것). static = 모든 GunEffects 가 같이 쓰는 칸 하나
        //   화면 복사(Opaque Texture)는 카메라에 하나라서, 마지막 자국이 사라질 때만 끈다
        private static int visibleWakeCount;

        private AudioSource audioSource;
        private LineRenderer template;   // Muzzle 의 LineRenderer. 직접 그리지 않고, 한 발마다 쓸 자국의 "본" 으로만 쓴다

        // 한 발 = 자국 하나. 앞 자국이 사라지기 전에 또 쏴도 앞 자국은 끝까지 남는다
        //   다 쓴 자국은 버리지 않고 모아 두었다가 다음 발에 다시 쓴다 (쏠 때마다 오브젝트를 만들지 않게)
        private readonly List<LineRenderer> idleWakes = new List<LineRenderer>();
        private readonly List<LineRenderer> activeWakes = new List<LineRenderer>();

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            template = GetComponent<LineRenderer>();

            // 자국은 BulletWake 셰이더로 그려야 한다. 다른 머티리얼이면 그냥 색 줄이 그려진다
            if (template.sharedMaterial == null || template.sharedMaterial.shader.name != WakeShaderName)
                throw new Exception($"[GunEffects] {name} 의 LineRenderer Material 이 {WakeShaderName} 셰이더가 아닙니다. Shader 를 {WakeShaderName} 로 고른 Material 을 꽂아 주세요.");

            template.enabled = false;   // 본은 늘 꺼 둔다
        }

        private void OnDisable()
        {
            // 자국이 날아가는 도중에 꺼지면(씬 다시 열기 등) 코루틴이 멈춘다 → 남은 자국을 여기서 모두 끈다
            StopAllCoroutines();
            for (int i = activeWakes.Count - 1; i >= 0; i--)
                HideWake(activeWakes[i]);
        }

        // 한 발 쏜 모습 : 총성 + 총구 화염 + target 까지 총알 자국
        //   target = 총알이 끝나는 곳 (맞은 곳, 또는 빗나가서 지나간 먼 곳)
        public void Play(Vector3 target)
        {
            // ① 총성. PlayOneShot 은 소리가 겹쳐도 앞 소리를 끊지 않는다
            audioSource.PlayOneShot(fireClip);

            // ② 총구 화염 (Particle System 의 Burst 가 한 번 터진다)
            muzzleFlash.Play();

            // ③ 총알 자국. 발마다 새 자국 → 앞 자국은 자기 시간대로 가라앉는다
            StartCoroutine(WakeRoutine(ShowWake(), transform.position, target));
        }

        private IEnumerator WakeRoutine(LineRenderer line, Vector3 from, Vector3 to)
        {
            Vector3 direction = (to - from).normalized;
            float distance = Vector3.Distance(from, to);

            SetWakeStrength(line, 1f);
            line.SetPosition(0, from);
            line.SetPosition(1, from);

            // ① 머리가 날아간다. 예) 10m, 120m/초 → 약 0.08초
            float traveled = 0f;
            while (traveled < distance)
            {
                traveled += bulletSpeed * Time.deltaTime;
                line.SetPosition(1, from + direction * Mathf.Min(traveled, distance));
                yield return null;
            }
            line.SetPosition(1, to);

            // ② 자국이 가라앉는다 (세기 1 → 0)
            float timer = 0f;
            while (timer < wakeLifetime)
            {
                timer += Time.deltaTime;
                SetWakeStrength(line, 1f - timer / wakeLifetime);
                yield return null;
            }

            HideWake(line);
        }

        // 자국 세기 (0 ~ 1). 꼭짓점 색의 알파로 셰이더에 넘긴다 (셰이더의 input.color.a)
        //   Material 의 값을 바꾸지 않는 이유 : 머티리얼 하나를 플레이어 총 · 적 총이 같이 쓰기 때문
        private static void SetWakeStrength(LineRenderer line, float strength)
        {
            Color color = new Color(1f, 1f, 1f, strength);
            line.startColor = color;
            line.endColor = color;
        }

        // 자국 하나 꺼내 켜기 : 쉬는 자국이 있으면 다시 쓰고, 없으면 새로 만든다
        //   첫 자국이면 카메라에 화면 복사를 켜 달라고 한다
        private LineRenderer ShowWake()
        {
            LineRenderer line;
            if (idleWakes.Count > 0)
            {
                line = idleWakes[idleWakes.Count - 1];
                idleWakes.RemoveAt(idleWakes.Count - 1);
            }
            else
            {
                line = CreateWake();
            }

            line.enabled = true;
            activeWakes.Add(line);

            visibleWakeCount++;
            if (visibleWakeCount == 1)
                SetSceneColorCopy(true);
            return line;
        }

        // 자국 끄기 : 쉬는 자국으로 돌려 두고, 마지막 자국이면 화면 복사를 다시 끈다
        private void HideWake(LineRenderer line)
        {
            if (activeWakes.Remove(line) == false) return;   // 이미 끈 자국 (두 번 빼지 않게)

            line.enabled = false;
            idleWakes.Add(line);

            visibleWakeCount--;
            if (visibleWakeCount == 0)
                SetSceneColorCopy(false);
        }

        // 새 자국 : Muzzle 의 LineRenderer(본)와 같은 모양으로 자식 오브젝트를 만든다
        //   월드 좌표로 그려서, 총구 자식이어도 총이 움직일 때 자국은 제자리에 남는다
        private LineRenderer CreateWake()
        {
            GameObject wakeObject = new GameObject("BulletWake");
            wakeObject.transform.SetParent(transform, false);

            LineRenderer line = wakeObject.AddComponent<LineRenderer>();
            line.sharedMaterial = template.sharedMaterial;
            line.widthMultiplier = template.widthMultiplier;
            line.widthCurve = template.widthCurve;
            line.alignment = template.alignment;
            line.numCapVertices = template.numCapVertices;
            line.numCornerVertices = template.numCornerVertices;
            line.shadowCastingMode = template.shadowCastingMode;
            line.receiveShadows = template.receiveShadows;
            line.sortingLayerID = template.sortingLayerID;
            line.sortingOrder = template.sortingOrder;
            line.useWorldSpace = true;
            line.positionCount = 2;                     // 총구 · 머리 두 점
            line.textureMode = LineTextureMode.Tile;    // uv.x = 총구에서 몇 m 왔나 → 셰이더의 고리 간격이 거리와 상관없이 같다
            line.enabled = false;
            return line;
        }

        // 카메라의 화면 복사(Opaque Texture) 켜기 / 끄기
        //   켜기 : 이 카메라만 On. 끄기 : 파이프라인 설정(Mobile_RPAsset 의 Opaque Texture, 꺼져 있음)을 따른다
        private void SetSceneColorCopy(bool isOn)
        {
            Camera mainCamera = Camera.main;   // Tag 가 MainCamera 인 카메라 (XR Origin 의 Main Camera)

            if (mainCamera == null)
            {
                // 끌 때는 씬을 닫는 중이라 카메라가 먼저 사라졌을 수 있다 → 끌 것이 없으니 넘어간다
                if (isOn == false) return;
                throw new Exception("[GunEffects] MainCamera 태그가 붙은 카메라가 없습니다. XR Origin 의 Main Camera 의 Tag 를 MainCamera 로 바꿔 주세요.");
            }

            UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
            cameraData.requiresColorOption = isOn ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;
        }
    }
}
